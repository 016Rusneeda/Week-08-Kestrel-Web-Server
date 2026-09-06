https://learn.microsoft.com/en-us/aspnet/core/tutorials/min-web-api?view=aspnetcore-10.0&tabs=visual-studio

## 1. Minimal APIs คืออะไร และทำไมถึงเกิดขึ้นมา?

ตามเอกสารทางการของ Microsoft **Minimal APIs** ถูกออกแบบขึ้นมาเพื่อลดความซับซ้อน (Boilerplate Code) ในการสร้าง HTTP REST API

- **Traditional Controller-based API (ดั้งเดิม)** ต้องสร้างโครงสร้างโฟลเดอร์แยก `Controllers/`, สืบทอดคลาสจาก `ControllerBase`, ตกแต่งด้วย Attributes จำนวนมาก (`[ApiController]`, `[Route]`, `[HttpGet]`) ซึ่งเหมาะกับเว็บแอปพลิเคชันขนาดใหญ่ แต่เกิด Overhead และความซับซ้อนโดยไม่จำเป็นสำหรับงานขนาดเล็กหรือ Microservices
- **Minimal APIs (ยุคใหม่):** อนุญาตให้นักพัฒนาประกาศ Endpoint และ Routing ได้โดยตรงในไฟล์เดียว (`Program.cs`) ผ่านฟังก์ชัน Lambda ที่กระชับ ประหยัดหน่วยความจำ ทำงานได้เร็วที่สุดบน Kestrel และรองรับฟีเจอร์ระดับองค์กรอย่างครบถ้วน (Dependency Injection, Model Validation, OpenAPI/Swagger)

---

## 2. โครงสร้างพื้นฐานของ Minimal API (Architecture & Lifecycle)

การทำงานของ Minimal API จะแบ่งออกเป็น **3 ขั้นตอนหลัก** ในไฟล์ `Program.cs`:

``` mermaid

flowchart TD

    A["1. WebApplication.CreateBuilder(args)<br/>เตรียม DI Container & Configuration"] --> B["2. builder.Build()<br/>สร้างอินสแตนซ์ WebApplication (Pipeline)"]

    B --> C["3. Map Endpoints (Routing)<br/>app.MapGet(), app.MapPost(), ..."]

    C --> D["4. app.Run()<br/>เริ่มเปิดรับฟังการเชื่อมต่อบน Kestrel"]
```

### ตัวอย่างโครงสร้างมาตรฐาน:

```csharp
var builder = WebApplication.CreateBuilder(args);
// ลงทะเบียน Services ที่ต้องการใน Dependency Injection (DI)
// เช่น Database Context, Background Workers, In-Memory Caching
var app = builder.Build();
// กำหนด Endpoint Routing
app.MapGet("/", () => "Hello World!");
// เริ่มการทำงานของเซิร์ฟเวอร์ Kestrel
app.Run();
```

---

## 3. รูปแบบการสร้าง RESTful CRUD Endpoints ตามแนวทาง Microsoft

เอกสาร Microsoft Learn ได้สาธิตมาตรฐานการออกแบบ CRUD (Create, Read, Update, Delete) โดยใช้ **`TypedResults`** เพื่อคืนค่าสถานะ HTTP Status Code ที่ชัดเจนและตรงตามมาตรฐาน REST:

| HTTP Method | การใช้งานใน Minimal API             | จุดประสงค์               | HTTP Status Code ที่ควรส่งกลับ        |
| ----------- | ----------------------------------- | ------------------------ | ------------------------------------- |
| **GET**     | `app.MapGet("/items", ...)`         | ดึงรายการข้อมูลทั้งหมด   | `200 OK`                              |
| **GET**     | `app.MapGet("/items/{id}", ...)`    | ดึงข้อมูลชิ้นเดียวตาม ID | `200 OK` หรือ `404 Not Found`         |
| **POST**    | `app.MapPost("/items", ...)`        | สร้างข้อมูลใหม่          | `201 Created` (พร้อม Header Location) |
| **PUT**     | `app.MapPut("/items/{id}", ...)`    | อัปเดตหรือแทนที่ข้อมูล   | `204 NoContent` หรือ `404 Not Found`  |
| **DELETE**  | `app.MapDelete("/items/{id}", ...)` | ลบข้อมูล                 | `204 NoContent` หรือ `404 Not Found`  |

### ตัวอย่างโค้ดสาธิต:

```csharp
// 1. GET: อ่านรายการทั้งหมด
app.MapGet("/api/sensors", (SensorStore store) => 
    TypedResults.Ok(store.GetAll()));
// 2. GET by ID: อ่านตาม Parameter
app.MapGet("/api/sensors/{id}", (string id, SensorStore store) =>
{
    var sensor = store.Find(id);
    return sensor is not null ? TypedResults.Ok(sensor) : TypedResults.NotFound();
});
// 3. POST: สร้างข้อมูลใหม่
app.MapPost("/api/sensors", (SensorInput input, SensorStore store) =>
{
    var created = store.Add(input);
    return TypedResults.Created($"/api/sensors/{created.Id}", created);
});
// 4. DELETE: ลบข้อมูล
app.MapDelete("/api/sensors/{id}", (string id, SensorStore store) =>
{
    return store.Delete(id) ? TypedResults.NoContent() : TypedResults.NotFound();
});
```

---

## 4. แนวปฏิบัติที่ดี (Best Practices) จากเอกสาร Microsoft

บทเรียนของ Microsoft ชี้ให้เห็นถึงประเด็นสำคัญที่ควรคำนึงถึงในการพัฒนา Minimal API:

### 1. การจัดกลุ่ม Route ด้วย `MapGroup`

หากมี Endpoint ที่ขึ้นต้นด้วย Path เดียวกันหลายตัว การใช้ `MapGroup` จะช่วยให้โค้ดเป็นระเบียบและลดการพิมพ์ซ้ำ:

```csharp
var sensorGroup = app.MapGroup("/api/telemetry");
sensorGroup.MapGet("/", GetAllTelemetry);
sensorGroup.MapGet("/{id}", GetTelemetryById);
sensorGroup.MapPost("/", AddTelemetry);
```
### 2. การใช้ DTO (Data Transfer Object) ป้องกัน Over-Posting

ไม่ควรนำ Data Model ภายในระบบ (เช่น Model ที่มีฟิลด์ลับ เช่น รหัสผ่าน หรือ Timestamp ภายใน) ส่งออกไปหรือรับจากไคลเอนต์โดยตรง แต่ควรสร้าง DTO ขึ้นมาเพื่อคัดกรองเฉพาะข้อมูลที่จำเป็นและปลอดภัยเท่านั้น

### 3. การแยก Handler Logic ออกจาก `Program.cs`

เมื่อโปรเจกต์เติบโตขึ้น สามารถย้ายฟังก์ชันของ Lambda ออกมาเป็น Static Method แยกไฟล์ เพื่อให้ `Program.cs` อ่านง่ายและดูแลได้สะดวก

---

## 5. ความเกี่ยวข้องกับโปรเจกต์ IoT ใน Week 08 ของเรา

ในบริบทของ **Edge IoT Gateway**:

1. **Low Memory & Fast Startup:** Minimal API ใช้แรมเริ่มต้นเพียง ~30 MB จึงรันบน Gateway หรือบอร์ดขนาดเล็กอย่าง Raspberry Pi ได้ดีเยี่ยม
2. **Seamless Serialization:** สามารถคืนค่า C# `record` หรือ anonymous object ออกมาเป็น JSON โดย Kestrel แปลงข้อมูลด้วย `System.Text.Json` ให้อัตโนมัติโดยไม่ต้องคอนฟิกเพิ่ม
3. **Background Worker Integration:** ทำงานร่วมกับ `BackgroundService` ได้อย่างไร้รอยต่อ โดยเธรดเบื้องหลังอ่านค่า Serial จาก ESP32 แล้วนำมาเสิร์ฟผ่าน `app.MapGet("/api/telemetry", ...)` ให้นักศึกษานำไปแสดงผลบน SVG Dashboard ได้ทันที