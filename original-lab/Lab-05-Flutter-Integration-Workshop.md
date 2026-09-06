# ใบงานการทดลองที่ 5 (Lab 05): The Handshake — เชื่อมต่อ Kestrel กับ Flutter Mobile App
> **โครงการ LabBuddy: เวิร์กช็อปร่วม (Joint Integration) ระหว่างทีม Edge/Server และทีม Mobile App**  
> *(ส่งคลื่นเซนเซอร์จากบอร์ดทดลองผ่าน Kestrel พุ่งตรงเข้าสู่หน้าจอมือถือ Flutter แบบ Real-Time)*

---

## 🎯 วัตถุประสงค์การทดลอง (Objectives)
1. เข้าใจกระบวนการทำ **Integration Testing** (การทดสอบผสานระบบข้ามแพลตฟอร์ม) ระหว่าง C#/.NET กับ Dart/Flutter
2. สามารถตั้งค่า Kestrel ให้ฟังการเชื่อมต่อผ่าน **Local Area Network (LAN / Wi-Fi)** ด้วย `options.ListenAnyIP()` เพื่อให้มือถือเข้าถึงได้
3. เข้าใจโค้ดเชื่อมต่อฝั่ง Flutter โดยใช้ไลบรารี `signalr_netcore`
4. รู้วิธีการ Debug และวิเคราะห์ปัญหาการเชื่อมต่อข้ามเน็ตเวิร์ก (Firewall, Same Subnet, CORS)
5. สรุปความสำเร็จของระบบ LabBuddy ตั้งแต่ Hardware $\rightarrow$ Edge Server $\rightarrow$ Mobile UI

---

## 🧭 แผนผังการทดสอบร่วม (Joint Integration Architecture)

```mermaid
graph LR
    subgraph "Hardware Station"
        ESP[ESP32 / eNose] -->|USB Serial| Kestrel
    end

    subgraph "PC / Edge Gateway (C# .NET)"
        Kestrel[Kestrel Web Server<br>IP: 192.168.1.50:5000]
    end

    subgraph "Smart Phone / Tablet (Dart / Flutter)"
        Phone[📱 LabBuddy Mobile App<br>iOS / Android]
    end

    Kestrel <===>|Wi-Fi / LAN<br>WebSocket (SignalR)| Phone
```

---

## 🛠️ อุปกรณ์และสิ่งที่ต้องเตรียม
1. คอมพิวเตอร์ที่รันโปรเจกต์ Kestrel (จาก Lab 3 และ Lab 4)
2. โทรศัพท์มือถือ (Android หรือ iPhone) หรือ Android Emulator ที่ลงแอป Flutter
3. **สำคัญที่สุด:** คอมพิวเตอร์และโทรศัพท์มือถือ **ต้องเชื่อมต่อ Wi-Fi หรือ Access Point วงเดียวกัน (Same Subnet)**

---

## 🧪 ขั้นตอนที่ 1: ตั้งค่า Kestrel ให้ฟังทุก IP ในวง Wi-Fi
โดยปกติ Kestrel จะฟังเฉพาะ `localhost` (127.0.0.1) ทำให้อุปกรณ์ภายนอกอย่างมือถือมองไม่เห็น เราจึงต้องปรับให้ฟังทุก IP Address (`0.0.0.0`)

เปิดไฟล์ `Program.cs` ของ Kestrel แล้วแก้ไขส่วน `ConfigureKestrel`:

```csharp
var builder = WebApplication.CreateBuilder(args);

// ตั้งค่าให้ Kestrel ฟังคำขอจากทุก IP บนพอร์ต 5000
builder.WebHost.ConfigureKestrel(options =>
{
    // ฟังทุก Network Interface (Wi-Fi, Ethernet, Hotspot)
    options.ListenAnyIP(5000);
});

// เปิด CORS ให้ทุกอุปกรณ์ในวงแลนเข้าถึงได้
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddSignalR();
// ... ส่วนของ Worker และ Hub ตาม Lab 4 ...
```

---

## 🧪 ขั้นตอนที่ 2: ค้นหาหมายเลข IP Address ของเครื่องคอมพิวเตอร์
เปิด Terminal บนเครื่องคอมพิวเตอร์เพื่อดูหมายเลข IP:

* **บน Windows (PowerShell):**
  ```powershell
  ipconfig
  ```
  *(มองหาบรรทัด `IPv4 Address` ของ Wi-Fi Adapter เช่น `192.168.1.120`)*
* **บน Linux / macOS:**
  ```bash
  ifconfig หรือ ip a
  ```

> ⚠️ **ข้อควรระวัง (Windows Firewall):**  
> ในการทดสอบครั้งแรก Windows Defender Firewall อาจจะเด้งถาม ให้กดยอมรับ (**Allow Access**) หรือถ้ามือถือต่อไม่ได้ ให้ลองปิด Firewall บน Private Network ชั่วคราว

---

## 🧪 ขั้นตอนที่ 3: โค้ดฝั่งแอปพลิเคชัน Mobile (Flutter)
นี่คือโค้ดตัวอย่างที่เพื่อนทีม Flutter จะนำไปใช้ในโปรเจกต์ Flutter เพื่อเชื่อมต่อมายัง Kestrel ของเรา:

### 3.1 ติดตั้ง Package ฝั่ง Flutter (`pubspec.yaml`)
```yaml
dependencies:
  flutter:
    sdk: flutter
  signalr_netcore: ^1.3.9 # ไลบรารีเชื่อมต่อ SignalR สำหรับ Flutter
```

### 3.2 โค้ดเชื่อมต่อในหน้าจอแสดงผล (`sensor_screen.dart`)
```dart
import 'package:flutter/material.dart';
import 'package:signalr_netcore/signalr_netcore.dart';

class SensorScreen extends StatefulWidget {
  @override
  _SensorScreenState createState() => _SensorScreenState();
}

class _SensorScreenState extends State<SensorScreen> {
  // เปลี่ยนเป็น IP ของเครื่องคอมพิวเตอร์ที่รัน Kestrel
  final String serverUrl = "http://192.168.1.120:5000/sensorHub";
  late HubConnection hubConnection;

  double temperature = 0.0;
  double humidity = 0.0;
  int gasAdc = 0;
  String mode = "CONNECTING...";

  @override
  void initState() {
    super.initState();
    initSignalR();
  }

  void initSignalR() async {
    // 1. สร้างการเชื่อมต่อไปยัง Kestrel Hub
    hubConnection = HubConnectionBuilder()
        .withUrl(serverUrl)
        .withAutomaticReconnect()
        .build();

    // 2. ดักฟัง Event "OnReceiveTelemetry" ที่ Kestrel บรอดแคสต์มา
    hubConnection.on("OnReceiveTelemetry", (arguments) {
      if (arguments != null && arguments.isNotEmpty) {
        final data = arguments[0] as Map<String, dynamic>;
        setState(() {
          temperature = (data["temperature"] as num).toDouble();
          humidity = (data["humidity"] as num).toDouble();
          gasAdc = (data["gasAdc"] as num).toInt();
          mode = data["mode"] ?? "LIVE";
        });
      }
    });

    // 3. เริ่มต้นเชื่อมต่อ
    await hubConnection.start();
    setState(() => mode = "CONNECTED");
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text("🔬 LabBuddy Mobile Dashboard")),
      body: Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Chip(label: Text("Status: $mode")),
            SizedBox(height: 20),
            Text("Temperature: ${temperature.toStringAsFixed(1)} °C", style: TextStyle(fontSize: 24)),
            Text("Humidity: ${humidity.toStringAsFixed(1)} %RH", style: TextStyle(fontSize: 24)),
            Text("eNose Gas ADC: $gasAdc", style: TextStyle(fontSize: 28, fontWeight: FontWeight.bold, color: Colors.blue)),
          ],
        ),
      ),
    );
  }
}
```

---

## 🧪 ขั้นตอนที่ 4: การทดสอบร่วม (The Joint Test)

1. ฝั่ง Server (คุณ): สั่งรัน Kestrel ด้วยคำสั่ง `dotnet run`
2. ฝั่ง Mobile (ทีม Flutter): กดรันแอปพลิเคชันบนสมาร์ทโฟน
3. **สังเกตผล:**
   - ทันทีที่แอปบนมือถือเปิดขึ้นมา สถานะจะเปลี่ยนจาก `CONNECTING...` กลายเป็น `CONNECTED`
   - ตัวเลขบนจอมือถือจะขยับขึ้นลงตามคลื่นข้อมูลสดทันที
   - หากเสียบบอร์ด ESP32 ผ่าน USB ตัวเลขจะสลับมารับข้อมูลจริงจากเซนเซอร์โดยที่แอปไม่ต้องปิดเปิดใหม่!

---

## 🔍 ตารางช่วยแก้ปัญหาเวลาเชื่อมต่อไม่ติด (Troubleshooting Guide)

| อาการที่พบ | สาเหตุที่เป็นไปได้มากที่สุด | วิธีแก้ไข |
| :--- | :--- | :--- |
| มือถือขึ้น Error: `Connection Refused` หรือ `Timeout` | 1. มือถือและคอมฯ อยู่คนละวง Wi-Fi<br>2. Windows Firewall บล็อกพอร์ต 5000 | - ตรวจสอบ IP ให้แน่ใจว่าอยู่วง `192.168.x.x` เดียวกัน<br>- ตั้งกฎ Inbound Rule ใน Windows Firewall ให้เปิดพอร์ต 5000 |
| หน้าจอบราวเซอร์เปิดได้ แต่มือถือต่อไม่ได้ | ใน Kestrel ตั้งเป็น `localhost:5000` | แก้เป็น `options.ListenAnyIP(5000)` เพื่อให้เปิดรับ IP ภายนอก |
| ต่อติดแต่ไม่มีข้อมูลวิ่งบนมือถือ | ชื่อ Event สะกดไม่ตรงกัน | ตรวจสอบว่าฝั่ง C# ส่ง `"OnReceiveTelemetry"` และฝั่ง Dart ดักฟัง `"OnReceiveTelemetry"` ตัวพิมพ์เล็ก-ใหญ่ตรงกัน |

---

## 🏆 บทสรุปความสำเร็จของทีม LabBuddy
ยินดีด้วย! เมื่อนักศึกษาผ่านใบงานทั้ง 5 ขั้นตอนนี้ ทีมจะได้รับทักษะครบวงจร:
1. **Low-Level & Server Architecture:** เขียน Kestrel ควบคุม Socket และ Middleware เองได้
2. **Data Contract Design:** เข้าใจ JSON Schema และ API Contract ที่เป็นภาษาสากล
3. **Hardware & Edge Ingress:** เชื่อมต่อ USB Microcontroller พร้อมระบบ Failover จำลองข้อมูล
4. **Real-Time Streaming:** ส่งข้อมูลความถี่สูงระดับ WebSockets ไม่ต้องง้อ Polling
5. **Cross-Platform Collaboration:** เชื่อมต่อกับทีมพัฒนา Mobile App (Flutter) ได้อย่างไร้รอยต่อ
