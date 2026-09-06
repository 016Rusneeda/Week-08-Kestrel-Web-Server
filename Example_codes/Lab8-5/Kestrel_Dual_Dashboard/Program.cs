// ==============================================================================
// Project: Kestrel Dual-Channel IoT Command Center (Lab 8-5)
// Course: Application of Internet of Things (Week 08)
// Architecture: Minimal API + BackgroundService + Dual-Sensor Telemetry
// ==============================================================================

using System.IO.Ports;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// 1. ลงทะเบียนบริการใน DI Container
builder.Services.AddSingleton<DualChannelStateStore>();
builder.Services.AddHostedService<DualSerialBridgeWorker>();

var app = builder.Build();

// 2. เปิดใช้งาน Static Files (wwwroot/index.html)
app.UseDefaultFiles();
app.UseStaticFiles();

// ==============================================================================
// 3. MINIMAL API ENDPOINTS
// ==============================================================================

// Endpoint ตรวจสอบสถานะระบบ
app.MapGet("/api/status", () => Results.Ok(new
{
    gateway = "Kestrel Dual-Channel IoT Gateway",
    version = "2.0.0",
    architecture = ".NET 9 / High-Performance Kestrel Edge",
    uptimeSeconds = Environment.TickCount64 / 1000,
    serverTime = DateTime.UtcNow.ToString("o")
}));

// Endpoint ดึงข้อมูลโทรมาตรแบบ 2 ช่องสัญญาณ (Channel A & Channel B)
app.MapGet("/api/telemetry", (DualChannelStateStore state) =>
{
    var snapshot = state.GetSnapshot();
    return Results.Ok(snapshot);
});

app.Run();

// ==============================================================================
// 4. DUAL-CHANNEL THREAD-SAFE STATE STORE
// ==============================================================================
public class DualChannelStateStore
{
    private readonly object _lock = new();

    // Channel A: ปกติรับจาก Potentiometer บนบอร์ด ESP32
    private int _rawA = 0;
    private string _nameA = "Potentiometer (Hardware)";

    // Channel B: รับจากเซนเซอร์ตัวที่ 2 (LDR) หรือสัญญาณจำลอง (Simulation)
    private int _rawB = 0;
    private string _nameB = "LDR / Room Sensor (Simulated)";

    private string _source = "Initializing";
    private DateTime _lastUpdated = DateTime.UtcNow;

    public void Update(int rawA, int rawB, string source, string? nameA = null, string? nameB = null)
    {
        lock (_lock)
        {
            _rawA = Math.Clamp(rawA, 0, 4095);
            _rawB = Math.Clamp(rawB, 0, 4095);
            _source = source;
            if (nameA != null) _nameA = nameA;
            if (nameB != null) _nameB = nameB;
            _lastUpdated = DateTime.UtcNow;
        }
    }

    public object GetSnapshot()
    {
        lock (_lock)
        {
            double voltA = Math.Round((_rawA / 4095.0) * 3.3, 2);
            double pctA = Math.Round((_rawA / 4095.0) * 100.0, 1);

            double voltB = Math.Round((_rawB / 4095.0) * 3.3, 2);
            double pctB = Math.Round((_rawB / 4095.0) * 100.0, 1);

            return new
            {
                channelA = new
                {
                    name = _nameA,
                    rawValue = _rawA,
                    voltage = voltA,
                    percentage = pctA
                },
                channelB = new
                {
                    name = _nameB,
                    rawValue = _rawB,
                    voltage = voltB,
                    percentage = pctB
                },
                dataSource = _source,
                lastUpdated = _lastUpdated.ToString("o")
            };
        }
    }
}

// ==============================================================================
// 5. BACKGROUND WORKER: DUAL SERIAL BRIDGE & GENERATOR
// ==============================================================================
public class DualSerialBridgeWorker : BackgroundService
{
    private readonly DualChannelStateStore _stateStore;
    private readonly ILogger<DualSerialBridgeWorker> _logger;

    public DualSerialBridgeWorker(DualChannelStateStore stateStore, ILogger<DualSerialBridgeWorker> logger)
    {
        _stateStore = stateStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        _logger.LogInformation("🚀 [Worker] Dual-Channel Bridge Background Service Started");

        while (!stoppingToken.IsCancellationRequested)
        {
            string[] availablePorts = SerialPort.GetPortNames();

            if (availablePorts.Length > 0)
            {
                _logger.LogInformation("📋 [Worker] พอร์ตในระบบ: [{Ports}]", string.Join(", ", availablePorts));

                // ข้าม COM1 อัตโนมัติ หรือระบุเจาะจง
                string targetPort = availablePorts.FirstOrDefault(p => !p.Equals("COM1", StringComparison.OrdinalIgnoreCase)) ?? availablePorts[0];

                _logger.LogInformation("🔌 [Worker] กำลังเชื่อมต่อไปยัง {Port}...", targetPort);

                try
                {
                    using var serial = new SerialPort(targetPort, 115200)
                    {
                        ReadTimeout = 2000,
                        DtrEnable = true,
                        RtsEnable = true
                    };

                    serial.Open();
                    serial.DiscardInBuffer();
                    _logger.LogInformation("✅ [Worker] เชื่อมต่อสำเร็จ! สตรีมข้อมูลจาก {Port}", targetPort);

                    while (!stoppingToken.IsCancellationRequested && serial.IsOpen)
                    {
                        try
                        {
                            if (serial.BytesToRead > 0)
                            {
                                string line = serial.ReadLine().Trim();

                                // กรณีส่งมา 2 ค่า คั่นด้วยจุลภาค เช่น "2400,1850"
                                if (line.Contains(','))
                                {
                                    var parts = line.Split(',');
                                    if (parts.Length >= 2 && int.TryParse(parts[0], out int valA) && int.TryParse(parts[1], out int valB))
                                    {
                                        _stateStore.Update(valA, valB, $"Live ESP32 ({targetPort})", "Potentiometer 1", "Sensor 2 (LDR/Pot)");
                                    }
                                }
                                // กรณีส่งมาค่าเดียว เช่น "2400" -> ช่อง A รับค่าจริง, ช่อง B สลับเป็นคลื่นจำลอง
                                else if (int.TryParse(line, out int valA))
                                {
                                    double t = Environment.TickCount64 / 1000.0;
                                    int simValB = (int)((Math.Sin(t * 1.2) + 1.0) / 2.0 * 4095);
                                    _stateStore.Update(valA, simValB, $"Live ESP32 ({targetPort}) + Sim B", "Potentiometer (Hardware)", "LDR / Room Sensor (Sim)");
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
                    _logger.LogWarning("⚠️ [Worker] ไม่สามารถเปิด {Port} ({Error}) -> สลับเข้าโหมดจำลอง 2 ช่อง", targetPort, ex.Message);
                }
            }

            // Fallback: Full Simulation Mode สำหรับทั้งสอง Channel
            double time = Environment.TickCount64 / 1000.0;
            int simA = (int)((Math.Sin(time * 0.8) + 1.0) / 2.0 * 4095);
            int simB = (int)((Math.Cos(time * 1.4) + 1.0) / 2.0 * 4095);
            _stateStore.Update(simA, simB, "Simulation Mode (2 Channels)", "Simulated Sensor A", "Simulated Sensor B");

            await Task.Delay(100, stoppingToken);
        }
    }
}
