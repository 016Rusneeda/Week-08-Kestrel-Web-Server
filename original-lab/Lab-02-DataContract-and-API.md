# ใบงานการทดลองที่ 2 (Lab 02): Data Contract & JSON API
> **โครงการ LabBuddy: สร้าง "ภาษากลาง" เชื่อมต่อฮาร์ดแวร์ สู่ Web Server และ Mobile App (Flutter)**  
> *(เปรียบเทียบเชิงแนวคิด: `struct` ในภาษา C $\leftrightarrow$ `class/record` ใน C# $\leftrightarrow$ `JSON Payload` ในเน็ตเวิร์ก)*

---

## 🎯 วัตถุประสงค์การทดลอง (Objectives)
1. เข้าใจแนวคิดของ **Data Contract** (ข้อตกลงรูปแบบข้อมูล) เพื่อให้ทีมฮาร์ดแวร์ (ESP32/STM32) และทีม Mobile (Flutter) สื่อสารกันได้โดยไม่มีข้อผิดพลาด
2. สามารถประกาศโครงสร้างข้อมูลด้วย **C# Record / Class** และแปลงเป็น **JSON (Serialization)** ได้
3. สามารถสร้าง **RESTful Minimal API Endpoints** (`GET`, `POST`) บน Kestrel เพื่อส่งข้อมูลและรับคำสั่งควบคุม
4. เข้าใจและตั้งค่า **CORS (Cross-Origin Resource Sharing)** เพื่อให้แอปพลิเคชันจากภายนอกสามารถยิงขอข้อมูลได้
5. สามารถใช้ `curl` หรือ Browser ทดสอบตรวจรับ Payload ที่ส่งไปให้ทีม Flutter

---

## 🧭 ตารางเปรียบเทียบแนวคิด: จาก C Struct สู่ JSON Data Contract

เวลาเราเขียนโปรแกรม C บนไมโครคอนโทรลเลอร์ เรามักจะแพ็กข้อมูลลงใน `struct` เพื่อส่งออกทาง Serial:

```c
// ภาษา C บนไมโครคอนโทรลเลอร์ (Binary Payload)
typedef struct {
    char device_id[16];   // ชื่อบอร์ด เช่น "eNose-01"
    float temperature;    // อุณหภูมิ
    float humidity;       // ความชื้น
    int gas_adc;          // ค่าอนาล็อกจากเซนเซอร์ก๊าซ
    uint32_t timestamp;   // เวลา
} SensorData;
```

แต่เมื่อข้อมูลต้องส่งข้ามระบบไปยัง Mobile App (Flutter) เราจะใช้ **JSON (JavaScript Object Notation)** เป็นภาษากลาง:

| มิติ | ภาษา C (Microcontroller) | C# Kestrel (Edge Gateway) | Network / Mobile (Flutter) |
| :--- | :--- | :--- | :--- |
| **โครงสร้างข้อมูล** | `struct SensorData` | `public record SensorReading(...)` | `class SensorModel` ใน Dart |
| **รูปแบบการส่ง** | Raw Binary Bytes หรือ CSV string | Object ในหน่วยความจำ RAM | **JSON String** (`{"temp": 25.5, ...}`) |
| **ความผิดพลาดที่พบบ่อย** | Memory alignment / Endianness | Type Mismatch (int vs double) | JSON Key สะกดไม่ตรงกัน (Case-sensitive) |

---

## 🛠️ อุปกรณ์และซอฟต์แวร์ที่ต้องใช้
- เครื่องคอมพิวเตอร์ที่ติดตั้ง **.NET SDK 8.0** หรือ **9.0**
- Terminal / PowerShell
- เว็บบราวเซอร์ หรือเครื่องมือทดสอบ API เช่น `curl`

---

## 🧪 ขั้นตอนที่ 1: การสร้างโปรเจกต์ LabBuddy Gateway
เปิด Terminal แล้วรันคำสั่งต่อไปนี้เพื่อสร้างโฟลเดอร์และโปรเจกต์:

```bash
# 1. สร้างโฟลเดอร์สำหรับ Lab 2
mkdir LabBuddy_API && cd LabBuddy_API

# 2. สร้างโปรเจกต์ Web API แบบ Minimal
dotnet new web -o .
```

---

## 🧪 ขั้นตอนที่ 2: สร้าง Data Contract (โมเดลข้อมูลเซนเซอร์)
เปิดไฟล์ `Program.cs` แล้วลบโค้ดเดิมออกทั้งหมด จากนั้นเริ่มเขียนโค้ดตามส่วนต่อไปนี้:

### 2.1 โค้ดส่วนที่ 1: กำหนด Data Models (ท้ายไฟล์หรือใน Namespace)
เขียนนิยามข้อมูลจำลองสำหรับเครื่องวัดก๊าซ / eNose:

```csharp
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

// ============================================================================
// 1. DATA CONTRACTS (เปรียบเสมือน struct ในภาษา C)
// ============================================================================

// ข้อมูลการอ่านค่าเซนเซอร์ที่ Kestrel จะส่งให้ Mobile App (Flutter)
public record SensorReading(
    string DeviceId,        // รหัสอุปกรณ์ เช่น "eNose-Station-1"
    DateTime Timestamp,      // เวลาที่บันทึก
    double Temperature,      // อุณหภูมิ (°C)
    double Humidity,         // ความชื้นสัมพัทธ์ (%RH)
    int GasAdc,              // ค่าก๊าซดิบจาก ADC (0 - 4095)
    string Status,           // สถานะระบบ เช่น "NORMAL", "WARNING"
    string Mode              // โหมด: "LIVE" หรือ "SIMULATION"
);

// ข้อมูลคำสั่งที่ Mobile App (Flutter) จะส่งเข้ามาสั่งการฮาร์ดแวร์
public record CalibrationCommand(
    string TargetSensor,     // เซนเซอร์ที่ต้องการ Calibrate เช่น "GAS_MQ135"
    int BaselineValue,       // ค่าอ้างอิงฐาน (Zero-point)
    string OperatorName      // ชื่อผู้ทดลองที่สั่งการ
);

// ข้อมูลผลลัพธ์ตอบกลับเมื่อทำงานสำเร็จ
public record ApiResponse(
    bool Success,
    string Message,
    DateTime HandledAt
);
```

> 💡 **ความรู้เสริม (C# Record):**  
> `record` ใน C# เป็นชนิดข้อมูลพิเศษที่ออกแบบมาสำหรับทำ Data Transfer Object (DTO) โดยเฉพาะ มีคุณสมบัติคงที่ (Immutable), มีการเปรียบเทียบค่าอัตโนมัติ และแปลงเป็น JSON ได้เร็วมากโดยแทบไม่เปลือง Memory

---

## 🧪 ขั้นตอนที่ 3: ประกอบร่าง Kestrel Server และเปิด CORS
เขียนส่วนเริ่มต้นของโปรแกรมเพื่อเปิดพอร์ตและอนุญาตให้ภายนอกเชื่อมต่อได้:

```csharp
// ============================================================================
// 2. SERVER BUILDER & CONFIGURATION
// ============================================================================
var builder = WebApplication.CreateBuilder(args);

// เปิดใช้งาน CORS (สำคัญมาก! เพื่อให้ Flutter / Web Browser ยิงเข้ามาได้)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()   // อนุญาตทุก IP (สำหรับใช้ในแล็บทดลอง)
              .AllowAnyMethod()   // อนุญาต GET, POST, PUT, DELETE
              .AllowAnyHeader();  // อนุญาตทุก Header
    });
});

var app = builder.Build();

// เรียกใช้ CORS ใน Middleware Pipeline
app.UseCors();

// ตัวแปรจำลอง In-Memory Database (เก็บประวัติการอ่านค่า 10 ครั้งล่าสุด)
var readingHistory = new List<SensorReading>();
```

---

## 🧪 ขั้นตอนที่ 4: สร้าง Endpoints (ช่องทางสื่อสารสำหรับ Mobile App)
เพิ่ม Route ด้านล่างนี้ต่อจาก `app.UseCors()`:

```csharp
// ============================================================================
// 3. API ENDPOINTS (จุดเชื่อมต่อสำหรับทีม Flutter)
// ============================================================================

// ----------------------------------------------------------------------------
// GET /api/telemetry/latest
// หน้าที่: ส่งค่าเซนเซอร์ล่าสุดแบบ Real-Time กลับไปให้แอปบนมือถือ
// ----------------------------------------------------------------------------
app.MapGet("/api/telemetry/latest", () =>
{
    // จำลองการอ่านค่าจากเซนเซอร์ (ใน Lab 3 จะเปลี่ยนเป็นการอ่านจากพอร์ต USB จริง)
    var currentData = new SensorReading(
        DeviceId: "eNose-Lab01",
        Timestamp: DateTime.UtcNow,
        Temperature: 26.8,
        Humidity: 58.4,
        GasAdc: 1850,
        Status: "NORMAL",
        Mode: "SIMULATION"
    );

    // เก็บลงประวัติ
    readingHistory.Add(currentData);
    if (readingHistory.Count > 20) readingHistory.RemoveAt(0);

    // Kestrel จะแปลง Object นี้เป็น JSON ให้อัตโนมัติ!
    return Results.Ok(currentData);
});

// ----------------------------------------------------------------------------
// GET /api/telemetry/history
// หน้าที่: ส่งประวัติข้อมูลทั้งหมดกลับไปให้แอปวาดกราฟเส้น
// ----------------------------------------------------------------------------
app.MapGet("/api/telemetry/history", () =>
{
    return Results.Ok(readingHistory);
});

// ----------------------------------------------------------------------------
// POST /api/command/calibrate
// หน้าที่: รับคำสั่ง Calibrate เซนเซอร์จากแอปมือถือเพื่อเตรียมส่งลงบอร์ดฮาร์ดแวร์
// ----------------------------------------------------------------------------
app.MapPost("/api/command/calibrate", (CalibrationCommand cmd) =>
{
    // ตรวจสอบความถูกต้องของข้อมูล (Validation)
    if (string.IsNullOrWhiteSpace(cmd.TargetSensor) || cmd.BaselineValue < 0)
    {
        return Results.BadRequest(new ApiResponse(false, "Invalid command parameters.", DateTime.UtcNow));
    }

    Console.WriteLine($"[HARDWARE CMD] Calibrating {cmd.TargetSensor} to Base={cmd.BaselineValue} by {cmd.OperatorName}");

    // ส่งสถานะยืนยันกลับไปให้ Mobile App
    return Results.Ok(new ApiResponse(
        Success: true,
        Message: $"Sensor {cmd.TargetSensor} calibrated successfully.",
        HandledAt: DateTime.UtcNow
    ));
});

// รันเซิร์ฟเวอร์ที่ Port 5000
app.Run("http://localhost:5000");
```

---

## 🧪 ขั้นตอนที่ 5: การทดสอบรับ-ส่งข้อมูลจริง (Verification)

### 5.1 รันโปรแกรม Kestrel
เปิด Terminal ในโฟลเดอร์โปรเจกต์แล้วรัน:
```bash
dotnet run
```
เมื่อเซิร์ฟเวอร์ทำงาน จะขึ้นข้อความแจ้งว่าฟังพอร์ตอยู่ที่ `http://localhost:5000`

---

### 5.2 การทดสอบฝั่งรับข้อมูล (GET Latest Data)
เปิด Terminal อีกหน้าต่าง หรือเปิด Web Browser ไปที่:
```text
http://localhost:5000/api/telemetry/latest
```
หรือรันคำสั่ง `curl`:
```bash
curl -i http://localhost:5000/api/telemetry/latest
```

**ผลลัพธ์ JSON ที่ได้รับกลับมา:**
```json
{
  "deviceId": "eNose-Lab01",
  "timestamp": "2026-09-04T02:50:11.1234567Z",
  "temperature": 26.8,
  "humidity": 58.4,
  "gasAdc": 1850,
  "status": "NORMAL",
  "mode": "SIMULATION"
}
```
> 🔍 **สังเกตตัวแปร:**  
> สังเกตว่าใน C# เราตั้งชื่อตัวแปรเป็น PascalCase (`DeviceId`, `Temperature`) แต่ Kestrel จะแปลงให้เป็น **camelCase (`deviceId`, `temperature`)** ให้อัตโนมัติ ซึ่งเป็นรูปแบบมาตรฐานที่ฝั่ง Flutter และ JavaScript นิยมใช้

---

### 5.3 การทดสอบฝั่งส่งคำสั่ง (POST Calibration Command)
สมมติว่าเพื่อนฝั่งทีม Flutter กดปุ่ม "Calibrate" บนหน้าจอมือถือ แล้วยิงคำสั่ง JSON มาที่ Kestrel:

รันคำสั่ง `curl` จำลองการยิง POST:
```bash
curl -i -X POST http://localhost:5000/api/command/calibrate ^
  -H "Content-Type: application/json" ^
  -d "{\"targetSensor\": \"GAS_MQ135\", \"baselineValue\": 1500, \"operatorName\": \"Student_A\"}"
```
*(ถ้าใช้ Linux หรือ macOS ให้เปลี่ยนเครื่องหมาย `^` เป็น `\`)*

**ผลลัพธ์ตอบกลับจาก Kestrel:**
```json
{
  "success": true,
  "message": "Sensor GAS_MQ135 calibrated successfully.",
  "handledAt": "2026-09-04T02:51:30.4567890Z"
}
```
และบนหน้าจอ Console ของ Kestrel จะพิมพ์ Log แจ้งเตือน:
```text
[HARDWARE CMD] Calibrating GAS_MQ135 to Base=1500 by Student_A
```

---

## 🎯 ภารกิจท้าทายประจำแล็บ (Lab Challenges for Students)

ให้นักศึกษาดัดแปลงโค้ดใน `Program.cs` เพื่อแก้โจทย์ 2 ข้อต่อไปนี้:

### 🧩 ภารกิจที่ 1: เพิ่มฟิลด์การอ่านค่าความเข้มข้นก๊าซ (PPM)
1. ให้เพิ่ม Property ใน `SensorReading` ชื่อ `GasPpm` (ชนิดข้อมูลเป็น `double`)
2. คำนวณค่า `GasPpm` โดยนำค่า `GasAdc / 4.0`
3. ทดสอบเรียกผ่าน `curl` แล้วตรวจสอบว่าใน JSON มีฟิลด์ `"gasPpm"` ปรากฏขึ้นมาถูกต้องหรือไม่

### 🧩 ภารกิจที่ 2: ระบบตรวจจับค่าวิกฤต (Threshold Alarm)
1. ใน Endpoint `GET /api/telemetry/latest` ให้เขียนเงื่อนไขตรวจสอบ:
   - ถ้า `GasAdc > 2000` ให้ตั้งค่า `Status = "CRITICAL_ALERT"`
   - ถ้าไม่เกิน ให้เป็น `"NORMAL"`
2. จำลองค่าทดสอบแล้วดูว่าแอป Flutter จะได้รับสถานะเตือนภัยถูกต้องหรือไม่

---

## 📝 คำถามทบทวนความเข้าใจ (Discussion & Checkpoint)

1. **ทำไมเราถึงไม่ส่ง Raw Struct Bytes จาก C เข้าตรงไปยังมือถือของเพื่อน?** ทำไมต้องให้ Kestrel แปลงเป็น JSON ก่อน?
2. **CORS คืออะไร?** หากเราลบบรรทัด `app.UseCors()` ออก เวลาเพื่อนเปิดแอปบน Web Browser หรือ Emulator ยิงมาหาเรา จะเกิดข้อผิดพลาดอะไรขึ้น?
3. **การออกแบบ Data Contract ร่วมกัน:** หากฝั่งฮาร์ดแวร์ส่งค่าอุณหภูมิเป็น String เช่น `"25.5 C"` แต่ฝั่ง Flutter ตั้งตัวแปรรับเป็นตัวเลข `double temperature` จะเกิดอะไรขึ้นกับแอปพลิเคชัน?
