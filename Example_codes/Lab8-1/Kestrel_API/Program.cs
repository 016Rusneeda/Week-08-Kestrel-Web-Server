// ============================================================================
// Lab 8.1: Kestrel Minimal API Fundamentals
// ============================================================================
var builder = WebApplication.CreateBuilder(args);

// ตั้งค่าให้ Kestrel เปิดรับฟังบนพอร์ต 5000 (0.0.0.0:5000)
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000);
});

var app = builder.Build();

// 1. Root Endpoint
app.MapGet("/", () => "Welcome to IoT Edge Gateway by Student!");

// 2. Health & Status Endpoint (Automatic JSON Serialization)
app.MapGet("/api/status", () => new
{
    gateway = "ESP32-EdgeGateway",
    status = "Online",
    uptimeSeconds = Environment.TickCount64 / 1000,
    isHealthy = true
});

// 3. Route Parameter: Control Endpoint (e.g. GET /api/relay/1/on)
app.MapGet("/api/relay/{channel}/{state}", (int channel, string state) =>
{
    return Results.Ok(new
    {
        relay = channel,
        action = state,
        message = $"Relay {channel} set to {state.ToUpper()}",
        timestamp = DateTime.UtcNow
    });
});

app.Run();
