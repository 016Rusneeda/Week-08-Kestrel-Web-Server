using System.IO.Ports;
var builder = WebApplication.CreateBuilder(args);
// ลงทะเบียน StateStore เป็น Singleton (มีชิ้นเดียวตลอดอายุโปรแกรม)
builder.Services.AddSingleton<TelemetryStateStore>();

// ลงทะเบียน SerialBridgeWorker เป็น Background Hosted Service
builder.Services.AddHostedService<SerialBridgeWorker>();

var app = builder.Build();


// // Endpoint หน้าแรก
// app.MapGet("/", () => "IoT Edge Gateway Online! Visit /api/telemetry to view live data.");

// Endpoint สำหรับดึงค่า Telemetry ล่าสุด
app.MapGet("/api/telemetry", (TelemetryStateStore state) =>
{
    var (raw, voltage, percent, source, updated) = state.GetSnapshot();
    return Results.Ok(new
    {
        sensor = "ESP32-Potentiometer",
        rawValue = raw,
        voltage = voltage,
        percentage = percent,
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
                // แสดงรายชื่อพอร์ตทั้งหมดที่พบ เพื่อช่วยในการ Debug
                _logger.LogInformation("📋 รายการ COM Ports ในระบบ: [{Ports}]", string.Join(", ", availablePorts));

                // เลือกระหว่าง: 
                // 1) กำหนดพอร์ตเจาะจง เช่น "COM24" (หากต้องการระบุโดยตรง)
                // 2) หรือให้ระบบเลือกพอร์ตแรกที่ไม่ใช่ COM1 อัตโนมัติ (ข้าม COM1 ซึ่งมักเป็นพอร์ตจำลองของเมนบอร์ด)
                string? targetPort = "COM24"; // <-- ใส่พอร์ตของ ESP32 ที่นี่ หรือตั้งเป็น null เพื่อให้ออโต้
                string selectedPort = targetPort ??
                                       availablePorts.FirstOrDefault(p => !p.Equals("COM1", StringComparison.OrdinalIgnoreCase)) ??
                                       availablePorts[0];

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
            // ถ้าไม่มีสายเสียบอยู่ ระบบจะสร้างคลื่น Sine Wave อัตโนมัติให้ทดสอบต่อได้
            double t = Environment.TickCount64 / 1000.0;
            int simAdc = (int)((Math.Sin(t * 1.5) + 1.0) / 2.0 * 4095);
            _stateStore.Update(simAdc, "Simulation Mode (Sine Wave)");

            await Task.Delay(100, stoppingToken);
        }
    }
}