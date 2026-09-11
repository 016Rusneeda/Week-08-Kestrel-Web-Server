# ใบงานการทดลองที่ 8.4 (Labsheet 8.4)
#### แดชบอร์ดมาตรวัดความเร็ว SVG และสนามทดลองสร้างสรรค์ (Creative Playground)
## 📋 ส่งงานและประเมินผล (Submission Check)
1. บันทึกวิดีโอคลิปสั้น (15-30 วินาที) โดยในคลิปต้องเห็น:
   - นิ้วมือนักศึกษากำลังหมุนตัวต้านทานปรับค่าได้บนบอร์ด ESP32
   - หน้าจอคอมพิวเตอร์ที่เข็มไมล์ Speedometer / VU Meter กวาดตามมืออย่างชัดเจน
2. แนบภาพหน้าจอซอร์สโค้ดและรายงานการทดลอง
### ผลการทดลอง

https://github.com/user-attachments/assets/b3875d0f-18d3-4f13-93a3-bd986ff1fa76

<img width="1532" height="817" alt="image" src="https://github.com/user-attachments/assets/d3f45af3-f0e2-4430-9b5b-cf9ee5cfec25" />
<img width="1527" height="817" alt="image" src="https://github.com/user-attachments/assets/011e25f7-4161-49b2-9908-431194e2f3b9" />

#### source code
HTML
```
<!DOCTYPE html>
<html lang="th">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>ESP32 IoT Interactive Gateway Dashboard</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            background: radial-gradient(circle at center, #1b263b 0%, #0d1b2a 100%);
            color: #e0e1dd;
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            min-height: 100vh;
            padding: 20px;
        }
        .container {
            background: rgba(255, 255, 255, 0.05);
            backdrop-filter: blur(10px);
            border: 1px solid rgba(255, 255, 255, 0.1);
            border-radius: 24px;
            padding: 30px;
            box-shadow: 0 20px 50px rgba(0, 0, 0, 0.5);
            text-align: center;
            max-width: 500px;
            width: 100%;
        }
        h1 { font-size: 1.5rem; margin-bottom: 5px; color: #00f2fe; }
        .subtitle { font-size: 0.85rem; color: #778da9; margin-bottom: 25px; }
        
        /* การ์ดสถิติ */
        .stats-grid {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 15px;
            margin-top: 25px;
        }
        .stat-card {
            background: rgba(13, 27, 42, 0.6);
            border: 1px solid rgba(255, 255, 255, 0.08);
            border-radius: 12px;
            padding: 12px;
        }
        .stat-label { font-size: 0.75rem; color: #778da9; text-transform: uppercase; }
        .stat-val { font-size: 1.4rem; font-weight: bold; color: #4cc9f0; margin-top: 4px; }
        .source-badge {
            display: inline-block;
            margin-top: 15px;
            padding: 6px 14px;
            border-radius: 20px;
            font-size: 0.75rem;
            background: rgba(0, 242, 254, 0.1);
            color: #00f2fe;
            border: 1px solid rgba(0, 242, 254, 0.3);
        }

        /* แอนิเมชันให้ระดับน้ำเคลื่อนที่ขึ้นลงอย่างนุ่มนวล */
        #water-fill {
            transition: y 0.15s ease-out, height 0.15s ease-out;
        }
    </style>
</head>
<body>

<div class="container">
    <h1>🛢️ IoT Edge Liquid Tank</h1>
    <div class="subtitle">ESP32 Hardware Stream &bull; Kestrel Edge Web Server</div>

    <!-- ถังระดับของเหลวอุตสาหกรรม (Liquid Level Tank) -->
    <svg width="220" height="220" viewBox="0 0 220 220" id="liquid-tank">
        <defs>
            <!-- การไล่เฉดสีของของเหลวสีฟ้าเรืองแสง (Neon Cyan to Blue) -->
            <linearGradient id="water-grad" x1="0%" y1="0%" x2="0%" y2="100%">
                <stop offset="0%" stop-color="#00f2fe" stop-opacity="0.95"/>
                <stop offset="100%" stop-color="#4facfe" stop-opacity="0.8"/>
            </linearGradient>

            <!-- คลิปพาร์ททรงถังน้ำขอบมน (ป้องกันน้ำล้นออกนอกถัง) -->
            <clipPath id="tank-shape">
                <rect x="60" y="25" width="80" height="135" rx="18"/>
            </clipPath>
        </defs>

        <!-- เงาพื้นหลังของถัง (ความลึกด้านใน) -->
        <rect x="60" y="25" width="80" height="135" rx="18" fill="#07101e" stroke="#1e293b" stroke-width="2"/>

        <!-- ระดับของเหลว (ถูกครอบด้วย clip-path ทรงถัง) -->
        <g clip-path="url(#tank-shape)">
            <rect id="water-fill" x="60" y="160" width="80" height="0" fill="url(#water-grad)" filter="drop-shadow(0 0 8px #00f2fe)"/>
        </g>

        <!-- ขอบกระจกถังน้ำ (Glass Body Outline) -->
        <rect x="60" y="25" width="80" height="135" rx="18" fill="none" stroke="#38bdf8" stroke-width="3" opacity="0.6"/>

        <!-- แถบสะท้อนแสงผิวกระจกด้านข้าง (Glossy Reflection) -->
        <rect x="66" y="32" width="6" height="120" rx="3" fill="#ffffff" opacity="0.15"/>

        <!-- สเกลวัดระดับขีดเปอร์เซ็นต์ (Scale Markings) -->
        <!-- 100% -->
        <line x1="145" y1="25" x2="155" y2="25" stroke="#94a3b8" stroke-width="2"/>
        <text x="162" y="29" fill="#94a3b8" font-size="11" font-family="sans-serif">100%</text>

        <!-- 75% -->
        <line x1="145" y1="59" x2="151" y2="59" stroke="#64748b" stroke-width="1.5"/>
        <text x="162" y="63" fill="#64748b" font-size="10" font-family="sans-serif">75%</text>

        <!-- 50% -->
        <line x1="145" y1="92" x2="155" y2="92" stroke="#94a3b8" stroke-width="2"/>
        <text x="162" y="96" fill="#94a3b8" font-size="11" font-family="sans-serif">50%</text>

        <!-- 25% -->
        <line x1="145" y1="126" x2="151" y2="126" stroke="#64748b" stroke-width="1.5"/>
        <text x="162" y="130" fill="#64748b" font-size="10" font-family="sans-serif">25%</text>

        <!-- 0% -->
        <line x1="145" y1="160" x2="155" y2="160" stroke="#94a3b8" stroke-width="2"/>
        <text x="162" y="164" fill="#94a3b8" font-size="11" font-family="sans-serif">0%</text>

        <!-- ตัวเลขแสดงเปอร์เซ็นต์แบบดิจิทัลใต้ถังน้ำ -->
        <text id="tank-val" x="100" y="195" text-anchor="middle" fill="#00f2fe" font-family="'Segoe UI', sans-serif" font-size="22" font-weight="bold">0.0%</text>
        <text x="100" y="212" text-anchor="middle" fill="#64748b" font-family="sans-serif" font-size="11">TANK LEVEL</text>
    </svg>

    <div class="stats-grid">
        <div class="stat-card">
            <div class="stat-label">ADC 12-Bit Raw</div>
            <div class="stat-val" id="disp-raw">0</div>
        </div>
        <div class="stat-card">
            <div class="stat-label">Sensor Voltage</div>
            <div class="stat-val" id="disp-volt">0.00 V</div>
        </div>
    </div>

    <div class="source-badge" id="disp-source">Connecting to Server...</div>
</div>

<script>
    // ฟังก์ชันคำนวณและอัปเดตระดับของเหลวในถัง
    function updateTankLevel(percentage) {
        const maxHeight = 135;   // ความสูงสูงสุดของถัง (px)
        const tankBottomY = 160; // พิกัดแกน Y ก้นถัง

        // จำกัดขอบเขต 0 - 100%
        const clampedPct = Math.min(100, Math.max(0, percentage));

        // คำนวณความสูงและพิกัด Y ของระดับน้ำ
        const fillHeight = (clampedPct / 100.0) * maxHeight;
        const fillY = tankBottomY - fillHeight;

        const water = document.getElementById('water-fill');
        if (water) {
            water.setAttribute('height', fillHeight);
            water.setAttribute('y', fillY);
        }

        // อัปเดตตัวเลขเปอร์เซ็นต์ดิจิทัลใต้ถัง
        const tankText = document.getElementById('tank-val');
        if (tankText) {
            tankText.textContent = clampedPct.toFixed(1) + '%';
        }
    }

    async function pollTelemetry() {
        try {
            const response = await fetch('/api/telemetry');
            if (!response.ok) return;

            const data = await response.json();

            // 1. อัปเดตการ์ดตัวเลขและสถานะด้านล่าง
            const dispRaw = document.getElementById('disp-raw');
            if (dispRaw) dispRaw.textContent = data.rawValue;

            const dispVolt = document.getElementById('disp-volt');
            if (dispVolt) dispVolt.textContent = data.voltage.toFixed(2) + ' V';

            const dispSource = document.getElementById('disp-source');
            if (dispSource) dispSource.textContent = '📡 ' + data.dataSource;

            // 2. เรียกฟังก์ชันอัปเดตระดับน้ำในถัง
            updateTankLevel(data.percentage);

        } catch (err) {
            console.error('Polling error:', err);
        }
    }

    // วนลูปดึงข้อมูลทุกๆ 150 มิลลิวินาที
    setInterval(pollTelemetry, 150);
</script>

</body>
</html>
```

Program.cs
```
using System.IO.Ports;
var builder = WebApplication.CreateBuilder(args);

// ลงทะเบียน StateStore เป็น Singleton (มีชิ้นเดียวตลอดอายุโปรแกรม)
builder.Services.AddSingleton<TelemetryStateStore>();

// ลงทะเบียน SerialBridgeWorker เป็น Background Hosted Service
builder.Services.AddHostedService<SerialBridgeWorker>();

var app = builder.Build();


// Endpoint สำหรับดึงค่า Telemetry ล่าสุด พร้อมระบบแจ้งเตือน (Micro-Challenge)
app.MapGet("/api/telemetry", (TelemetryStateStore state) => {
    var (raw, voltage, percent, source, updated) = state.GetSnapshot();
    
    // 1. คำนวณระดับการแจ้งเตือนตามเงื่อนไขของโจทย์
    string currentAlert = "NORMAL";
    if (percent > 85.0) 
    {
        currentAlert = "DANGER (HIGH)";
    } 
    else if (percent >= 70.0) 
    {
        currentAlert = "WARNING";
    }

    return Results.Ok(new {
        sensor = "ESP32-Potentiometer",
        rawValue = raw,
        voltage = voltage,
        percentage = percent,
        alertLevel = currentAlert, // 2. เพิ่มฟิลด์ alertLevel ส่งออกไปใน JSON
        dataSource = source,
        timestamp = updated.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
    });
});
app.UseFileServer();
app.Run();
// ============================================================================
// 1. THREAD-SAFE STATE STORE (คลังเก็บค่าสถานะกลาง)
// ============================================================================
public class TelemetryStateStore
{
    private readonly object _lock = new();
    private int _rawValue = 0;
    private DateTime _lastUpdated = DateTime.UtcNow;
    private string _source = "Initializing";

    public void Update(int rawValue, string source)
    {
        lock (_lock)
        {
            _rawValue = rawValue;
            _source = source;
            _lastUpdated = DateTime.UtcNow;
        }
    }

    public (int raw, double voltage, double percent, string source, DateTime updated) GetSnapshot()
    {
        lock (_lock)
        {
            double voltage = Math.Round((_rawValue / 4095.0) * 3.3, 2);
            double percent = Math.Round((_rawValue / 4095.0) * 100.0, 1);
            return (_rawValue, voltage, percent, _source, _lastUpdated);
        }
    }
}
// ============================================================================
// 2. BACKGROUND WORKER (คนงานดักฟังข้อมูลจากสาย USB)
// ============================================================================
public class SerialBridgeWorker : BackgroundService
{
    private readonly TelemetryStateStore _stateStore;
    private readonly ILogger<SerialBridgeWorker> _logger;

    public SerialBridgeWorker(TelemetryStateStore stateStore, ILogger<SerialBridgeWorker> logger)
    {
        _stateStore = stateStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // คืนสิทธิ์ให้ Host สามารถเริ่ม Kestrel Web Server ได้อย่างราบรื่น ไม่ติดบล็อก
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            // 1. ค้นหาพอร์ต COM ทั้งหมดที่มีอยู่ในเครื่อง
            string[] availablePorts = SerialPort.GetPortNames();
            
            if (availablePorts.Length > 0)
            {
                _logger.LogInformation("📋 รายการ COM Ports ในระบบ: [{Ports}]", string.Join(", ", availablePorts));

                // เลือกระหว่าง:
                // 1) ระบุพอร์ตเจาะจง เช่น "COM24" (ถ้ามีอยู่ในระบบ)
                // 2) หรือให้ระบบเลือกพอร์ตแรกที่ไม่ใช่ COM1 อัตโนมัติ
                string? targetPort = "COM24"; // <-- ใส่พอร์ตที่ต้องการ หรือตั้งเป็น null เพื่อให้ออโต้
                
                string? selectedPort = null;
                if (!string.IsNullOrEmpty(targetPort) && availablePorts.Contains(targetPort, StringComparer.OrdinalIgnoreCase))
                {
                    selectedPort = targetPort;
                }
                else
                {
                    if (!string.IsNullOrEmpty(targetPort))
                    {
                        _logger.LogWarning("⚠️ ไม่พบพอร์ต {Target} ในระบบ! ระบบจะเลือกพอร์ตอื่นให้อัตโนมัติ...", targetPort);
                    }
                    selectedPort = availablePorts.FirstOrDefault(p => !p.Equals("COM1", StringComparison.OrdinalIgnoreCase)) ?? availablePorts[0];
                }

                _logger.LogInformation("🔌 กำลังทดลองเชื่อมต่อพอร์ต: {Port}", selectedPort);

                try
                {
                    using var serial = new SerialPort(selectedPort, 115200);
                    serial.ReadTimeout = 2000;
                    serial.Open();
                    serial.DiscardInBuffer(); // เคลียร์ขยะเก่าในบัฟเฟอร์
                    _logger.LogInformation("✅ เชื่อมต่อฮาร์ดแวร์สำเร็จบน {Port} (Live Mode)", selectedPort);

                    while (!stoppingToken.IsCancellationRequested && serial.IsOpen)
                    {
                        try
                        {
                            if (serial.BytesToRead > 0)
                            {
                                string line = serial.ReadLine().Trim();
                                if (int.TryParse(line, out int val))
                                {
                                    _stateStore.Update(val, $"Live Hardware ({selectedPort})");
                                }
                            }
                            else
                            {
                                await Task.Delay(50, stoppingToken);
                            }
                        }
                        catch (TimeoutException) 
                        { 
                            await Task.Delay(50, stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("⚠️ ไม่สามารถเปิด {Port} ({Msg}) -> สลับเข้าโหมดจำลอง", selectedPort, ex.Message);
                }
            }
            else
            {
                _logger.LogInformation("🔍 ไม่พบพอร์ต USB -> ทำงานในโหมดจำลอง (Simulation Mode)");
            }

            // 2. โหมดจำลองสัญญาณอัตโนมัติ (Fallback Simulation Mode)
            // วนจำลองสัญญาณ 2 วินาที (20 รอบ รอบละ 100ms) ก่อนจะกลับไปตรวจเช็คพอร์ตใหม่ เพื่อไม่ให้ Log แสดงผลรัวเกินไป
            for (int i = 0; i < 20 && !stoppingToken.IsCancellationRequested; i++)
            {
                double t = Environment.TickCount64 / 1000.0;
                int simAdc = (int)((Math.Sin(t * 1.5) + 1.0) / 2.0 * 4095);
                _stateStore.Update(simAdc, "Simulation Mode (Sine Wave)");
                await Task.Delay(100, stoppingToken);
            }
        }
    }
}
```
