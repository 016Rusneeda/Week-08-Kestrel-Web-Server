# บทเรียนที่ 8.2: การติดตั้ง .NET SDK และโครงสร้างโปรเจกต์ ASP.NET Core Minimal API

## 1. การตรวจสอบและติดตั้ง .NET SDK

ในการพัฒนา Web Application ด้วยภาษา C# และ Kestrel Web Server จำเป็นต้องมี **.NET SDK (Software Development Kit)** ติดตั้งอยู่บนเครื่องคอมพิวเตอร์

### 1.1 การตรวจสอบ .NET SDK ผ่าน Command Line
เปิด Terminal (Command Prompt หรือ PowerShell) แล้วพิมพ์คำสั่ง:

```powershell
dotnet --version
```

หากติดตั้งเรียบร้อยแล้ว หน้าจอจะแสดงหมายเลขเวอร์ชัน เช่น:
```text
10.0.400  (หรือ 8.0.x / 9.0.x)
```

หากต้องการดูรายการ SDK ทั้งหมดในเครื่อง สามารถใช้คำสั่ง:
```powershell
dotnet --list-sdks
```

> [!TIP]
> หากยังไม่ได้ติดตั้ง สามารถดาวน์โหลด .NET SDK เวอร์ชันล่าสุด (LTS) ได้ฟรีจากเว็บไซต์ทางการของ Microsoft: [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)

---

## 2. การสร้างโปรเจกต์ Minimal API ด้วย dotnet CLI

เราสามารถใช้คำสั่ง `dotnet new` เพื่อสร้างโครงสร้างโปรเจกต์ C# Web API ได้อย่างรวดเร็วผ่าน Command Line:

```powershell
# 1. เข้าไปในโฟลเดอร์ทำงาน
cd d:\GithubRepos\ENGEDU\03376134_APP_IOT\Kestrel_Project

# 2. สร้างโปรเจกต์ Web API แบบ Minimal
dotnet new webapi -o Kestrel_Iot_Web --no-https

# 3. เข้าไปในโฟลเดอร์โปรเจกต์
cd Kestrel_Iot_Web
```

### โครงสร้างไฟล์ในโปรเจกต์ (Project Directory Structure)
```text
Kestrel_Iot_Web/
├── Kestrel_Iot_Web.csproj   # ไฟล์คอนฟิกโปรเจกต์ C# (Target Framework, Packages)
├── Program.cs               # ไฟล์หลักควบคุม Web Server และ REST API Endpoints
├── appsettings.json         # ไฟล์บันทึกค่าคอนฟิกระบบและ Logging Levels
└── wwwroot/                 # โฟลเดอร์สำหรับเก็บไฟล์ Static Web (HTML, CSS, JS, Images)
    ├── index.html
    ├── css/
    │   └── style.css
    └── js/
        └── dashboard.js
```

---

## 3. การกำหนดค่า Kestrel Web Server ให้เปิดรับการเชื่อมต่อจากภายนอก

ตามค่าเริ่มต้น Kestrel จะเปิดฟังเฉพาะ Local Loopback (`localhost` หรือ `127.0.0.1`) ซึ่งทำให้ ESP32 ที่อยู่ในวง Wi-Fi เดียวกันไม่สามารถยิง HTTP Request เข้ามาได้

ดังนั้น เราต้องกำหนดให้ Kestrel เปิดรับฟังทุก IP (`0.0.0.0`) บน Port 5000 ในไฟล์ `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

// ตั้งค่าให้ Kestrel เปิดฟังทุก IP Address ในวง LAN
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000); // Binding to 0.0.0.0:5000
});

// เพิ่ม CORS เพื่อให้ Web Browser เรียก API ข้าม Domain ได้
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();
app.UseCors("AllowAll");

// เปิดใช้งาน Static Files ใน wwwroot/
app.UseDefaultFiles();
app.UseStaticFiles();
```

---

## 4. การออกแบบ In-Memory Data Store ด้วย Concurrent Collections

สำหรับการรับข้อมูล Telemetry จาก ESP32 ที่ยิงเข้ามาเรื่อยๆ การเขียนลงฐานข้อมูลดิสก์ทุกวินาทีอาจส่งผลกระทบต่อ I/O performance ในขั้นเริ่มต้น เราจึงนิยมใช้ **In-Memory Buffer** บน RAM

และเนื่องจาก Kestrel ทำงานแบบ Multi-threaded (มีโอกาสที่ ESP32 ยิง POST เข้ามาพร้อมๆ กับ Web Dashboard ยิง GET เข้ามา) เราจึงต้องใช้ Class ใน Namespace `System.Collections.Concurrent` เช่น **`ConcurrentQueue<T>`** เพื่อความปลอดภัยทางด้าน Thread Safety (Thread-safe data structure):

```csharp
public class TelemetryStore
{
    private readonly ConcurrentQueue<TelemetryRecord> _history = new();
    private TelemetryRecord? _latest = null;
    private const int MaxHistory = 30; // เก็บย้อนหลังสูงสุด 30 รายการ

    public void Add(TelemetryRecord record)
    {
        _latest = record;
        _history.Enqueue(record);
        
        // หากเกิน 30 รายการ ให้ตัดรายการเก่าสุดออก
        while (_history.Count > MaxHistory)
        {
            _history.TryDequeue(out _);
        }
    }

    public TelemetryRecord? GetLatest() => _latest;
    public IEnumerable<TelemetryRecord> GetHistory() => _history.ToArray();
}
```

---

## 5. คำสั่งที่สำคัญในการรันและทดสอบ Kestrel Web Server

* **การสั่ง Build ตรวจสอบข้อผิดพลาด:**
  ```powershell
  dotnet build
  ```
* **การสั่งรัน Web Server:**
  ```powershell
  dotnet run
  ```
* **การรันในโหมด Hot Reload (แก้ไขโค้ดแล้วอัปเดตอัตโนมัติไม่ต้องปิดเปิดใหม่):**
  ```powershell
  dotnet watch
  ```
