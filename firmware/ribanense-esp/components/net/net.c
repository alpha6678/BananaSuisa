#include "net.h"

#include <string.h>

#include "esp_event.h"
#include "esp_log.h"
#include "esp_netif.h"
#include "esp_wifi.h"
#include "freertos/FreeRTOS.h"
#include "freertos/semphr.h"

static const char *TAG = "net";

static bool s_ready;
static net_scan_state_t s_state = NET_SCAN_IDLE;
static net_ap_t s_aps[NET_AP_MAX];
static wifi_ap_record_t s_rec[NET_AP_MAX];
static int s_count;
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
    (void)data;
    if (id != WIFI_EVENT_SCAN_DONE) {
        return;
    }

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
        s_state = NET_SCAN_OK;
        ESP_LOGI(TAG, "scan %d redes", s_count);
    } else {
        s_count = 0;
        s_state = NET_SCAN_ERR;
        ESP_LOGE(TAG, "scan falhou (%s)", esp_err_to_name(err));
    }
    unlock();
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
    err = esp_event_handler_instance_register(WIFI_EVENT, WIFI_EVENT_SCAN_DONE, on_wifi, NULL, NULL);
    if (err != ESP_OK) {
        return err;
    }
    err = esp_wifi_set_storage(WIFI_STORAGE_RAM);
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
    ESP_LOGI(TAG, "STA pronto para scan");
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
    if (s_state == NET_SCAN_BUSY) {
        unlock();
        return ESP_ERR_INVALID_STATE;
    }
    s_state = NET_SCAN_BUSY;
    s_count = 0;
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
        s_state = NET_SCAN_ERR;
        unlock();
        ESP_LOGE(TAG, "scan_start %s", esp_err_to_name(err));
    }
    return err;
}

net_scan_state_t net_scan_state(void)
{
    lock();
    net_scan_state_t st = s_state;
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
