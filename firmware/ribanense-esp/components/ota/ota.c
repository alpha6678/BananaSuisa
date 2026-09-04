#include "ota.h"
#include "net.h"
#include "ribanense_esp_version.h"

#include <ctype.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "cJSON.h"
#include "esp_crt_bundle.h"
#include "esp_http_client.h"
#include "esp_http_server.h"
#include "esp_log.h"
#include "esp_ota_ops.h"
#include "esp_system.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "mbedtls/sha256.h"

#define TAG "ota"
#define CHUNK 1024
#define MSG_MAX 48
#define SLOT_MAX 0x190000u

static httpd_handle_t s_httpd;
static volatile ota_state_t s_state = OTA_IDLE;
static char s_msg[MSG_MAX] = "OTA";
static volatile bool s_pull_busy;
static uint8_t s_chunk[CHUNK];

static void set_state(ota_state_t st, const char *msg)
{
    s_state = st;
    if (msg != NULL) {
        strncpy(s_msg, msg, sizeof(s_msg) - 1);
        s_msg[sizeof(s_msg) - 1] = 0;
    }
}

static void hex64(const uint8_t d[32], char *out)
{
    static const char *h = "0123456789abcdef";
    for (int i = 0; i < 32; i++) {
        out[i * 2] = h[d[i] >> 4];
        out[i * 2 + 1] = h[d[i] & 0xf];
    }
    out[64] = 0;
}

static int hex_eq(const char *got, const char *want)
{
    if (want == NULL || strlen(want) < 64) {
        return 0;
    }
    for (int i = 0; i < 64; i++) {
        if (tolower((unsigned char)got[i]) != tolower((unsigned char)want[i])) {
            return 0;
        }
    }
    return 1;
}

static int parse3(const char *s, int *x, int *y, int *z)
{
    *x = *y = *z = 0;
    if (s == NULL || *s == 0) {
        return -1;
    }
    return sscanf(s, "%d.%d.%d", x, y, z) >= 1 ? 0 : -1;
}

static int semver_cmp(const char *a, const char *b)
{
    int a0, a1, a2, b0, b1, b2;
    if (parse3(a, &a0, &a1, &a2) != 0 || parse3(b, &b0, &b1, &b2) != 0) {
        return strcmp(a, b);
    }
    if (a0 != b0) {
        return a0 - b0;
    }
    if (a1 != b1) {
        return a1 - b1;
    }
    return a2 - b2;
}

static bool key_ok(httpd_req_t *req)
{
    char key[32];
    if (httpd_req_get_hdr_value_str(req, "X-Ribanense-Key", key, sizeof(key)) != ESP_OK) {
        return false;
    }
    return strcmp(key, RIBANENSEESP_OTA_KEY) == 0;
}

static esp_err_t write_stream(esp_ota_handle_t h, mbedtls_sha256_context *sha, const void *p, int n)
{
    mbedtls_sha256_update(sha, p, (size_t)n);
    return esp_ota_write(h, p, (size_t)n);
}

static esp_err_t finish_ota(esp_ota_handle_t h, const esp_partition_t *part, mbedtls_sha256_context *sha,
                            const char *want_sha)
{
    uint8_t dig[32];
    char hex[65];
    mbedtls_sha256_finish(sha, dig);
    hex64(dig, hex);
    if (want_sha != NULL && want_sha[0] != 0 && !hex_eq(hex, want_sha)) {
        ESP_LOGE(TAG, "sha256 %s != %s", hex, want_sha);
        (void)esp_ota_abort(h);
        return ESP_ERR_INVALID_CRC;
    }
    esp_err_t err = esp_ota_end(h);
    if (err != ESP_OK) {
        return err;
    }
    err = esp_ota_set_boot_partition(part);
    if (err == ESP_OK) {
        ESP_LOGI(TAG, "boot -> %s sha=%s", part->label, hex);
    }
    return err;
}

static esp_err_t on_status(httpd_req_t *req)
{
    char ip[NET_IP_MAX];
    char body[192];
    net_sta_ip(ip, sizeof(ip));
    snprintf(body, sizeof(body),
             "{\"product\":\"%s\",\"version\":\"%s\",\"ip\":\"%s\",\"ota\":\"%s\"}",
             RIBANENSEESP_PRODUCT, RIBANENSEESP_VERSION, ip, s_msg);
    httpd_resp_set_type(req, "application/json");
    return httpd_resp_send(req, body, HTTPD_RESP_USE_STRLEN);
}

static esp_err_t on_update(httpd_req_t *req)
{
    if (!key_ok(req)) {
        httpd_resp_set_status(req, "401 Unauthorized");
        httpd_resp_sendstr(req, "key");
        return ESP_OK;
    }
    if (req->content_len <= 0 || (size_t)req->content_len > SLOT_MAX) {
        httpd_resp_set_status(req, "400 Bad Request");
        httpd_resp_sendstr(req, "size");
        return ESP_OK;
    }

    const esp_partition_t *part = esp_ota_get_next_update_partition(NULL);
    if (part == NULL) {
        httpd_resp_set_status(req, "500 Internal Server Error");
        httpd_resp_sendstr(req, "part");
        return ESP_OK;
    }

    set_state(OTA_DOWNLOADING, "gravando...");
    esp_ota_handle_t h = 0;
    esp_err_t err = esp_ota_begin(part, req->content_len, &h);
    if (err != ESP_OK) {
        set_state(OTA_ERR, "falha OTA");
        httpd_resp_set_status(req, "500 Internal Server Error");
        httpd_resp_sendstr(req, "begin");
        return ESP_OK;
    }

    mbedtls_sha256_context sha;
    mbedtls_sha256_init(&sha);
    mbedtls_sha256_starts(&sha, 0);

    int left = req->content_len;
    while (left > 0) {
        int want = left > CHUNK ? CHUNK : left;
        int n = httpd_req_recv(req, (char *)s_chunk, want);
        if (n <= 0) {
            (void)esp_ota_abort(h);
            mbedtls_sha256_free(&sha);
            set_state(OTA_ERR, "falha no envio");
            httpd_resp_set_status(req, "500 Internal Server Error");
            httpd_resp_sendstr(req, "recv");
            return ESP_OK;
        }
        err = write_stream(h, &sha, s_chunk, n);
        if (err != ESP_OK) {
            (void)esp_ota_abort(h);
            mbedtls_sha256_free(&sha);
            set_state(OTA_ERR, "falha ao gravar");
            httpd_resp_set_status(req, "500 Internal Server Error");
            httpd_resp_sendstr(req, "write");
            return ESP_OK;
        }
        left -= n;
    }

    err = finish_ota(h, part, &sha, NULL);
    mbedtls_sha256_free(&sha);
    if (err != ESP_OK) {
        set_state(OTA_ERR, "falha OTA");
        httpd_resp_set_status(req, "500 Internal Server Error");
        httpd_resp_sendstr(req, "end");
        return ESP_OK;
    }

    set_state(OTA_OK_REBOOT, "reiniciando...");
    httpd_resp_sendstr(req, "ok");
    vTaskDelay(pdMS_TO_TICKS(300));
    esp_restart();
    return ESP_OK;
}

#define HTTP_UA "RibanenseESP"

typedef struct {
    char *buf;
    int cap;
    int acc;
} text_acc_t;

typedef struct {
    const esp_partition_t *part;
    esp_ota_handle_t h;
    mbedtls_sha256_context sha;
    int total;
    bool began;
    esp_err_t err;
} bin_acc_t;

static esp_err_t on_http_text(esp_http_client_event_t *e)
{
    if (e->event_id != HTTP_EVENT_ON_DATA || e->data == NULL || e->data_len <= 0) {
        return ESP_OK;
    }
    text_acc_t *a = e->user_data;
    int n = e->data_len;
    if (a->acc + n > a->cap - 1) {
        n = a->cap - 1 - a->acc;
    }
    if (n > 0) {
        memcpy(a->buf + a->acc, e->data, (size_t)n);
        a->acc += n;
        a->buf[a->acc] = 0;
    }
    return ESP_OK;
}

static esp_err_t on_http_bin(esp_http_client_event_t *e)
{
    bin_acc_t *a = e->user_data;
    if (e->event_id != HTTP_EVENT_ON_DATA || a->err != ESP_OK || e->data == NULL || e->data_len <= 0) {
        return ESP_OK;
    }
    if (!a->began) {
        int len = (int)esp_http_client_get_content_length(e->client);
        if (len > (int)SLOT_MAX) {
            a->err = ESP_ERR_INVALID_SIZE;
            return ESP_OK;
        }
        a->err = esp_ota_begin(a->part, len > 0 ? (size_t)len : OTA_WITH_SEQUENTIAL_WRITES, &a->h);
        if (a->err != ESP_OK) {
            return ESP_OK;
        }
        mbedtls_sha256_init(&a->sha);
        mbedtls_sha256_starts(&a->sha, 0);
        a->began = true;
        set_state(OTA_DOWNLOADING, "baixando...");
    }
    if ((size_t)(a->total + e->data_len) > SLOT_MAX) {
        a->err = ESP_ERR_INVALID_SIZE;
        return ESP_OK;
    }
    a->err = write_stream(a->h, &a->sha, e->data, e->data_len);
    if (a->err == ESP_OK) {
        a->total += e->data_len;
        if ((a->total & 0xffff) < e->data_len) {
            char m[24];
            snprintf(m, sizeof(m), "baixando %dk", a->total / 1024);
            set_state(OTA_DOWNLOADING, m);
        }
    }
    return ESP_OK;
}

static void http_fill(esp_http_client_config_t *c, const char *url, int timeout_ms,
                      http_event_handle_cb cb, void *user)
{
    memset(c, 0, sizeof(*c));
    c->url = url;
    c->timeout_ms = timeout_ms;
    c->crt_bundle_attach = esp_crt_bundle_attach;
    c->max_redirection_count = 8;
    c->user_agent = HTTP_UA;
    c->event_handler = cb;
    c->user_data = user;
    c->buffer_size = 1024;
}

static void set_http_err(int status, const char *fallback)
{
    if (status > 0 && status != 200) {
        char m[20];
        snprintf(m, sizeof(m), "http %d", status);
        set_state(OTA_ERR, m);
        return;
    }
    set_state(OTA_ERR, fallback);
}

static esp_err_t http_get_text(const char *url, char *out, int cap, int *out_n)
{
    out[0] = 0;
    *out_n = 0;
    text_acc_t acc = {.buf = out, .cap = cap, .acc = 0};
    esp_http_client_config_t c;
    http_fill(&c, url, 20000, on_http_text, &acc);
    esp_http_client_handle_t cli = esp_http_client_init(&c);
    if (cli == NULL) {
        return ESP_FAIL;
    }
    esp_err_t err = esp_http_client_perform(cli);
    int status = esp_http_client_get_status_code(cli);
    *out_n = acc.acc;
    esp_http_client_cleanup(cli);
    if (err != ESP_OK) {
        ESP_LOGE(TAG, "GET %s %s", url, esp_err_to_name(err));
        return err;
    }
    if (status != 200 || acc.acc <= 0) {
        ESP_LOGE(TAG, "GET %s status=%d n=%d", url, status, acc.acc);
        return ESP_FAIL;
    }
    return ESP_OK;
}

static esp_err_t http_stream_bin(const char *url, const char *want_sha)
{
    const esp_partition_t *part = esp_ota_get_next_update_partition(NULL);
    if (part == NULL) {
        return ESP_ERR_NOT_FOUND;
    }

    bin_acc_t acc = {
        .part = part,
        .err = ESP_OK,
    };
    esp_http_client_config_t c;
    http_fill(&c, url, 60000, on_http_bin, &acc);
    esp_http_client_handle_t cli = esp_http_client_init(&c);
    if (cli == NULL) {
        return ESP_FAIL;
    }
    set_state(OTA_DOWNLOADING, "baixando...");
    esp_err_t err = esp_http_client_perform(cli);
    int status = esp_http_client_get_status_code(cli);
    esp_http_client_cleanup(cli);
    if (err != ESP_OK || status != 200 || acc.err != ESP_OK || !acc.began) {
        ESP_LOGE(TAG, "BIN %s err=%s status=%d acc=%s began=%d", url, esp_err_to_name(err), status,
                 esp_err_to_name(acc.err), (int)acc.began);
        if (acc.began) {
            (void)esp_ota_abort(acc.h);
            mbedtls_sha256_free(&acc.sha);
        }
        if (status != 200) {
            set_http_err(status, "falha no download");
        } else if (acc.err != ESP_OK) {
            set_state(OTA_ERR, "falha ao gravar");
        } else {
            set_state(OTA_ERR, "falha no download");
        }
        return ESP_FAIL;
    }

    err = finish_ota(acc.h, part, &acc.sha, want_sha);
    mbedtls_sha256_free(&acc.sha);
    if (err == ESP_ERR_INVALID_CRC) {
        set_state(OTA_ERR, "sha256");
    } else if (err != ESP_OK) {
        set_state(OTA_ERR, "falha OTA");
    }
    return err;
}

static void pull_task(void *arg)
{
    (void)arg;
    char *json = malloc(2048);
    if (json == NULL) {
        set_state(OTA_ERR, "sem RAM");
        s_pull_busy = false;
        vTaskDelete(NULL);
        return;
    }

    set_state(OTA_CHECKING, "relogio...");
    (void)net_time_wait(8000);
    set_state(OTA_CHECKING, "buscando...");
    int n = 0;
    esp_err_t err = http_get_text(RIBANENSEESP_MANIFEST_URL, json, 2048, &n);
    if (err != ESP_OK) {
        free(json);
        set_state(OTA_ERR, "sem manifesto");
        s_pull_busy = false;
        vTaskDelete(NULL);
        return;
    }

    cJSON *root = cJSON_Parse(json);
    free(json);
    if (root == NULL) {
        set_state(OTA_ERR, "manifesto invalido");
        s_pull_busy = false;
        vTaskDelete(NULL);
        return;
    }

    const cJSON *product = cJSON_GetObjectItem(root, "product");
    const cJSON *ver = cJSON_GetObjectItem(root, "version");
    const cJSON *url = cJSON_GetObjectItem(root, "url");
    const cJSON *sha = cJSON_GetObjectItem(root, "sha256");
    const char *pv = cJSON_IsString(product) ? product->valuestring : "";
    const char *vv = cJSON_IsString(ver) ? ver->valuestring : "";
    const char *uv = cJSON_IsString(url) ? url->valuestring : "";
    const char *sv = cJSON_IsString(sha) ? sha->valuestring : "";

    if (strcmp(pv, RIBANENSEESP_PRODUCT) != 0) {
        cJSON_Delete(root);
        set_state(OTA_ERR, "produto diferente");
        s_pull_busy = false;
        vTaskDelete(NULL);
        return;
    }
    if (semver_cmp(vv, RIBANENSEESP_VERSION) <= 0) {
        cJSON_Delete(root);
        set_state(OTA_IDLE, "atual");
        s_pull_busy = false;
        vTaskDelete(NULL);
        return;
    }
    if (uv[0] == 0) {
        cJSON_Delete(root);
        set_state(OTA_ERR, "sem binario");
        s_pull_busy = false;
        vTaskDelete(NULL);
        return;
    }

    char url_copy[512];
    char sha_copy[72];
    strncpy(url_copy, uv, sizeof(url_copy) - 1);
    url_copy[sizeof(url_copy) - 1] = 0;
    strncpy(sha_copy, sv, sizeof(sha_copy) - 1);
    sha_copy[sizeof(sha_copy) - 1] = 0;
    cJSON_Delete(root);

    err = http_stream_bin(url_copy, sha_copy);
    if (err != ESP_OK) {
        if (s_state != OTA_ERR) {
            set_state(OTA_ERR, "falha no download");
        }
        s_pull_busy = false;
        vTaskDelete(NULL);
        return;
    }

    set_state(OTA_OK_REBOOT, "reiniciando...");
    s_pull_busy = false;
    vTaskDelay(pdMS_TO_TICKS(400));
    esp_restart();
    vTaskDelete(NULL);
}

esp_err_t ota_apply_file(const char *abs_path)
{
    if (abs_path == NULL || abs_path[0] == 0) {
        return ESP_ERR_INVALID_ARG;
    }

    FILE *f = fopen(abs_path, "rb");
    if (f == NULL) {
        return ESP_ERR_NOT_FOUND;
    }
    if (fseek(f, 0, SEEK_END) != 0) {
        fclose(f);
        return ESP_FAIL;
    }
    long sz = ftell(f);
    rewind(f);
    if (sz <= 0 || (size_t)sz > SLOT_MAX) {
        fclose(f);
        return ESP_ERR_INVALID_SIZE;
    }

    const esp_partition_t *part = esp_ota_get_next_update_partition(NULL);
    if (part == NULL) {
        fclose(f);
        return ESP_ERR_NOT_FOUND;
    }

    set_state(OTA_DOWNLOADING, "gravando app...");
    esp_ota_handle_t h = 0;
    esp_err_t err = esp_ota_begin(part, (size_t)sz, &h);
    if (err != ESP_OK) {
        fclose(f);
        set_state(OTA_ERR, "falha OTA");
        return err;
    }

    size_t left = (size_t)sz;
    while (left > 0) {
        size_t want = left > CHUNK ? CHUNK : left;
        size_t n = fread(s_chunk, 1, want, f);
        if (n == 0) {
            (void)esp_ota_abort(h);
            fclose(f);
            set_state(OTA_ERR, "falha ao ler");
            return ESP_FAIL;
        }
        err = esp_ota_write(h, s_chunk, n);
        if (err != ESP_OK) {
            (void)esp_ota_abort(h);
            fclose(f);
            set_state(OTA_ERR, "falha ao gravar");
            return err;
        }
        left -= n;
    }
    fclose(f);

    err = esp_ota_end(h);
    if (err != ESP_OK) {
        set_state(OTA_ERR, "falha OTA");
        return err;
    }
    err = esp_ota_set_boot_partition(part);
    if (err != ESP_OK) {
        set_state(OTA_ERR, "falha OTA");
        return err;
    }
    set_state(OTA_OK_REBOOT, "abrindo...");
    ESP_LOGI(TAG, "app -> %s (%ld bytes)", part->label, sz);
    return ESP_OK;
}

esp_err_t ota_init(void)
{
    (void)esp_ota_mark_app_valid_cancel_rollback();
    set_state(OTA_IDLE, "Atualizar");
    ESP_LOGI(TAG, "OTA pronto (GET /status POST /update)");
    return ESP_OK;
}

esp_err_t ota_start_httpd(void)
{
    if (s_httpd != NULL) {
        return ESP_OK;
    }
    httpd_config_t cfg = HTTPD_DEFAULT_CONFIG();
    cfg.server_port = 80;
    cfg.lru_purge_enable = true;
    cfg.recv_wait_timeout = 30;
    cfg.send_wait_timeout = 30;
    cfg.stack_size = 8192;
    esp_err_t err = httpd_start(&s_httpd, &cfg);
    if (err != ESP_OK) {
        ESP_LOGE(TAG, "httpd %s", esp_err_to_name(err));
        return err;
    }
    const httpd_uri_t status = {
        .uri = "/status",
        .method = HTTP_GET,
        .handler = on_status,
    };
    const httpd_uri_t update = {
        .uri = "/update",
        .method = HTTP_POST,
        .handler = on_update,
    };
    httpd_register_uri_handler(s_httpd, &status);
    httpd_register_uri_handler(s_httpd, &update);
    ESP_LOGI(TAG, "httpd :80 /status /update");
    return ESP_OK;
}

void ota_pull_start(void)
{
    if (s_pull_busy) {
        if (s_msg[0] == 0) {
            set_state(s_state, "aguarde...");
        }
        return;
    }
    s_pull_busy = true;
    set_state(OTA_CHECKING, "buscando...");
    if (xTaskCreate(pull_task, "ota_pull", 20480, NULL, 4, NULL) != pdPASS) {
        s_pull_busy = false;
        set_state(OTA_ERR, "sem tarefa");
    }
}

ota_state_t ota_state(void)
{
    return s_state;
}

const char *ota_message(void)
{
    return s_msg;
}
