#include "board.h"
#include "nvs_flash.h"
#include "ribanense_esp_version.h"
#include "storage.h"
#include "ui.h"

#include "esp_log.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"

static const char *TAG = "ribanenseesp";

void app_main(void)
{
    ESP_LOGI(TAG, "%s %s", RIBANENSEESP_PRODUCT, RIBANENSEESP_VERSION);

    esp_err_t err = nvs_flash_init();
    if (err == ESP_ERR_NVS_NO_FREE_PAGES || err == ESP_ERR_NVS_NEW_VERSION_FOUND) {
        ESP_ERROR_CHECK(nvs_flash_erase());
        err = nvs_flash_init();
    }
    ESP_ERROR_CHECK(err);

    ESP_ERROR_CHECK(board_init());
    bool sd_ok = storage_mount();
    ESP_ERROR_CHECK(ui_init(sd_ok));

    while (1) {
        ui_tick();
        vTaskDelay(pdMS_TO_TICKS(5));
    }
}
