# ใบงานที่ 8.1: Kestrel Web Server & Minimal API Cookbook

## 0. กล่าวนำ (Introduction)
ใบงานนี้ถูกออกแบบในรูปแบบ **Cookbook (สูตรการเขียนโค้ดเป็นขั้นตอนย่อย)** โดยแบ่งเป็น **Recipe ย่อยๆ** เพื่อให้นักศึกษาได้ฝึกพิมพ์โค้ด ทีละส่วน เข้าใจหน้าที่ของบรรทัดคำสั่ง และมี **Micro-Checkpoint (จุดทดสอบย่อย)** เพื่อตรวจสอบความถูกต้องของโปรแกรมในทุกๆ ขั้นตอน

---

## 1. วัตถุประสงค์ (Objectives)
1. ติดตั้งและเปิดใช้งาน .NET SDK ผ่าน Command Line
2. สร้างและกำหนดค่า Kestrel Web Server เปิดรับฟังบนพอร์ต 5000 (`0.0.0.0:5000`)
3. สร้าง Data Model และ REST API Endpoints (`POST` และ `GET`)
4. ปรับแต่งโครงสร้างข้อมูลให้มีความปลอดภัยแบบ Thread-Safe ด้วย `ConcurrentQueue`

---

## 2. อุปกรณ์และโปรแกรมที่ใช้ (Equipment & Tools)
* คอมพิวเตอร์ระบบปฏิบัติการ Windows / macOS / Linux
* โปรแกรม **Visual Studio Code**
* **.NET 8.0 SDK** หรือใหม่กว่า
* **PowerShell** หรือ **Terminal**

---

## 3. ขั้นตอนการปฏิบัติการแบบ Cookbook (Step-by-Step Recipes)

---

### Recipe 8.1.1: สร้างโครงสร้างโปรเจกต์และ Hello Kestrel Server (5 บรรทัดแรก)

#### 📝 ขั้นตอนการปฏิบัติ:
1. เปิด **PowerShell** หรือ **Terminal** ย้ายไปยังโฟลเดอร์ทำงาน:
   ```powershell
   cd d:\GithubRepos\ENGEDU\03376134_APP_IOT\Kestrel_Project
   ```
2. สั่งสร้างโปรเจกต์ใหม่ชื่อ `Kestrek_IoT_Web`:
   ```powershell
   dotnet new webapi -o Kestrek_IoT_Web --no-https
   cd Kestrek_IoT_Web
   ```
3. เปิดไฟล์ `Program.cs` ลบโค้ดเดิมออกทั้งหมด แล้วพิมพ์โค้ดชุดแรกดังนี้:

```csharp
var builder = WebApplication.CreateBuilder(args);

// กำหนดให้ Kestrel เปิดฟังทุก IP บน Port 5000
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000);
});

var app = builder.Build();

// Endpoint ทดสอบระบบ
app.MapGet("/", () => "🚀 Kestrel IoT Server Ready!");

app.Run();
```

#### 🧪 Micro-Checkpoint 1 (จุดทดสอบที่ 1):
1. ใน Terminal สั่งรันเซิร์ฟเวอร์:
   ```powershell
   dotnet run
   ```
2. เปิดเว็บเบราว์เซอร์ไปที่: **`http://localhost:5000`**
3. **ผลลัพธ์ที่ต้องปรากฏ:** หน้าจอเบราว์เซอร์ต้องแสดงข้อความ **`🚀 Kestrel IoT Server Ready!`**

---

### Recipe 8.1.2: การสร้าง Data Model และ REST API Endpoint รับข้อมูล (`POST`)

#### 📝 ขั้นตอนการปฏิบัติ:
1. กด `Ctrl + C` ใน Terminal เพื่อหยุดการรันเซิร์ฟเวอร์ก่อน
2. เปิดไฟล์ `Program.cs` เพิ่ม Class `TelemetryPayload` ด้านล่างสุดของไฟล์:

```csharp
public class TelemetryPayload
{
    public string? device_id { get; set; }
    public int potentiometer { get; set; }
    public int ldr { get; set; }
}
```

3. ย้อนขึ้นมาเพิ่ม REST API Endpoint `POST /api/telemetry` ก่อนบรรทัด `app.Run();`:

```csharp
app.MapPost("/api/telemetry", (TelemetryPayload payload) =>
{
    Console.WriteLine($"[RECV] Device: {payload.device_id} | POT: {payload.potentiometer} | LDR: {payload.ldr}");
    return Results.Ok(new { success = true, message = "Data received" });
});
```

#### 🧪 Micro-Checkpoint 2 (จุดทดสอบที่ 2):
1. สั่งรันเซิร์ฟเวอร์อีกครั้ง: `dotnet run`
2. เปิด PowerShell หน้าต่างใหม่ ยิงคำสั่งทดสอบ HTTP POST จำลองข้อมูลจาก ESP32:
   ```powershell
   Invoke-RestMethod -Uri "http://localhost:5000/api/telemetry" -Method POST -ContentType "application/json" -Body '{"device_id":"ESP32_TEST","potentiometer":2048,"ldr":1024}'
   ```
3. **ผลลัพธ์ที่ต้องปรากฏ:** บน Terminal ของ Kestrel ต้องแสดงข้อความ Log:
   `[RECV] Device: ESP32_TEST | POT: 2048 | LDR: 1024`

---

### 🧩 Hands-on Challenge 1 (โจทย์ท้าทายความคิด):
ให้นักศึกษาปรับแต่งโค้ดใน `app.MapPost("/api/telemetry")` เพื่อคำนวณแปลงค่า ADC (0 - 4095) ออกมาเป็น **แรงดันไฟฟ้า (Voltage 0.0 - 3.3V)** และ **เปอร์เซ็นต์ความเข้มแสง (0.0 - 100.0%)** ด้วยตัวเอง:

```csharp
app.MapPost("/api/telemetry", (TelemetryPayload payload) =>
{
    // 💡 ให้นักศึกษาเติมสูตรคำนวณ 2 บรรทัดนี้ด้วยตัวเอง:
    double potVoltage = Math.Round((payload.potentiometer / 4095.0) * 3.3, 2); // แปลงเป็นโวลต์
    double ldrPercent = Math.Round((payload.ldr / 4095.0) * 100.0, 1);        // แปลงเป็นเปอร์เซ็นต์

    Console.WriteLine($"[CALC] Voltage: {potVoltage}V | Light: {ldrPercent}%");
    return Results.Ok(new { success = true, voltage = potVoltage, light = ldrPercent });
});
```

---

### Recipe 8.1.3: การจัดเก็บข้อมูลบน RAM แบบ Thread-Safe (`ConcurrentQueue`)

#### 📝 ขั้นตอนการปฏิบัติ:
เนื่องจากมีโอกาสที่ ESP32 และ Web Browser จะเข้าถึงข้อมูลพร้อมๆ กัน เราจึงต้องใช้ `ConcurrentQueue` ในการเก็บข้อมูลลง RAM

1. เพิ่ม `using System.Collections.Concurrent;` ไว้บรรทัดแรกสุดของ `Program.cs`
2. เพิ่ม Class `TelemetryStore` ด้านล่างสุดของไฟล์:

```csharp
public class TelemetryRecord
{
    public string DeviceId { get; set; } = string.Empty;
    public int Potentiometer { get; set; }
    public double PotVoltage { get; set; }
    public int Ldr { get; set; }
    public double LdrPercent { get; set; }
    public string Timestamp { get; set; } = string.Empty;
}

public class TelemetryStore
{
    private readonly ConcurrentQueue<TelemetryRecord> _history = new();
    private TelemetryRecord? _latest;

    public void Add(TelemetryRecord record)
    {
        _latest = record;
        _history.Enqueue(record);
        while (_history.Count > 30) _history.TryDequeue(out _); // เก็บย้อนหลัง 30 รายการ
    }
    public TelemetryRecord? GetLatest() => _latest;
    public IEnumerable<TelemetryRecord> GetHistory() => _history.ToArray();
}
```

3. ประกาศตัวแปร `var telemetryStore = new TelemetryStore();` และสร้าง Endpoint `GET /api/telemetry/latest`:

```csharp
app.MapGet("/api/telemetry/latest", () =>
{
    var latest = telemetryStore.GetLatest();
    return Results.Ok(latest ?? new TelemetryRecord { DeviceId = "No Device Connected" });
});
```

#### 🧪 Micro-Checkpoint 3 (จุดทดสอบที่ 3):
1. รันเซิร์ฟเวอร์ `dotnet run` และยิงข้อมูล POST เข้าไปอีกครั้ง
2. เปิดเบราว์เซอร์ไปที่: **`http://localhost:5000/api/telemetry/latest`**
3. **ผลลัพธ์ที่ต้องปรากฏ:** เบราว์เซอร์จะแสดงผลลัพธ์ข้อมูลล่าสุดในรูปแบบโครงสร้าง JSON

---

### Recipe 8.1.4: การเปิดใช้งาน Static Files Middleware (เตรียมรองรับ Web Dashboard)

#### 📝 ขั้นตอนการปฏิบัติ:
1. เพิ่มคำสั่งเปิดใช้งาน Static Files ใน `Program.cs` ก่อนบรรทัด `app.Run();`:

```csharp
// เปิดใช้งาน CORS และ Static Files (wwwroot)
app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
app.UseDefaultFiles();
app.UseStaticFiles();
```

#### 🧪 Micro-Checkpoint 4 (จุดทดสอบที่ 4):
1. สร้างโฟลเดอร์ `wwwroot` ภายในโปรเจกต์ `Kestrek_IoT_Web`
2. สร้างไฟล์ `wwwroot/index.html` ใส่โค้ดทดสอบ:
   `<h1>Hello IoT Dashboard from wwwroot!</h1>`
3. รัน `dotnet run` แล้วเปิดเบราว์เซอร์ไปที่ **`http://localhost:5000`** ต้องเห็นข้อความจากไฟล์ `index.html` ทันที
