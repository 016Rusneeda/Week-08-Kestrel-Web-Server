// ============================================================================
// Lab 8.3: Hardware Serial Bridge to Web (Kestrel Serial Gateway)
// ============================================================================
using System.IO.Ports;

var builder = WebApplication.CreateBuilder(args);

// ตั้งค่าให้ Kestrel เปิดรับฟังบนพอร์ต 5000 ทุก IP
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000);
});

// เปิดใช้งาน CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ลงทะเบียน State Store และ Background Worker
builder.Services.AddSingleton<TelemetryStateStore>();
builder.Services.AddHostedService<SerialBridgeWorker>();

var app = builder.Build();

app.UseCors("AllowAll");

// ----------------------------------------------------------------------------
// API Endpoints
// ----------------------------------------------------------------------------
app.MapGet("/api/ping", () => "pong");

app.MapGet("/api/telemetry", (TelemetryStateStore stateStore) =>
{
    var snapshot = stateStore.GetSnapshot();
    return Results.Ok(new
    {
        sensor = "ESP32-Potentiometer",
        rawValue = snapshot.raw,
        voltage = snapshot.voltage,
        percentage = snapshot.percent,
        source = snapshot.source,
        timestamp = snapshot.updated
    });
});

app.MapGet("/api/status", (TelemetryStateStore stateStore) =>
{
    var snapshot = stateStore.GetSnapshot();
    return Results.Ok(new
    {
        gateway = "ESP32-Kestrel-Bridge",
        uptimeSeconds = Environment.TickCount64 / 1000,
        hardwareStatus = snapshot.source,
        lastReadingTime = snapshot.updated
    });
});

app.Run();

// ============================================================================
// 1. THREAD-SAFE STATE STORE
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
// 2. BACKGROUND WORKER (System.IO.Ports + Fallback Simulation)
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
        _logger.LogInformation("🚀 Serial Bridge Worker is starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            string? targetPort = FindTargetPort();

            if (targetPort != null)
            {
                _logger.LogInformation("🔌 Attempting to connect to hardware on port {Port}...", targetPort);
                try
                {
                    using var serial = new SerialPort(targetPort, 115200)
                    {
                        ReadTimeout = 2000,
                        NewLine = "\n"
                    };
                    serial.Open();
                    serial.DiscardInBuffer();
                    _logger.LogInformation("✅ Connected to hardware on {Port}! Streaming telemetry...", targetPort);

                    while (!stoppingToken.IsCancellationRequested)
                    {
                        try
                        {
                            string line = serial.ReadLine().Trim();
                            if (int.TryParse(line, out int adcVal))
                            {
                                int clamped = Math.Clamp(adcVal, 0, 4095);
                                _stateStore.Update(clamped, $"Hardware ({targetPort})");
                            }
                        }
                        catch (TimeoutException)
                        {
                            // ไม่ได้รับข้อมูลใน 2 วินาที รออ่านใหม่
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("⚠️ Serial Port {Port} disconnected or error: {Msg}. Falling back to simulation...", targetPort, ex.Message);
                }
            }
            else
            {
                _logger.LogWarning("⚠️ No hardware COM port found. Running in Fallback Simulation Mode...");
                await RunSimulationLoopAsync(stoppingToken);
            }

            await Task.Delay(1000, stoppingToken);
        }
    }

    private string? FindTargetPort()
    {
        string[] ports = SerialPort.GetPortNames();
        if (ports.Length == 0) return null;

        _logger.LogInformation("📋 รายการ COM Ports ในระบบ: [{Ports}]", string.Join(", ", ports));

        // เลือกระหว่าง: กำหนดพอร์ตเจาะจง หรือ ข้าม COM1 อัตโนมัติ
        const string? explicitPort = null; // สามารถระบุ เช่น "COM24" ได้ตามต้องการ
        return explicitPort ?? ports.FirstOrDefault(p => !p.Equals("COM1", StringComparison.OrdinalIgnoreCase)) ?? ports[0];
    }

    private async Task RunSimulationLoopAsync(CancellationToken stoppingToken)
    {
        double angle = 0.0;
        int simCheckCounter = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            // จำลองคลื่น Sine wave นุ่มนวลช่วง 200 - 3800
            int simValue = (int)(2000 + 1800 * Math.Sin(angle));
            _stateStore.Update(simValue, "Fallback Simulator (Sine Wave)");

            angle += 0.08;
            if (angle > Math.PI * 2) angle = 0;

            simCheckCounter++;
            if (simCheckCounter >= 25) // ทุก ~2.5 วินาที ตรวจสอบว่ามีบอร์ดมาเสียบหรือยัง
            {
                if (FindTargetPort() != null)
                {
                    _logger.LogInformation("🔍 Detected hardware! Exiting simulation mode...");
                    return;
                }
                simCheckCounter = 0;
            }

            await Task.Delay(100, stoppingToken);
        }
    }
}
