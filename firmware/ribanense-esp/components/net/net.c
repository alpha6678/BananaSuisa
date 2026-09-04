#include "net.h"
#include "esp_log.h"

static const char *TAG = "net";

esp_err_t net_init(void)
{
    ESP_LOGI(TAG, "Wi-Fi na F1 (SoftAP + STA)");
    return ESP_OK;
}
