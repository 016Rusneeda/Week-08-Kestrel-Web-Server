# ใบงานการทดลองที่ 1 (Lab 01): Kestrel Web Server & Network Fundamentals
> **โครงการ LabBuddy: พื้นฐานสถาปัตยกรรมเว็บเซิร์ฟเวอร์ และการทำงานระดับ Socket**  
> *(เปรียบเทียบเชิงแนวคิดกับภาษา C: Header, Library, Entry Point, Socket และ Request Delegate)*

---

## 🎯 วัตถุประสงค์การทดลอง (Objectives)
1. เข้าใจโครงสร้างโปรเจกต์ .NET Web App และสถาปัตยกรรมภายในของ Kestrel Web Server
2. เข้าใจการเทียบเคียงแนวคิดระหว่าง **C Programming** กับ **Modern C# / Kestrel** (`#include` vs `using`, `Makefile` vs `.csproj`, `main()` vs `Top-level statements`, `socket()` vs `Kestrel Listeners`)
3. สามารถกำหนดค่า Socket Endpoints, Listening Ports, Protocol (HTTP/1.1, HTTP/2) และ Server Limits ได้ด้วยตนเอง
4. เข้าใจกลไกการรับส่งข้อมูลผ่าน `HttpContext` (Headers, Request/Response, Buffer Streams)
5. สามารถเขียน Middleware (Pipeline Callback Functions) เพื่อดักจับและวัดประสิทธิภาพของระบบได้

---

## 🧭 ตารางเปรียบเทียบแนวคิด: C Language vs C# Kestrel

| มิติการทำงาน | ภาษา C (POSIX / Socket Programming) | C# Kestrel Web Server |
| :--- | :--- | :--- |
| **Build Spec / Libs** | `Makefile` / `gcc -lws2_32 -I...` | `.csproj` (`<Project Sdk="Microsoft.NET.Sdk.Web">`) |
| **Header Files** | `#include <stdio.h>`, `#include <sys/socket.h>` | `using Microsoft.AspNetCore.Builder;`, `using Microsoft.AspNetCore.Hosting;` |
| **Entry Point** | `int main(int argc, char* argv[])` | `public static void Main(string[] args)` หรือ Top-Level statements |
| **Create & Bind Socket** | `socket()`, `bind()`, `listen()` | `options.ListenLocalhost(5000)` / `options.ListenAnyIP(...)` |
| **Event Loop / Accept** | `accept()` + `select()` / `epoll()` loop | `KestrelServer` + `System.IO.Pipelines` (Non-blocking I/O) |
| **Request / Response** | `char buffer[1024]; recv(); send();` | `HttpContext context` (`context.Request`, `context.Response`) |
| **Function Callback** | Function Pointer (`void (*handler)(int sock)`) | Request Delegate / Middleware (`async (context, next) => { ... }`) |

---

## 🛠️ อุปกรณ์และซอฟต์แวร์ที่ต้องใช้
- เครื่องคอมพิวเตอร์ที่ติดตั้ง **.NET SDK 8.0** หรือ **9.0** ขึ้นไป
- Terminal / PowerShell
- Web Browser หรือคำสั่ง `curl`

---

## 🧪 ขั้นตอนที่ 1: การสร้างโปรเจกต์ Bare-Metal Kestrel
เปิด Terminal แล้วรันคำสั่ง:
```bash
# 1. สร้างโฟลเดอร์สำหรับ Lab 1
mkdir LabBuddy_Lab01 && cd LabBuddy_Lab01

# 2. สร้างโปรเจกต์แบบ Web ที่ว่างเปล่า (Empty Web)
dotnet new web -o .
```

เปิดดูไฟล์ `.csproj` เพื่อดูการ Link Library:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```
> 🔍 **คำอธิบาย:** `Sdk="Microsoft.NET.Sdk.Web"` คือตัว Link ชุดคำสั่งและไลบรารีของ Kestrel Web Server เข้ามาทั้งหมดโดยอัตโนมัติ

---

## 🧪 ขั้นตอนที่ 2: เขียน Entry Point และ Socket Binding (C-Style Explicit)
เปิดไฟล์ `Program.cs` แล้วแทนที่ด้วยโค้ดด้านล่างเพื่อศึกษาโครงสร้างแบบชัดเจน:

```csharp
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace LabBuddy
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("=== [LabBuddy] Starting Kestrel Gateway Engine ===");

            // Step 1: สร้าง WebApplicationBuilder
            var builder = WebApplication.CreateBuilder(args);

            // Step 2: คอนฟิก Kestrel Socket Listeners โดยตรง (เทียบเท่า bind/listen ใน C)
            builder.WebHost.ConfigureKestrel(options =>
            {
                // ฟังการเชื่อมต่อที่พอร์ต 5000 รองรับทั้ง HTTP/1.1 และ HTTP/2
                options.ListenLocalhost(5000, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                });
            });

            // Step 3: Build เป็น Server Instance
            var app = builder.Build();

            // Step 4: ผูก Endpoint พื้นฐาน
            app.MapGet("/", async (HttpContext context) =>
            {
                context.Response.ContentType = "text/plain; charset=utf-8";
                await context.Response.WriteAsync("LabBuddy Gateway is Running on Kestrel!");
            });

            // Step 5: เริ่มต้น Event Loop
            await app.RunAsync();
        }
    }
}
```

---

## 🧪 ขั้นตอนที่ 3: ตรวจสอบและอ่านค่า `HttpContext`
ใน Kestrel เมื่อมีไคลเอนต์ส่ง Request เข้ามา ออบเจ็กต์ `HttpContext` จะเก็บข้อมูลทั้งหมดไว้ ทั้ง Headers, IP, และ Query Parameters

เพิ่ม Endpoint `/inspect` ใน `Program.cs` ก่อนคำสั่ง `app.RunAsync()`:

```csharp
app.MapGet("/inspect", async (HttpContext context) =>
{
    string clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
    string userAgent = context.Request.Headers["User-Agent"].ToString();
    string protocol = context.Request.Protocol;

    // เพิ่ม Custom Header ตอบกลับไป
    context.Response.Headers.Append("X-Server-By", "LabBuddy-Kestrel");

    var summary = $"=== Client Connection Metadata ===\n" +
                  $"IP Address: {clientIp}\n" +
                  $"Protocol:   {protocol}\n" +
                  $"User-Agent: {userAgent}\n";

    context.Response.ContentType = "text/plain; charset=utf-8";
    await context.Response.WriteAsync(summary);
});
```

---

## 🧪 ขั้นตอนที่ 4: สร้าง Middleware Pipeline (Callback Chain)
ก่อนที่ Request จะวิ่งไปถึง Endpoint เราสามารถเขียน **Middleware** ดักจับข้อมูลได้ (เทียบเท่ากับการส่งต่อ Function Pointer ในภาษา C)

เพิ่มโค้ด Middleware นี้ก่อนบรรทัด `app.MapGet(...)`:

```csharp
// Middleware จับเวลาและพิมพ์ Log ของทุก Request
app.Use(async (HttpContext context, Func<Task> next) =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    Console.WriteLine($"--> [INCOMING] {context.Request.Method} {context.Request.Path}");

    // ส่งต่อไปยัง Middleware ตัวถัดไปหรือ Endpoint
    await next.Invoke();

    sw.Stop();
    Console.WriteLine($"<-- [COMPLETED] {context.Request.Path} in {sw.ElapsedMilliseconds} ms (Status: {context.Response.StatusCode})");
});
```

---

## 🧪 ขั้นตอนที่ 5: การทดสอบผลลัพธ์
1. รันเซิร์ฟเวอร์:
   ```bash
   dotnet run
   ```
2. ทดสอบเรียกผ่าน `curl` หรือเปิดบราวเซอร์ไปที่ `http://localhost:5000/inspect`:
   ```bash
   curl -i http://localhost:5000/inspect
   ```
3. สังเกต Log บน Terminal ที่ Kestrel พิมพ์ออกมาพร้อม Response Headers

---

## 🎯 ภารกิจท้าทายประจำแล็บ (Lab Challenge)
- ให้นักศึกษาเพิ่ม Query Parameter `?sensor=gas` ใน Route `/inspect` และให้อ่านค่าจาก `context.Request.Query["sensor"]` มาแสดงผลในข้อความตอบกลับ
