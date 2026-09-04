#include "net.h"

#include <stdio.h>
#include <string.h>
#include <time.h>

#include "esp_event.h"
#include "esp_log.h"
#include "esp_netif.h"
#include "esp_netif_sntp.h"
#include "esp_wifi.h"
#include "freertos/FreeRTOS.h"
#include "freertos/semphr.h"

static const char *TAG = "net";

static bool s_ready;
static net_scan_state_t s_scan = NET_SCAN_IDLE;
static net_sta_state_t s_sta = NET_STA_IDLE;
static net_ap_t s_aps[NET_AP_MAX];
static wifi_ap_record_t s_rec[NET_AP_MAX];
static int s_count;
static char s_ip[NET_IP_MAX];
static uint16_t s_fail_reason;
static SemaphoreHandle_t s_lock;

static void lock(void)
{
    if (s_lock) {
        xSemaphoreTake(s_lock, portMAX_DELAY);
    }
}

static void unlock(void)
{
    if (s_lock) {
        xSemaphoreGive(s_lock);
    }
}

static void store_records(const wifi_ap_record_t *rec, uint16_t n)
{
    s_count = 0;
    for (uint16_t i = 0; i < n; i++) {
        if (rec[i].ssid[0] == 0) {
            continue;
        }
        int found = -1;
        for (int j = 0; j < s_count; j++) {
            if (strcmp(s_aps[j].ssid, (const char *)rec[i].ssid) == 0) {
                found = j;
                break;
            }
        }
        if (found >= 0) {
            if (rec[i].rssi > s_aps[found].rssi) {
                s_aps[found].rssi = rec[i].rssi;
                s_aps[found].auth = (uint8_t)rec[i].authmode;
            }
            continue;
        }
        if (s_count >= NET_AP_MAX) {
            int weakest = 0;
            for (int j = 1; j < s_count; j++) {
                if (s_aps[j].rssi < s_aps[weakest].rssi) {
                    weakest = j;
                }
            }
            if (rec[i].rssi <= s_aps[weakest].rssi) {
                continue;
            }
            found = weakest;
        } else {
            found = s_count++;
        }
        strncpy(s_aps[found].ssid, (const char *)rec[i].ssid, NET_SSID_MAX - 1);
        s_aps[found].ssid[NET_SSID_MAX - 1] = 0;
        s_aps[found].rssi = rec[i].rssi;
        s_aps[found].auth = (uint8_t)rec[i].authmode;
    }

    for (int i = 1; i < s_count; i++) {
        net_ap_t key = s_aps[i];
        int j = i - 1;
        while (j >= 0 && s_aps[j].rssi < key.rssi) {
            s_aps[j + 1] = s_aps[j];
            j--;
        }
        s_aps[j + 1] = key;
    }
}

static void on_wifi(void *arg, esp_event_base_t base, int32_t id, void *data)
{
    (void)arg;
    (void)base;

    if (id == WIFI_EVENT_STA_START) {
        wifi_config_t cfg;
        if (esp_wifi_get_config(WIFI_IF_STA, &cfg) == ESP_OK && cfg.sta.ssid[0] != 0) {
            lock();
            if (s_sta == NET_STA_IDLE) {
                s_sta = NET_STA_CONNECTING;
            }
            unlock();
            ESP_LOGI(TAG, "reconectando a %s", (const char *)cfg.sta.ssid);
            (void)esp_wifi_connect();
        }
        return;
    }

    if (id == WIFI_EVENT_SCAN_DONE) {
        uint16_t n = 0;
        esp_err_t err = esp_wifi_scan_get_ap_num(&n);
        if (err == ESP_OK && n > NET_AP_MAX) {
            n = NET_AP_MAX;
        }
        if (err == ESP_OK) {
            err = esp_wifi_scan_get_ap_records(&n, s_rec);
        }
        lock();
        if (err == ESP_OK) {
            store_records(s_rec, n);
            s_scan = NET_SCAN_OK;
        } else {
            s_scan = NET_SCAN_ERR;
            ESP_LOGE(TAG, "scan falhou (%s)", esp_err_to_name(err));
        }
        unlock();
        return;
    }

    if (id == WIFI_EVENT_STA_DISCONNECTED) {
        const wifi_event_sta_disconnected_t *ev = data;
        lock();
        if (s_sta == NET_STA_CONNECTING || s_sta == NET_STA_GOT_IP) {
            s_sta = NET_STA_FAIL;
            s_fail_reason = ev ? ev->reason : 0;
            s_ip[0] = 0;
            ESP_LOGW(TAG, "STA caiu reason=%u", (unsigned)s_fail_reason);
        }
        unlock();
    }
}

static void on_ip(void *arg, esp_event_base_t base, int32_t id, void *data)
{
    (void)arg;
    (void)base;
    if (id != IP_EVENT_STA_GOT_IP) {
        return;
    }
    const ip_event_got_ip_t *ev = data;
    lock();
    snprintf(s_ip, sizeof(s_ip), IPSTR, IP2STR(&ev->ip_info.ip));
    s_sta = NET_STA_GOT_IP;
    s_fail_reason = 0;
    unlock();
    ESP_LOGI(TAG, "STA IP %s", s_ip);
}

esp_err_t net_init(void)
{
    if (s_ready) {
        return ESP_OK;
    }

    s_lock = xSemaphoreCreateMutex();
    if (s_lock == NULL) {
        return ESP_ERR_NO_MEM;
    }

    esp_err_t err = esp_netif_init();
    if (err != ESP_OK) {
        return err;
    }
    err = esp_event_loop_create_default();
    if (err != ESP_OK && err != ESP_ERR_INVALID_STATE) {
        return err;
    }
    if (esp_netif_create_default_wifi_sta() == NULL) {
        return ESP_FAIL;
    }

    wifi_init_config_t cfg = WIFI_INIT_CONFIG_DEFAULT();
    err = esp_wifi_init(&cfg);
    if (err != ESP_OK) {
        return err;
    }
    err = esp_event_handler_instance_register(WIFI_EVENT, ESP_EVENT_ANY_ID, on_wifi, NULL, NULL);
    if (err != ESP_OK) {
        return err;
    }
    err = esp_event_handler_instance_register(IP_EVENT, IP_EVENT_STA_GOT_IP, on_ip, NULL, NULL);
    if (err != ESP_OK) {
        return err;
    }
    esp_sntp_config_t sntp = ESP_NETIF_SNTP_DEFAULT_CONFIG("pool.ntp.org");
    sntp.wait_for_sync = true;
    err = esp_netif_sntp_init(&sntp);
    if (err != ESP_OK) {
        ESP_LOGW(TAG, "SNTP %s", esp_err_to_name(err));
    }
    err = esp_wifi_set_storage(WIFI_STORAGE_FLASH);
    if (err != ESP_OK) {
        return err;
    }
    err = esp_wifi_set_mode(WIFI_MODE_STA);
    if (err != ESP_OK) {
        return err;
    }
    err = esp_wifi_start();
    if (err != ESP_OK) {
        return err;
    }

    wifi_country_t br = {
        .cc = "BR",
        .schan = 1,
        .nchan = 13,
        .policy = WIFI_COUNTRY_POLICY_AUTO,
    };
    (void)esp_wifi_set_country(&br);

    s_ready = true;
    ESP_LOGI(TAG, "STA pronto");
    return ESP_OK;
}

bool net_ready(void)
{
    return s_ready;
}

esp_err_t net_scan_start(void)
{
    if (!s_ready) {
        return ESP_ERR_INVALID_STATE;
    }

    lock();
    if (s_scan == NET_SCAN_BUSY) {
        unlock();
        return ESP_ERR_INVALID_STATE;
    }
    s_scan = NET_SCAN_BUSY;
    unlock();

    const wifi_scan_config_t cfg = {
        .ssid = NULL,
        .bssid = NULL,
        .channel = 0,
        .show_hidden = false,
        .scan_type = WIFI_SCAN_TYPE_ACTIVE,
    };
    esp_err_t err = esp_wifi_scan_start(&cfg, false);
    if (err != ESP_OK) {
        lock();
        s_scan = NET_SCAN_ERR;
        unlock();
        ESP_LOGE(TAG, "scan_start %s", esp_err_to_name(err));
    }
    return err;
}

esp_err_t net_scan_stop(void)
{
    if (!s_ready) {
        return ESP_ERR_INVALID_STATE;
    }
    (void)esp_wifi_scan_stop();
    lock();
    if (s_scan == NET_SCAN_BUSY) {
        s_scan = (s_count > 0) ? NET_SCAN_OK : NET_SCAN_IDLE;
    }
    unlock();
    return ESP_OK;
}

net_scan_state_t net_scan_state(void)
{
    lock();
    net_scan_state_t st = s_scan;
    unlock();
    return st;
}

int net_scan_copy(net_ap_t *out, int max)
{
    if (out == NULL || max <= 0) {
        return 0;
    }
    lock();
    int n = s_count;
    if (n > max) {
        n = max;
    }
    memcpy(out, s_aps, (size_t)n * sizeof(net_ap_t));
    unlock();
    return n;
}

esp_err_t net_sta_connect(const char *ssid, const char *pass)
{
    if (!s_ready || ssid == NULL || ssid[0] == 0) {
        return ESP_ERR_INVALID_ARG;
    }

    (void)net_scan_stop();

    wifi_config_t cfg = {0};
    strncpy((char *)cfg.sta.ssid, ssid, sizeof(cfg.sta.ssid) - 1);
    if (pass != NULL) {
        strncpy((char *)cfg.sta.password, pass, sizeof(cfg.sta.password) - 1);
    }
    cfg.sta.threshold.authmode = WIFI_AUTH_OPEN;
    cfg.sta.pmf_cfg.capable = true;
    cfg.sta.pmf_cfg.required = false;

    lock();
    s_sta = NET_STA_CONNECTING;
    s_fail_reason = 0;
    s_ip[0] = 0;
    unlock();

    esp_err_t err = esp_wifi_set_config(WIFI_IF_STA, &cfg);
    if (err != ESP_OK) {
        lock();
        s_sta = NET_STA_FAIL;
        unlock();
        return err;
    }
    (void)esp_wifi_disconnect();
    err = esp_wifi_connect();
    if (err != ESP_OK) {
        lock();
        s_sta = NET_STA_FAIL;
        unlock();
    }
    ESP_LOGI(TAG, "conectando a %s", ssid);
    return err;
}

net_sta_state_t net_sta_state(void)
{
    lock();
    net_sta_state_t st = s_sta;
    unlock();
    return st;
}

void net_sta_ip(char *out, size_t max)
{
    if (out == NULL || max == 0) {
        return;
    }
    lock();
    strncpy(out, s_ip, max - 1);
    out[max - 1] = 0;
    unlock();
}

uint16_t net_sta_fail_reason(void)
{
    lock();
    uint16_t r = s_fail_reason;
    unlock();
    return r;
}

esp_err_t net_time_wait(int timeout_ms)
{
    time_t now = 0;
    time(&now);
    if (now > 1700000000) {
        return ESP_OK;
    }
    if (timeout_ms < 0) {
        timeout_ms = 0;
    }
    return esp_netif_sntp_sync_wait(pdMS_TO_TICKS(timeout_ms));
}
