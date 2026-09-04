#include "ota.h"
#include "esp_log.h"

static const char *TAG = "ota";

esp_err_t ota_init(void)
{
    ESP_LOGI(TAG, "OTA na F3 (firmware.json + /update)");
    return ESP_OK;
}
