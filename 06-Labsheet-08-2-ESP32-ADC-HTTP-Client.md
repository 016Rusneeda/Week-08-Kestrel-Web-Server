# ใบงานที่ 8.2: ESP32 ADC & HTTP Client Telemetry Cookbook

## 0. กล่าวนำ (Introduction)
ในใบงานนี้ นักศึกษาจะได้ลงมือประกอบวงจรเซนเซอร์ **Potentiometer** และ **LDR** เข้ากับบอร์ด ESP32 และเขียนโปรแกรมในรูปแบบ **Cookbook Recipe ย่อย** เพื่ออ่านค่าอนาล็อก แปลงเป็นโครงสร้างข้อมูล **JSON** และส่งผ่านโปรโตคอล **HTTP POST** เข้าไปยัง Kestrel Web Server บนเครื่องคอมพิวเตอร์

---

## 1. วัตถุประสงค์ (Objectives)
1. สามารถต่อวงจร Potentiometer และ LDR Sensor เข้ากับขา ADC1 ของ ESP32 ได้อย่างถูกต้อง
2. สามารถเขียนโค้ดตั้งค่าไดรเวอร์ ADC Oneshot อ่านค่าความละเอียด 12-bit (0 - 4095)
3. สามารถใช้ไลบรารี `cJSON` (หรือ `ArduinoJson`) เพื่อแปลงข้อมูลเซนเซอร์เป็น JSON Payload
4. สามารถใช้โมดูล HTTP Client ยิงข้อมูลเข้า Kestrel Web Server บนพอร์ต 5000 ได้สำเร็จ

---

## 2. อุปกรณ์ที่ใช้ในการทดลอง (Equipment)
* บอร์ดไมโครคอนโทรลเลอร์ ESP32 พร้อมสาย USB
* ตัวต้านทานปรับค่าได้ (Potentiometer) $10k\Omega$ จำนวน 1 ตัว
* เซนเซอร์วัดแสง LDR พร้อมตัวต้านทาน $10k\Omega$ จำนวน 1 ชุด
* Breadboard และสาย Jumper Wires

---

## 3. ขั้นตอนการปฏิบัติการ

---

### Recipe 8.2.1: การต่อวงจรฮาร์ดแวร์และการหาหมายเลข IP Address ของ PC

#### 📝 ขั้นตอนการปฏิบัติ:
1. **ต่อวงจร Potentiometer:**
   - ขากลาง Volume $\rightarrow$ ต่อเข้าขา **GPIO 34** (ADC1 Channel 6)
   - ขาซ้าย $\rightarrow$ ต่อเข้า **3.3V** | ขาขวา $\rightarrow$ ต่อลง **GND**
2. **ต่อวงจร LDR Sensor (Voltage Divider):**
   - ขาหนึ่งของ LDR $\rightarrow$ ต่อ **3.3V**
   - อีกขาของ LDR $\rightarrow$ ต่อร่วมกับตัวต้านทาน $10k\Omega$ และนำสายสัญญาณเข้าขา **GPIO 35** (ADC1 Channel 7)
   - ขาอีกข้างของตัวต้านทาน $10k\Omega$ $\rightarrow$ ต่อลง **GND**
3. **ค้นหา IP Address ของเครื่องคอมพิวเตอร์:**
   เปิด PowerShell บน PC พิมพ์คำสั่ง: `ipconfig` แล้วจดหมายเลข **IPv4 Address** เช่น `192.168.1.100`

#### 🧪 Micro-Checkpoint 1 (จุดทดสอบที่ 1):
ตรวจสอบความถูกต้องของผังสายสัญญาณ และมั่นใจว่าคอมพิวเตอร์และ ESP32 อยู่บนเกตเวย์ Wi-Fi วงเดียวกัน

---

### Recipe 8.2.2: การตั้งค่า ADC Oneshot Driver อ่านค่าเซนเซอร์ (ESP-IDF)

#### 📝 ขั้นตอนการปฏิบัติ:
เปิดไฟล์ `main/main.c` และสร้างฟังก์ชันตั้งค่าขา ADC1 (GPIO34 และ GPIO35):

```c
#include "esp_adc/adc_oneshot.h"

static adc_oneshot_unit_handle_t adc1_handle;

void adc_init(void)
{
    // 1. สร้าง Unit Handle สำหรับ ADC1
    adc_oneshot_unit_init_cfg_t init_config1 = { .unit_id = ADC_UNIT_1 };
    adc_oneshot_new_unit(&init_config1, &adc1_handle);

    // 2. กำหนดค่า Channel 6 (GPIO34) และ Channel 7 (GPIO35)
    adc_oneshot_chan_cfg_t config = {
        .bitwidth = ADC_BITWIDTH_DEFAULT, // 12-bit (0 - 4095)
        .atten = ADC_ATTEN_DB_12,         // แรงดันสูงสุด ~3.3V
    };
    adc_oneshot_config_channel(adc1_handle, ADC_CHANNEL_6, &config);
    adc_oneshot_config_channel(adc1_handle, ADC_CHANNEL_7, &config);
}
```

#### 🧪 Micro-Checkpoint 2 (จุดทดสอบที่ 2):
ทดลองอ่านค่าดิบลง Serial Log:
```c
int pot_raw = 0, ldr_raw = 0;
adc_oneshot_read(adc1_handle, ADC_CHANNEL_6, &pot_raw);
adc_oneshot_read(adc1_handle, ADC_CHANNEL_7, &ldr_raw);
ESP_LOGI("ADC_TEST", "POT Raw: %d | LDR Raw: %d", pot_raw, ldr_raw);
```
**ผลลัพธ์:** เมื่อหมุน Potentiometer ค่าใน Serial Monitor ต้องเปลี่ยนตามช่วง 0 ถึง 4095

---

### Recipe 8.2.3: การสร้างโครงสร้างข้อมูล JSON ด้วย `cJSON`

#### 📝 ขั้นตอนการปฏิบัติ:
สร้างฟังก์ชันจัดแพ็กเกจข้อมูลเซนเซอร์ลงในรูปแบบ JSON String:

```c
#include "cJSON.h"

char* create_json_payload(const char* device_id, int pot_val, int ldr_val)
{
    cJSON *root = cJSON_CreateObject();
    cJSON_AddStringToObject(root, "device_id", device_id);
    cJSON_AddNumberToObject(root, "potentiometer", pot_val);
    cJSON_AddNumberToObject(root, "ldr", ldr_val);

    char *json_str = cJSON_PrintUnformatted(root);
    cJSON_Delete(root); // ล้างหน่วยความจำคลาส cJSON
    return json_str;    // คืนค่า Pointer (ต้อง free() หลังใช้งาน)
}
```

#### 🧪 Micro-Checkpoint 3 (จุดทดสอบที่ 3):
ทดลองเรียกฟังก์ชันแล้วพิมพ์ดูผลลัพธ์ผ่าน Serial Monitor:
```c
char *payload = create_json_payload("ESP32_DEV", 2048, 1024);
ESP_LOGI("JSON_TEST", "Generated Payload: %s", payload);
free(payload);
```
**ผลลัพธ์ที่ต้องปรากฏ:** `{"device_id":"ESP32_DEV","potentiometer":2048,"ldr":1024}`

---

### Recipe 8.2.4: การยิง HTTP POST ไปยัง Kestrel Web Server

#### 📝 ขั้นตอนการปฏิบัติ:
นำโค้ดทั้งหมดมารวมกับ `esp_http_client` เพื่อส่งข้อมูลเข้า Kestrel บนพอร์ต 5000:

```c
#include "esp_http_client.h"

void send_to_kestrel(const char* json_data)
{
    esp_http_client_config_t config = {
        .url = "http://192.168.1.100:5000/api/telemetry", // 💡 แก้เป็น IP ของ PC
        .method = HTTP_METHOD_POST,
        .timeout_ms = 2000,
    };

    esp_http_client_handle_t client = esp_http_client_init(&config);
    esp_http_client_set_header(client, "Content-Type", "application/json");
    esp_http_client_set_post_field(client, json_data, strlen(json_data));

    esp_err_t err = esp_http_client_perform(client);
    if (err == ESP_OK) {
        ESP_LOGI("HTTP", "POST Success! Status: %d", esp_http_client_get_status_code(client));
    } else {
        ESP_LOGE("HTTP", "POST Failed: %s", esp_err_to_name(err));
    }
    esp_http_client_cleanup(client);
}
```

#### 🧪 Micro-Checkpoint 4 (จุดทดสอบที่ 4):
1. เปิด Kestrel Web Server บนเครื่อง PC รอไว้ (`dotnet run`)
2. แฟลชโค้ดลง ESP32 (`idf.py flash monitor`)
3. **ผลลัพธ์ที่ต้องปรากฏ:** หน้าต่าง Kestrel Console บน PC ต้องได้รับข้อมูลอัปเดตทุกๆ 1 วินาที!

---

### 💡 ทางเลือกเพิ่มเติมสำหรับผู้ใช้ Arduino IDE (C++):
```cpp
#include <WiFi.h>
#include <HTTPClient.h>
#include <ArduinoJson.h>

void loop() {
  if (WiFi.status() == WL_CONNECTED) {
    StaticJsonDocument<200> doc;
    doc["device_id"] = "ESP32_ARDUINO";
    doc["potentiometer"] = analogRead(34);
    doc["ldr"] = analogRead(35);

    String jsonPayload;
    serializeJson(doc, jsonPayload);

    HTTPClient http;
    http.begin("http://192.168.1.100:5000/api/telemetry");
    http.addHeader("Content-Type", "application/json");
    int httpCode = http.POST(jsonPayload);
    Serial.printf("HTTP Response Code: %d\n", httpCode);
    http.end();
  }
  delay(1000);
}
```
