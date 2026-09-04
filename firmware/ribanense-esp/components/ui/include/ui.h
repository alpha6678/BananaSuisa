#pragma once

#include <stdbool.h>
#include "esp_err.h"

esp_err_t ui_init(bool sd_ok);
void ui_tick(void);
