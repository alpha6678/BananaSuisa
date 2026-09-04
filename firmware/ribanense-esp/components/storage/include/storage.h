#pragma once

#include <stdbool.h>
#include "esp_err.h"

#define STORAGE_MOUNT "/sdcard"

/* Monta FAT32 no microSD (SPI2). false se o cartão não estiver presente. */
bool storage_mount(void);
bool storage_ready(void);
esp_err_t storage_write_text(const char *rel_path, const char *text);
