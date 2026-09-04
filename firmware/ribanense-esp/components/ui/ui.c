#include "ui.h"
#include "board.h"
#include "board_pins.h"
#include "ribanense_esp_version.h"
#include "storage.h"
#include "ui_palette.h"

#include "esp_heap_caps.h"
#include "esp_lcd_panel_io.h"
#include "esp_log.h"
#include "esp_timer.h"
#include "lvgl.h"

#define BUF_LINES 20
#define UI_KB_H   ((BOARD_LCD_V * 35) / 100)

static const char *TAG = "ui";
static lv_display_t *s_disp;
static lv_obj_t *s_ta;
static lv_obj_t *s_kb;
static lv_obj_t *s_status;

static void kb_hide(void);

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
    /* draw_bitmap é DMA: flush_ready só no callback on_color_trans_done. */
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

static void set_status(const char *msg, lv_color_t color)
{
    lv_label_set_text(s_status, msg);
    lv_obj_set_style_text_color(s_status, color, 0);
}

static void on_save(lv_event_t *e)
{
    (void)e;
    ESP_LOGI(TAG, "click Gravar SD");
    const char *txt = lv_textarea_get_text(s_ta);
    if (!storage_ready()) {
        set_status("SD ausente", ui_color_red());
        return;
    }
    if (storage_write_text("ribanense.txt", txt) == ESP_OK) {
        set_status("gravado no SD", ui_color_green());
    } else {
        set_status("falha ao gravar", ui_color_red());
    }
    kb_hide();
}

static void style_keys(lv_obj_t *kb, lv_style_selector_t sel, bool pressed)
{
    lv_obj_set_style_bg_color(kb, ui_color_black(), sel);
    lv_obj_set_style_bg_opa(kb, LV_OPA_COVER, sel);
    lv_obj_set_style_text_color(kb, ui_color_white(), sel);
    lv_obj_set_style_border_color(kb, pressed ? ui_color_yellow() : ui_color_white(), sel);
    lv_obj_set_style_border_width(kb, pressed ? 2 : 1, sel);
    lv_obj_set_style_radius(kb, 0, sel);
    lv_obj_set_style_shadow_width(kb, 0, sel);
    lv_obj_set_style_transform_width(kb, 0, sel);
    lv_obj_set_style_transform_height(kb, 0, sel);
    lv_obj_set_style_pad_all(kb, 0, sel);
}

static void style_keyboard(lv_obj_t *kb)
{
    /* Tema simple pinta ITEMS de branco e CHECKED de cinza. */
    lv_obj_remove_style_all(kb);

    lv_obj_set_style_bg_color(kb, ui_color_black(), LV_PART_MAIN);
    lv_obj_set_style_bg_opa(kb, LV_OPA_COVER, LV_PART_MAIN);
    lv_obj_set_style_pad_all(kb, 2, LV_PART_MAIN);
    lv_obj_set_style_pad_row(kb, 2, LV_PART_MAIN);
    lv_obj_set_style_pad_column(kb, 2, LV_PART_MAIN);
    lv_obj_set_style_border_width(kb, 0, LV_PART_MAIN);
    lv_obj_set_style_outline_width(kb, 0, LV_PART_MAIN);
    lv_obj_set_style_outline_width(kb, 0, LV_PART_MAIN | LV_STATE_FOCUS_KEY);
    lv_obj_set_style_outline_width(kb, 0, LV_PART_MAIN | LV_STATE_EDITED);
    lv_obj_set_style_radius(kb, 0, LV_PART_MAIN);
    lv_obj_set_style_shadow_width(kb, 0, LV_PART_MAIN);

    const lv_state_t bits[] = {
        LV_STATE_CHECKED, LV_STATE_FOCUSED, LV_STATE_FOCUS_KEY, LV_STATE_EDITED,
    };
    for (int mask = 0; mask < 16; mask++) {
        lv_style_selector_t sel = LV_PART_ITEMS;
        for (int b = 0; b < 4; b++) {
            if (mask & (1 << b)) {
                sel |= bits[b];
            }
        }
        style_keys(kb, sel, false);
        style_keys(kb, sel | LV_STATE_PRESSED, true);
    }
}

static void kb_show(void)
{
    if (s_kb) {
        lv_obj_remove_flag(s_kb, LV_OBJ_FLAG_HIDDEN);
        lv_obj_move_foreground(s_kb);
        lv_obj_align(s_kb, LV_ALIGN_BOTTOM_MID, 0, 0);
    }
}

static void kb_hide(void)
{
    if (s_kb) {
        lv_obj_add_flag(s_kb, LV_OBJ_FLAG_HIDDEN);
    }
}

static void on_ta_open_kb(lv_event_t *e)
{
    (void)e;
    ESP_LOGI(TAG, "click textarea");
    kb_show();
}

static void on_kb_done(lv_event_t *e)
{
    (void)e;
    kb_hide();
}

static void build_home(bool sd_ok)
{
    lv_obj_t *scr = lv_screen_active();
    lv_obj_set_style_bg_color(scr, ui_color_black(), 0);
    lv_obj_set_style_bg_opa(scr, LV_OPA_COVER, 0);
    lv_obj_set_style_text_color(scr, ui_color_white(), 0);
    lv_obj_set_flex_flow(scr, LV_FLEX_FLOW_COLUMN);
    lv_obj_set_style_pad_left(scr, 4, 0);
    lv_obj_set_style_pad_right(scr, 4, 0);
    lv_obj_set_style_pad_top(scr, 4, 0);
    lv_obj_set_style_pad_bottom(scr, 0, 0);
    lv_obj_set_style_pad_row(scr, 3, 0);

    lv_obj_t *title = lv_label_create(scr);
    lv_label_set_text(title, RIBANENSEESP_PRODUCT);
    lv_obj_set_style_text_color(title, ui_color_blue(), 0);

    lv_obj_t *ver = lv_label_create(scr);
    lv_label_set_text(ver, RIBANENSEESP_VERSION);

    s_status = lv_label_create(scr);
    set_status(sd_ok ? "SD ok" : "SD ausente", sd_ok ? ui_color_green() : ui_color_red());

    s_ta = lv_textarea_create(scr);
    lv_obj_set_width(s_ta, lv_pct(100));
    lv_obj_set_height(s_ta, 40);
    lv_textarea_set_placeholder_text(s_ta, "teclado...");
    lv_obj_set_style_bg_color(s_ta, ui_color_black(), 0);
    lv_obj_set_style_text_color(s_ta, ui_color_white(), 0);
    lv_obj_set_style_border_color(s_ta, ui_color_white(), 0);
    lv_obj_set_style_border_width(s_ta, 1, 0);
    lv_obj_set_style_radius(s_ta, 0, 0);

    lv_obj_t *btn = lv_button_create(scr);
    lv_obj_set_style_bg_color(btn, ui_color_blue(), 0);
    lv_obj_set_style_radius(btn, 0, 0);
    lv_obj_set_style_shadow_width(btn, 0, 0);
    lv_obj_add_event_cb(btn, on_save, LV_EVENT_CLICKED, NULL);
    lv_obj_t *bl = lv_label_create(btn);
    lv_label_set_text(bl, "Gravar SD");
    lv_obj_set_style_text_color(bl, ui_color_white(), 0);

    lv_obj_add_event_cb(s_ta, on_ta_open_kb, LV_EVENT_CLICKED, NULL);

    s_kb = lv_keyboard_create(scr);
    lv_obj_add_flag(s_kb, LV_OBJ_FLAG_FLOATING);
    lv_obj_set_size(s_kb, BOARD_LCD_H, UI_KB_H);
    lv_obj_set_style_min_height(s_kb, UI_KB_H, 0);
    lv_obj_set_style_max_height(s_kb, UI_KB_H, 0);
    lv_obj_align(s_kb, LV_ALIGN_BOTTOM_MID, 0, 0);
    style_keyboard(s_kb);
    lv_keyboard_set_textarea(s_kb, s_ta);
    lv_keyboard_set_popovers(s_kb, false);
    lv_obj_add_event_cb(s_kb, on_kb_done, LV_EVENT_READY, NULL);
    lv_obj_add_event_cb(s_kb, on_kb_done, LV_EVENT_CANCEL, NULL);
    kb_hide();
}

esp_err_t ui_init(bool sd_ok)
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
    /* LVGL 9 amarra o indev a LV_DEF_REFR_PERIOD (333 ms). O painel fica em 3 Hz; o toque não. */
    lv_timer_set_period(lv_indev_get_read_timer(indev), 20);

    const esp_timer_create_args_t tick_args = {
        .callback = lvgl_tick,
        .name = "lvgl",
    };
    esp_timer_handle_t tick = NULL;
    ESP_ERROR_CHECK(esp_timer_create(&tick_args, &tick));
    ESP_ERROR_CHECK(esp_timer_start_periodic(tick, 5000));

    build_home(sd_ok);
    ESP_LOGI(TAG, "UI pronta (flush 3 Hz, toque 50 Hz)");
    return ESP_OK;
}

void ui_tick(void)
{
    lv_timer_handler();
}
