# ใบงานที่ 8.3: Real-time Dynamic SVG & VU Meter Dashboard Cookbook

## 0. กล่าวนำ (Introduction)
ใบงานนี้จะพานักศึกษาลงมือสร้าง **Web Dashboard** ยุคใหม่สไตล์ Glassmorphism ในรูปแบบ **Cookbook Step-by-Step** โดยการสร้างภาพเวกเตอร์ **SVG Radial Gauge** (เกจวัดเข็มหมุน) สำหรับ Potentiometer และ **Audio VU Meter** (มาตรวัดหลอดไฟ LED) สำหรับ LDR Sensor และเชื่อมต่อข้อมูลเรียลไทม์ผ่าน JavaScript

---

## 1. วัตถุประสงค์ (Objectives)
1. เข้าใจโครงสร้าง HTML5 ร่วมกับแท็กเวกเตอร์กราฟิก `<svg>`, `<path>`, `<line>`
2. สามารถใช้ CSS Styling ตกแต่งองค์ประกอบสไตล์ Glassmorphic และกำหนดการเคลื่อนไหวแบบนุ่มนวลด้วย Transitions
3. สามารถเขียนคำนวณสูตรทางคณิตศาสตร์ใน JavaScript เพื่อเปลี่ยนค่า `stroke-dashoffset` และมุมหมุนเข็ม Gauge
4. สามารถเชื่อมต่อ Polling Fetch API ดึงข้อมูลจาก Kestrel Server มาอัปเดตกราฟิกทุกๆ 1 วินาที

---

## 2. ขั้นตอนการปฏิบัติการแบบ Cookbook (Step-by-Step Recipes)

---

### Recipe 8.3.1: โครงสร้าง HTML5 แบนเนอร์ และโครงแดชบอร์ด

#### 📝 ขั้นตอนการปฏิบัติ:
สร้างไฟล์ `wwwroot/index.html` ภายในโปรเจกต์ `Kestrek_IoT_Web` แล้วเขียนโครงร่างหลักดังนี้:

```html
<!DOCTYPE html>
<html lang="th">
<head>
    <meta charset="UTF-8">
    <title>ESP32 IoT Dashboard - Kestrel Web Server</title>
    <link rel="stylesheet" href="css/style.css">
    <link href="https://fonts.googleapis.com/css2?family=Orbitron:wght@500;700&family=Kanit&display=swap" rel="stylesheet">
</head>
<body>
    <div class="container">
        <header class="dashboard-header">
            <h1>IoT Telemetry Dashboard</h1>
            <div class="status-pill">
                <span class="status-dot" id="status-dot"></span>
                <span id="status-text">Disconnect</span>
            </div>
        </header>

        <main class="dashboard-grid">
            <!-- จุดที่จะใส่ Widget 1 (Gauge) และ Widget 2 (VU Meter) -->
        </main>
    </div>
    <script src="js/dashboard.js"></script>
</body>
</html>
```

#### 🧪 Micro-Checkpoint 1 (จุดทดสอบที่ 1):
1. รัน Kestrel Server: `dotnet run`
2. เปิดเบราว์เซอร์去ที่: **`http://localhost:5000`**
3. **ผลลัพธ์:** ปรากฏหัวข้อส่วนแบนเนอร์และสถานะสีแดง `Disconnect` บนหน้าจอ

---

### Recipe 8.3.2: การวาดมาตรวัด SVG Radial Gauge และเข็มหมุน Needle

#### 📝 ขั้นตอนการปฏิบัติ:
เพิ่มองค์ประกอบ `<svg>` ภายในแท็ก `<main class="dashboard-grid">` ในไฟล์ `index.html`:

```html
<section class="card gauge-card">
    <h2>🎛️ Potentiometer (ADC 0-4095)</h2>
    <div class="gauge-container">
        <svg class="radial-gauge" viewBox="0 0 200 200">
            <!-- 1. เส้นโค้งพื้นหลัง -->
            <path class="gauge-bg" d="M 30 150 A 80 80 0 1 1 170 150" fill="none" stroke="#2d3748" stroke-width="16"/>
            <!-- 2. เส้นโค้งบอกระดับค่า (เติมสีด้วย Dashoffset) -->
            <path class="gauge-fill" id="gauge-fill" d="M 30 150 A 80 80 0 1 1 170 150" fill="none" stroke="#00f2fe" stroke-width="16" stroke-dasharray="351.85" stroke-dashoffset="351.85"/>
            <!-- 3. เข็ม Gauge และจุดหมุนตรงกลาง -->
            <line id="gauge-needle" x1="100" y1="100" x2="100" y2="35" class="needle" transform="rotate(-125, 100, 100)"/>
            <circle cx="100" cy="100" r="10" class="needle-cap"/>
        </svg>
        <div class="gauge-value-display">
            <span class="digital-val" id="pot-raw">0</span> ADC RAW
            <div class="sub-voltage"><span id="pot-voltage">0.00</span> V</div>
        </div>
    </div>
</section>
```

#### 🧪 Micro-Checkpoint 2 (จุดทดสอบที่ 2):
รีเฟรชหน้าเบราว์เซอร์ **`http://localhost:5000`** จะต้องเห็นโครงวงกลมเกจวัดสีเข้มและเข็มหมุนสีแดงชี้อยู่ที่ตำแหน่งเริ่มต้นทางซ้ายสุด ($-125^\circ$)

---

### Recipe 8.3.3: การสร้างหลอดไฟ LED ของ Audio VU Meter ด้วย JavaScript

#### 📝 ขั้นตอนการปฏิบัติ:
1. เติมโครงร่าง VU Meter ใน `index.html`:
```html
<section class="card vu-card">
    <h2>💡 LDR VU Meter (Light Level)</h2>
    <div class="vu-container">
        <div class="vu-meter-bars" id="vu-meter"></div>
        <div class="vu-value-display">
            <span class="digital-val" id="ldr-percent">0.0</span> %
            <div class="sub-raw">RAW ADC: <span id="ldr-raw">0</span> / 4095</div>
        </div>
    </div>
</section>
```

2. เปิดไฟล์ `wwwroot/js/dashboard.js` เขียนฟังก์ชันสร้าง 10 แท่ง LED Bars:

```javascript
const vuMeterContainer = document.getElementById('vu-meter');

function initVUMeter() {
    vuMeterContainer.innerHTML = '';
    for (let i = 0; i < 10; i++) {
        const bar = document.createElement('div');
        bar.className = 'vu-bar';
        for (let j = 9; j >= 0; j--) {
            const seg = document.createElement('div');
            seg.className = 'vu-segment';
            bar.appendChild(seg);
        }
        vuMeterContainer.appendChild(bar);
    }
}
document.addEventListener('DOMContentLoaded', initVUMeter);
```

#### 🧪 Micro-Checkpoint 3 (จุดทดสอบที่ 3):
รีเฟรชหน้าเบราว์เซอร์ จะต้องเห็นแผงหลอดไฟ LED สีเข้ม 10 แท่งปรากฏอยู่ใต้การ์ด LDR VU Meter

---

### 🧩 Hands-on Challenge 2 (สูตรคำนวณการหมุนเข็ม SVG Needle & Dashoffset):

ให้นักศึกษาเติมสูตรคำนวณทางคณิตศาสตร์ในไฟล์ `wwwroot/js/dashboard.js` เพื่อเปลี่ยนค่าตามสัญญาณ ADC:

```javascript
function updateGauge(rawVal, voltage) {
    document.getElementById('pot-raw').textContent = rawVal;
    document.getElementById('pot-voltage').textContent = voltage.toFixed(2);

    // 💡 1. คำนวณอัตราส่วน 0.0 ถึง 1.0 (จากช่วง ADC 0 - 4095)
    const ratio = Math.max(0, Math.min(1, rawVal / 4095.0));

    // 💡 2. คำนวณความยาวเส้นรอบวง (351.85px คือซ่อน 100%, 0px คือแสดงเต็ม 100%)
    const offset = 351.85 * (1 - ratio);
    document.getElementById('gauge-fill').style.strokeDashoffset = offset;

    // 💡 3. คำนวณมุมหมุนเข็ม (กวาดมุมตั้งแต่ -125 องศา ถึง +125 องศา)
    const angle = -125 + (ratio * 250);
    document.getElementById('gauge-needle').style.transform = `rotate(${angle}deg)`;
}
```

---

### Recipe 8.3.4: การดึงข้อมูลเรียลไทม์ (Polling API) และการจัดโซนสี LED

#### 📝 ขั้นตอนการปฏิบัติ:
เพิ่มฟังก์ชัน Polling API ดึงข้อมูลจาก Kestrel มาอัปเดตทุกๆ 1 วินาทีใน `dashboard.js`:

```javascript
async function fetchTelemetry() {
    try {
        const response = await fetch('/api/telemetry/latest');
        const data = await response.json();

        if (data.deviceId && data.deviceId !== "No Device Connected") {
            document.getElementById('status-dot').classList.add('online');
            document.getElementById('status-text').textContent = 'Online';

            // อัปเดต Gauge และ VU Meter
            updateGauge(data.potentiometer, data.potVoltage);
            updateVUMeter(data.ldrPercent, data.ldr);
        }
    } catch (err) {
        document.getElementById('status-dot').classList.remove('online');
        document.getElementById('status-text').textContent = 'Offline';
    }
}

// ตั้ง Polling ทุกๆ 1 วินาที
setInterval(fetchTelemetry, 1000);
```

#### 🧪 Micro-Checkpoint 4 (จุดทดสอบขั้นสุดท้าย):
1. เปิด Kestrel Server (`dotnet run`)
2. จ่ายไฟให้ ESP32 เชื่อมต่อ Wi-Fi ยิงข้อมูลเข้ามา
3. เปิดเบราว์เซอร์ไปที่ `http://localhost:5000`
4. **ทดสอบผลลัพธ์:**
   - เมื่อ **หมุน Potentiometer**: เข็มสีแดงและเส้นโค้งเวกเตอร์ SVG จะหมุนกวาดตามมืออย่างนุ่มนวล
   - เมื่อ **เอามือปิดบังแสง LDR**: หลอดไฟ LED ของ VU Meter จะลดระดับลง และเมื่อนำไฟฉายส่อง แผงไฟจะสว่างพุ่งขึ้นไปถึงโซนสีแดงทันที!
