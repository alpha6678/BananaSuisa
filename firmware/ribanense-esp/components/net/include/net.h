#pragma once

#include <stdbool.h>
#include <stdint.h>
#include "esp_err.h"

#define NET_SSID_MAX 33
#define NET_AP_MAX   16

typedef struct {
    char ssid[NET_SSID_MAX];
    int8_t rssi;
} net_ap_t;

typedef enum {
    NET_SCAN_IDLE = 0,
    NET_SCAN_BUSY,
    NET_SCAN_OK,
    NET_SCAN_ERR,
} net_scan_state_t;

esp_err_t net_init(void);
bool net_ready(void);

/* Scan ativo, sem bloquear a UI. */
esp_err_t net_scan_start(void);
net_scan_state_t net_scan_state(void);
int net_scan_copy(net_ap_t *out, int max);
