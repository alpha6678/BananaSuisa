#include "ui.h"
#include "board.h"
#include "board_pins.h"
#include "net.h"
#include "ribanense_esp_version.h"
#include "ui_palette.h"

#include <stdio.h>

#include "esp_err.h"
#include "esp_heap_caps.h"
#include "esp_lcd_panel_io.h"
#include "esp_log.h"
#include "esp_timer.h"
#include "lvgl.h"

#define BUF_LINES 20
#define ROW_H     48

static const char *TAG = "ui";
static lv_display_t *s_disp;
static lv_obj_t *s_home;
static lv_obj_t *s_wifi;
static lv_obj_t *s_wifi_status;
static lv_obj_t *s_wifi_list;
static bool s_wifi_pending;

static void show_wifi(void);
static void show_home(void);
static void wifi_poll(void);

static void lvgl_tick(void *arg)
{
    (void)arg;
    lv_tick_inc(5);
}

static bool on_lcd_flush_done(esp_lcd_panel_io_handle_t io, esp_lcd_panel_io_event_data_t *edata,
                             void *ctx)
{
    (void)io;
    (void)edata;
    lv_display_flush_ready((lv_display_t *)ctx);
    return false;
}

static void flush_cb(lv_display_t *disp, const lv_area_t *area, uint8_t *px)
{
    (void)disp;
    const int32_t w = lv_area_get_width(area);
    const int32_t h = lv_area_get_height(area);
    lv_draw_sw_rgb565_swap(px, (uint32_t)(w * h));
    esp_lcd_panel_draw_bitmap(board_lcd(), area->x1, area->y1, area->x2 + 1, area->y2 + 1, px);
}

static void touch_cb(lv_indev_t *indev, lv_indev_data_t *data)
{
    (void)indev;
    int16_t x = 0;
    int16_t y = 0;
    if (board_touch_read(&x, &y)) {
        data->state = LV_INDEV_STATE_PRESSED;
        data->point.x = x;
        data->point.y = y;
    } else {
        data->state = LV_INDEV_STATE_RELEASED;
    }
}

static void style_screen(lv_obj_t *scr)
{
    lv_obj_set_style_bg_color(scr, ui_color_black(), 0);
    lv_obj_set_style_bg_opa(scr, LV_OPA_COVER, 0);
    lv_obj_set_style_text_color(scr, ui_color_white(), 0);
    lv_obj_set_flex_flow(scr, LV_FLEX_FLOW_COLUMN);
    lv_obj_set_style_pad_left(scr, 4, 0);
    lv_obj_set_style_pad_right(scr, 4, 0);
    lv_obj_set_style_pad_top(scr, 4, 0);
    lv_obj_set_style_pad_bottom(scr, 4, 0);
    lv_obj_set_style_pad_row(scr, 4, 0);
}

static void style_row(lv_obj_t *obj)
{
    lv_obj_remove_style_all(obj);
    lv_obj_set_width(obj, lv_pct(100));
    lv_obj_set_height(obj, ROW_H);
    lv_obj_set_style_bg_color(obj, ui_color_black(), 0);
    lv_obj_set_style_bg_opa(obj, LV_OPA_COVER, 0);
    lv_obj_set_style_border_color(obj, ui_color_white(), 0);
    lv_obj_set_style_border_width(obj, 1, 0);
    lv_obj_set_style_radius(obj, 0, 0);
    lv_obj_set_style_pad_left(obj, 6, 0);
    lv_obj_set_style_pad_right(obj, 6, 0);
    lv_obj_set_style_shadow_width(obj, 0, 0);
    lv_obj_set_style_outline_width(obj, 0, 0);
    lv_obj_set_style_transform_width(obj, 0, 0);
    lv_obj_set_style_transform_height(obj, 0, 0);
    lv_obj_set_style_border_color(obj, ui_color_yellow(), LV_STATE_PRESSED);
    lv_obj_set_style_border_width(obj, 2, LV_STATE_PRESSED);
}

static lv_obj_t *make_scroll_list(lv_obj_t *parent)
{
    lv_obj_t *list = lv_obj_create(parent);
    lv_obj_remove_style_all(list);
    lv_obj_set_width(list, lv_pct(100));
    lv_obj_set_flex_grow(list, 1);
    lv_obj_set_flex_flow(list, LV_FLEX_FLOW_COLUMN);
    lv_obj_set_style_pad_row(list, 4, 0);
    lv_obj_set_scroll_dir(list, LV_DIR_VER);
    lv_obj_set_scrollbar_mode(list, LV_SCROLLBAR_MODE_AUTO);
    lv_obj_set_style_bg_color(list, ui_color_white(), LV_PART_SCROLLBAR);
    lv_obj_set_style_bg_opa(list, LV_OPA_COVER, LV_PART_SCROLLBAR);
    lv_obj_set_style_width(list, 2, LV_PART_SCROLLBAR);
    lv_obj_remove_flag(list, LV_OBJ_FLAG_SCROLL_ELASTIC);
    lv_obj_remove_flag(list, LV_OBJ_FLAG_SCROLL_MOMENTUM);
    return list;
}

static void set_wifi_status(const char *msg, lv_color_t color)
{
    if (s_wifi_status == NULL) {
        return;
    }
    lv_label_set_text(s_wifi_status, msg);
    lv_obj_set_style_text_color(s_wifi_status, color, 0);
}

static lv_color_t rssi_color(int8_t rssi)
{
    if (rssi >= -60) {
        return ui_color_green();
    }
    if (rssi >= -75) {
        return ui_color_white();
    }
    return ui_color_red();
}

static void fill_ap_list(void)
{
    if (s_wifi_list == NULL) {
        return;
    }
    lv_obj_clean(s_wifi_list);

    net_ap_t aps[NET_AP_MAX];
    const int n = net_scan_copy(aps, NET_AP_MAX);
    if (n <= 0) {
        set_wifi_status("nenhuma rede", ui_color_white());
        return;
    }

    char summary[24];
    snprintf(summary, sizeof(summary), "%d redes", n);
    set_wifi_status(summary, ui_color_green());

    for (int i = 0; i < n; i++) {
        lv_obj_t *row = lv_obj_create(s_wifi_list);
        style_row(row);
        lv_obj_remove_flag(row, LV_OBJ_FLAG_CLICKABLE);
        lv_obj_set_flex_flow(row, LV_FLEX_FLOW_ROW);
        lv_obj_set_flex_align(row, LV_FLEX_ALIGN_SPACE_BETWEEN, LV_FLEX_ALIGN_CENTER,
                              LV_FLEX_ALIGN_CENTER);
        lv_obj_set_style_pad_column(row, 6, 0);

        lv_obj_t *ssid = lv_label_create(row);
        lv_label_set_text(ssid, aps[i].ssid);
        lv_label_set_long_mode(ssid, LV_LABEL_LONG_CLIP);
        lv_obj_set_flex_grow(ssid, 1);
        lv_obj_set_style_text_color(ssid, ui_color_white(), 0);

        char dbm[12];
        snprintf(dbm, sizeof(dbm), "%d dBm", (int)aps[i].rssi);
        lv_obj_t *sig = lv_label_create(row);
        lv_label_set_text(sig, dbm);
        lv_obj_set_style_text_color(sig, rssi_color(aps[i].rssi), 0);
    }
}

static void wifi_start_scan(void)
{
    if (s_wifi_list) {
        lv_obj_clean(s_wifi_list);
    }
    if (!net_ready()) {
        set_wifi_status("Wi-Fi ausente", ui_color_red());
        s_wifi_pending = false;
        return;
    }
    set_wifi_status("buscando...", ui_color_white());
    esp_err_t err = net_scan_start();
    if (err == ESP_ERR_INVALID_STATE && net_scan_state() == NET_SCAN_BUSY) {
        s_wifi_pending = true;
        return;
    }
    if (err != ESP_OK) {
        set_wifi_status("falha no scan", ui_color_red());
        s_wifi_pending = false;
        return;
    }
    s_wifi_pending = true;
}

static void wifi_poll(void)
{
    if (!s_wifi_pending) {
        return;
    }
    const net_scan_state_t st = net_scan_state();
    if (st == NET_SCAN_BUSY) {
        return;
    }
    s_wifi_pending = false;
    if (st == NET_SCAN_OK) {
        fill_ap_list();
    } else {
        set_wifi_status("falha no scan", ui_color_red());
    }
}

static void on_wifi_back(lv_event_t *e)
{
    (void)e;
    show_home();
}

static void on_wifi_refresh(lv_event_t *e)
{
    (void)e;
    wifi_start_scan();
}

static void build_wifi(void)
{
    s_wifi = lv_obj_create(NULL);
    style_screen(s_wifi);

    lv_obj_t *bar = lv_obj_create(s_wifi);
    lv_obj_remove_style_all(bar);
    lv_obj_set_width(bar, lv_pct(100));
    lv_obj_set_height(bar, 36);
    lv_obj_set_flex_flow(bar, LV_FLEX_FLOW_ROW);
    lv_obj_set_flex_align(bar, LV_FLEX_ALIGN_SPACE_BETWEEN, LV_FLEX_ALIGN_CENTER,
                          LV_FLEX_ALIGN_CENTER);

    lv_obj_t *back = lv_button_create(bar);
    style_row(back);
    lv_obj_set_width(back, 72);
    lv_obj_set_height(back, 32);
    lv_obj_add_event_cb(back, on_wifi_back, LV_EVENT_CLICKED, NULL);
    lv_obj_t *bl = lv_label_create(back);
    lv_label_set_text(bl, LV_SYMBOL_LEFT " voltar");
    lv_obj_set_style_text_color(bl, ui_color_white(), 0);
    lv_obj_center(bl);

    lv_obj_t *title = lv_label_create(bar);
    lv_label_set_text(title, "Wi-Fi");
    lv_obj_set_style_text_color(title, ui_color_blue(), 0);

    lv_obj_t *refresh = lv_button_create(bar);
    style_row(refresh);
    lv_obj_set_width(refresh, 40);
    lv_obj_set_height(refresh, 32);
    lv_obj_add_event_cb(refresh, on_wifi_refresh, LV_EVENT_CLICKED, NULL);
    lv_obj_t *rl = lv_label_create(refresh);
    lv_label_set_text(rl, LV_SYMBOL_REFRESH);
    lv_obj_set_style_text_color(rl, ui_color_white(), 0);
    lv_obj_center(rl);

    s_wifi_status = lv_label_create(s_wifi);
    lv_label_set_text(s_wifi_status, "");
    lv_obj_set_style_text_color(s_wifi_status, ui_color_white(), 0);

    s_wifi_list = make_scroll_list(s_wifi);
}

static void show_wifi(void)
{
    ESP_LOGI(TAG, "tela Wi-Fi");
    if (s_wifi == NULL) {
        build_wifi();
    }
    lv_screen_load(s_wifi);
    wifi_start_scan();
}

static void show_home(void)
{
    s_wifi_pending = false;
    lv_screen_load(s_home);
    if (s_wifi) {
        lv_obj_delete(s_wifi);
        s_wifi = NULL;
        s_wifi_status = NULL;
        s_wifi_list = NULL;
    }
}

static void on_open_wifi(lv_event_t *e)
{
    (void)e;
    show_wifi();
}

static void build_home(void)
{
    s_home = lv_screen_active();
    style_screen(s_home);

    lv_obj_t *title = lv_label_create(s_home);
    lv_label_set_text(title, RIBANENSEESP_PRODUCT);
    lv_obj_set_style_text_color(title, ui_color_blue(), 0);

    lv_obj_t *ver = lv_label_create(s_home);
    lv_label_set_text(ver, RIBANENSEESP_VERSION);

    lv_obj_t *list = make_scroll_list(s_home);

    lv_obj_t *wifi = lv_button_create(list);
    style_row(wifi);
    lv_obj_add_event_cb(wifi, on_open_wifi, LV_EVENT_CLICKED, NULL);
    lv_obj_t *wl = lv_label_create(wifi);
    lv_label_set_text(wl, LV_SYMBOL_WIFI "  Wi-Fi");
    lv_obj_set_style_text_color(wl, ui_color_white(), 0);
    lv_obj_center(wl);
}

esp_err_t ui_init(void)
{
    lv_init();

    const size_t buf_sz = (size_t)BOARD_LCD_H * BUF_LINES * sizeof(uint16_t);
    void *buf = heap_caps_malloc(buf_sz, MALLOC_CAP_DMA);
    if (buf == NULL) {
        ESP_LOGE(TAG, "sem RAM para buffer LVGL");
        return ESP_ERR_NO_MEM;
    }

    s_disp = lv_display_create(BOARD_LCD_H, BOARD_LCD_V);
    lv_display_set_flush_cb(s_disp, flush_cb);
    lv_display_set_buffers(s_disp, buf, NULL, buf_sz, LV_DISPLAY_RENDER_MODE_PARTIAL);
    ESP_ERROR_CHECK(board_lcd_on_trans_done(on_lcd_flush_done, s_disp));

    lv_indev_t *indev = lv_indev_create();
    lv_indev_set_type(indev, LV_INDEV_TYPE_POINTER);
    lv_indev_set_read_cb(indev, touch_cb);
    lv_timer_set_period(lv_indev_get_read_timer(indev), 20);

    const esp_timer_create_args_t tick_args = {
        .callback = lvgl_tick,
        .name = "lvgl",
    };
    esp_timer_handle_t tick = NULL;
    ESP_ERROR_CHECK(esp_timer_create(&tick_args, &tick));
    ESP_ERROR_CHECK(esp_timer_start_periodic(tick, 5000));

    build_home();
    ESP_LOGI(TAG, "UI pronta (lista + Wi-Fi)");
    return ESP_OK;
}

void ui_tick(void)
{
    wifi_poll();
    lv_timer_handler();
}
