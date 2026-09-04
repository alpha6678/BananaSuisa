#include "storage.h"
#include "board_pins.h"

#include "driver/sdspi_host.h"
#include "driver/spi_master.h"
#include "esp_log.h"
#include "esp_vfs_fat.h"
#include "sdmmc_cmd.h"

#include <stdio.h>
#include <string.h>

static const char *TAG = "storage";
static bool s_ready;
static sdmmc_card_t *s_card;

bool storage_mount(void)
{
    spi_bus_config_t bus = {
        .sclk_io_num = BOARD_SD_SCK,
        .mosi_io_num = BOARD_SD_MOSI,
        .miso_io_num = BOARD_SD_MISO,
        .quadwp_io_num = -1,
        .quadhd_io_num = -1,
        .max_transfer_sz = 4000,
    };
    esp_err_t err = spi_bus_initialize(SPI2_HOST, &bus, SPI_DMA_CH_AUTO);
    if (err != ESP_OK && err != ESP_ERR_INVALID_STATE) {
        ESP_LOGW(TAG, "spi2: %s", esp_err_to_name(err));
        return false;
    }

    sdmmc_host_t host = SDSPI_HOST_DEFAULT();
    host.slot = SPI2_HOST;
    host.max_freq_khz = 10000;

    sdspi_device_config_t slot = SDSPI_DEVICE_CONFIG_DEFAULT();
    slot.gpio_cs = BOARD_SD_CS;
    slot.host_id = SPI2_HOST;

    esp_vfs_fat_sdmmc_mount_config_t mount = {
        .format_if_mount_failed = false,
        .max_files = 4,
        .allocation_unit_size = 16 * 1024,
    };

    err = esp_vfs_fat_sdspi_mount(STORAGE_MOUNT, &host, &slot, &mount, &s_card);
    if (err != ESP_OK) {
        ESP_LOGW(TAG, "SD ausente ou FAT invalida: %s", esp_err_to_name(err));
        s_ready = false;
        return false;
    }
    s_ready = true;
    ESP_LOGI(TAG, "SD montado em %s", STORAGE_MOUNT);
    return true;
}

bool storage_ready(void)
{
    return s_ready;
}

esp_err_t storage_write_text(const char *rel_path, const char *text)
{
    if (!s_ready || rel_path == NULL || text == NULL) {
        return ESP_ERR_INVALID_STATE;
    }
    char path[160];
    snprintf(path, sizeof(path), "%s/%s", STORAGE_MOUNT, rel_path);
    FILE *f = fopen(path, "w");
    if (f == NULL) {
        return ESP_FAIL;
    }
    size_t n = fwrite(text, 1, strlen(text), f);
    fflush(f);
    fclose(f);
    return n == strlen(text) ? ESP_OK : ESP_FAIL;
}
