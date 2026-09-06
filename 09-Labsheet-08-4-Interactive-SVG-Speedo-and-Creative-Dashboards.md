# ใบงานการทดลองที่ 8.4 (Labsheet 8.4): แดชบอร์ดมาตรวัดความเร็ว SVG และสนามทดลองสร้างสรรค์ (Creative Playground)

> **คำชี้แจง:** ในใบงานสุดท้ายของสัปดาห์นี้ นักศึกษาจะได้นำทักษะทั้งหมดมาสร้าง **"หน้าปัดมาตรวัดความเร็ว (Speedometer Gauge) ด้วย Pure SVG"** แสดงผลบนเว็บเบราว์เซอร์ โดยไม่ต้องพึ่งพาไลบรารีภายนอก เมื่อหมุน Volume บนบอร์ด ESP32 เข็มไมล์จะกวาดตามมืออย่างนุ่มนวล และปิดท้ายด้วย **Creative Sandbox** ให้เลือกปรับแต่งวิดเจ็ตในสไตล์ของตนเอง

---

## 🎯 วัตถุประสงค์การทดลอง (Objectives)
1. สามารถกำหนดค่า Kestrel ให้ทำหน้าที่เสิร์ฟไฟล์หน้าเว็บ (Static Files / Single Page App) ได้
2. เข้าใจการเขียนกราฟิกเวกเตอร์สองมิติด้วยแท็ก `<svg>` สำหรับสร้างหน้าปัดเครื่องมือวัด
3. สามารถเขียน JavaScript (`fetch` API) ทำงานแบบ Polling ดึงข้อมูลเซนเซอร์มาอัปเดตหน้าจอแบบต่อเนื่อง
4. สามารถแปลงสเกลเชิงเส้น (Linear Mapping) จากค่า ADC สู่มุมการหมุนของเข็มไมล์ด้วย CSS Transform ได้
5. ได้ใช้ความคิดสร้างสรรค์ในการปรับแต่งหน้าปัด เช่น ปรับธีม, สร้าง VU Meter หรือหน้าจอ 7-Segment Display

---

## 🛠️ เครื่องมือและสิ่งที่ต้องเตรียม (Prerequisites)
- บอร์ด ESP32 พร้อมสาย USB ที่ต่อวงจร Potentiometer เรียบร้อยแล้ว
- เว็บเบราว์เซอร์ที่รองรับ HTML5 / SVG (Google Chrome, Edge, Safari, Firefox)

> ⚠️ **ข้อกำหนดสำคัญด้านการส่งงาน (Project Isolation & Anti-Cheating):**  
> ในใบงานสุดท้ายที่ 8.4 นี้ ให้นักศึกษาจัดเตรียมโฟลเดอร์แยกต่างหากเป็น **`Lab8-4`** อย่างชัดเจน:
> 1. **ฝั่ง ESP32:** โฟลเดอร์ `Lab8-4/ESP32_ADC_Stream` (คัดลอกหรือสร้างใหม่จากแล็บก่อนหน้า)
> 2. **ฝั่ง Kestrel Dashboard:** โฟลเดอร์ `Lab8-4/Kestrel_SVG_Dashboard`

---

## 🧪 ขั้นตอนการทดลอง (Step-by-Step Activities)

### 🌟 กิจกรรมที่ 1: เตรียมโปรเจกต์ Kestrel_SVG_Dashboard และเปิดใช้งาน Static File Server

1. เตรียมโฟลเดอร์สำหรับ Lab 8.4 โดยสร้างโปรเจกต์ใหม่ (หรือคัดลอกโครงสร้างจาก `Lab8-3/Kestrel_Serial_Gateway` มาต่อยอด):
   ```bash
   # หากสร้างใหม่
   mkdir -p Lab8-4/Kestrel_SVG_Dashboard && cd Lab8-4/Kestrel_SVG_Dashboard
   dotnet new web -o .
   dotnet add package System.IO.Ports

   # สร้างโฟลเดอร์ wwwroot สำหรับเก็บไฟล์เว็บ HTML/CSS/SVG
   mkdir wwwroot
   ```

2. ในไฟล์ `Program.cs` ให้ตรวจเช็คว่ามีโค้ด State Store และ Background Worker จากใบงานที่ 8.3 ครบถ้วน จากนั้นเพิ่มคำสั่ง `app.UseFileServer();` ก่อนบรรทัด `app.Run();`:
   ```csharp
   // เปิดใช้งานการเสิร์ฟไฟล์สถิต (HTML, CSS, JS) ใน wwwroot และเปิด index.html อัตโนมัติ
   app.UseFileServer();

   app.Run();
   ```

---

### 🌟 กิจกรรมที่ 2: สร้างหน้าเว็บและหน้าปัด Speedometer SVG

1. สร้างไฟล์ใหม่ชื่อ `index.html` ไว้ด้านในโฟลเดอร์ `wwwroot/index.html`
2. ใส่โค้ด HTML + SVG + JavaScript ต่อไปนี้ลงไป:

```html
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
        
        /* สไตล์หน้าปัด SVG */
        .gauge-svg {
            width: 100%;
            max-width: 320px;
            filter: drop-shadow(0 0 15px rgba(0, 242, 254, 0.2));
        }
        #needle {
            transform-origin: 150px 150px;
            transition: transform 0.15s cubic-bezier(0.4, 0, 0.2, 1);
        }
        
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
    </style>
</head>
<body>

<div class="container">
    <h1>🏎️ IoT Edge Speedometer</h1>
    <div class="subtitle">ESP32 Hardware Stream &bull; Kestrel Edge Web Server</div>

    <!-- หน้าปัดวัดความเร็วแบบ SVG แท้ 100% -->
    <svg class="gauge-svg" viewBox="0 0 300 200">
        <!-- รางมาตรวัดพื้นหลัง (สีเทาจาง) -->
        <path d="M 40 150 A 110 110 0 0 1 260 150" fill="none" stroke="#2a3b5c" stroke-width="18" stroke-linecap="round"/>
        
        <!-- รางโซนสีเตือน (เขียว - เหลือง - แดง) -->
        <path d="M 40 150 A 110 110 0 0 1 150 40" fill="none" stroke="#4ade80" stroke-width="6" stroke-linecap="round"/>
        <path d="M 150 40 A 110 110 0 0 1 215 65" fill="none" stroke="#facc15" stroke-width="6"/>
        <path d="M 215 65 A 110 110 0 0 1 260 150" fill="none" stroke="#ef4444" stroke-width="6" stroke-linecap="round"/>

        <!-- ขีดตัวเลขสเกล -->
        <text x="35" y="175" fill="#778da9" font-size="12" text-anchor="middle">0%</text>
        <text x="150" y="25" fill="#778da9" font-size="12" text-anchor="middle">50%</text>
        <text x="265" y="175" fill="#778da9" font-size="12" text-anchor="middle">100%</text>

        <!-- ตัวเลขค่าเปอร์เซ็นต์ดิจิทัลตรงกลาง -->
        <text id="disp-percent" x="150" y="135" fill="#ffffff" font-size="32" font-weight="bold" text-anchor="middle">0.0%</text>

        <!-- เข็มไมล์สีแดงสะท้อนแสง (หมุนรอบจุด 150, 150) -->
        <g id="needle" style="transform: rotate(-90deg);">
            <!-- ตัวเข็มทรงสามเหลี่ยมเรียว -->
            <polygon points="147,150 153,150 151,50 149,50" fill="#ff0055" filter="drop-shadow(0 0 6px #ff0055)"/>
            <!-- ฝาครอบแกนหมุนตรงกลาง -->
            <circle cx="150" cy="150" r="10" fill="#ffffff"/>
            <circle cx="150" cy="150" r="5" fill="#ff0055"/>
        </g>
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
    // ฟังก์ชันคำนวณแปลงสเกลเชิงเส้น (Linear Mapping)
    // แปลง 0% - 100% เป็นมุมองศา -90deg ถึง +90deg
    function percentToAngle(pct) {
        return (pct / 100.0) * 180.0 - 90.0;
    }

    async function pollTelemetry() {
        try {
            const res = await fetch('/api/telemetry');
            if (!res.ok) return;
            const data = await res.json();

            // อัปเดตตัวเลขบนหน้าจอ
            document.getElementById('disp-percent').textContent = data.percentage.toFixed(1) + '%';
            document.getElementById('disp-raw').textContent = data.rawValue;
            document.getElementById('disp-volt').textContent = data.voltage.toFixed(2) + ' V';
            document.getElementById('disp-source').textContent = '📡 ' + data.dataSource;

            // สั่งหมุนเข็มไมล์ SVG นุ่มนวลตามมุมที่คำนวณได้
            const angle = percentToAngle(data.percentage);
            document.getElementById('needle').style.transform = `rotate(${angle}deg)`;
        } catch (err) {
            console.error('Polling error:', err);
        }
    }

    // วนลูปดึงข้อมูลทุกๆ 150 มิลลิวินาที (ประมาณ 7 ครั้งต่อวินาที)
    setInterval(pollTelemetry, 150);
</script>

</body>
</html>
```

---

### 🌟 กิจกรรมที่ 3: ทดสอบหมุนหน้าปัด

1. รันเซิร์ฟเวอร์ด้วยคำสั่ง:
   ```bash
   dotnet run
   ```
2. เปิดเบราว์เซอร์ไปที่: `http://localhost:5000`
3. **สิ่งที่เกิดขึ้น:** หน้าปัด Speedometer สไตล์ซูเปอร์คาร์จะปรากฏขึ้นกลางหน้าจอ
4. **ลงมือทดสอบ:** 
   - ใช้นิ้วหมุนตัวต้านทานปรับค่าได้ (Potentiometer) บนโต๊ะไปทางขวา $\rightarrow$ เข็มไมล์จะกวาดขึ้นอย่างลื่นไหล ตัวเลขเปอร์เซ็นต์วิ่งขึ้นตามมือทันที!
   - หมุนกลับมาทางซ้าย $\rightarrow$ เข็มไมล์ตกลงมาที่ศูนย์อย่างนุ่มนวล!

> 🎉 **ยินดีด้วย!** นักศึกษาได้สร้างระบบ **Full-Stack Physical-to-Web IoT Gateway** ที่สมบูรณ์แบบด้วยฝีมือตนเองตั้งแต่ระดับวงจรแอนะล็อกจนถึงหน้าจอเว็บแอปพลิเคชัน!

---

## 🎨 สนามทดลองสร้างสรรค์ (Creative Playground: ปล่อยพลังแต่ง Dashboard)

เพื่อให้นักศึกษาได้นำเสนอความคิดสร้างสรรค์และสร้างผลงานเฉพาะตัว ให้เลือกทำกิจกรรมเสริมอย่างน้อย **1 รูปแบบ** จากตัวเลือกด้านล่างนี้:

---

### 🎛️ ตัวเลือก A: หน้าปัดมาตรวัดสัญญาณเสียง (Audio VU Meter / LED Bar)
สร้างแถบหลอดไฟ LED แนวตั้งหรือแนวนอน 10 ดวง (เขียว 6 ดวง, เหลือง 2 ดวง, แดง 2 ดวง) ที่จะค่อยๆ สว่างขึ้นตามระดับความแรงของเซนเซอร์:

```html
<!-- โค้ด SVG ตัวอย่างสำหรับ VU Meter (แทรกในหน้าเว็บ) -->
<svg width="250" height="40" viewBox="0 0 250 40" id="vumeter">
    <!-- แถบ LED 10 แท่ง (ปรับ opacity ด้วย JavaScript) -->
    <rect class="led" x="5" y="5" width="18" height="30" rx="3" fill="#22c55e" opacity="0.2"/>
    <rect class="led" x="28" y="5" width="18" height="30" rx="3" fill="#22c55e" opacity="0.2"/>
    <rect class="led" x="51" y="5" width="18" height="30" rx="3" fill="#22c55e" opacity="0.2"/>
    <rect class="led" x="74" y="5" width="18" height="30" rx="3" fill="#22c55e" opacity="0.2"/>
    <rect class="led" x="97" y="5" width="18" height="30" rx="3" fill="#22c55e" opacity="0.2"/>
    <rect class="led" x="120" y="5" width="18" height="30" rx="3" fill="#22c55e" opacity="0.2"/>
    <rect class="led" x="143" y="5" width="18" height="30" rx="3" fill="#eab308" opacity="0.2"/>
    <rect class="led" x="166" y="5" width="18" height="30" rx="3" fill="#eab308" opacity="0.2"/>
    <rect class="led" x="189" y="5" width="18" height="30" rx="3" fill="#ef4444" opacity="0.2"/>
    <rect class="led" x="212" y="5" width="18" height="30" rx="3" fill="#ef4444" opacity="0.2"/>
</svg>
```
*แนวทาง JavaScript ควบคุม:* คำนวณจำนวนหลอดที่ต้องเปิด = `Math.floor(data.percentage / 10)` แล้วสั่งเปลี่ยน `opacity` เป็น `1.0` พร้อมใส่ Glow Effect!

---

### 📟 ตัวเลือก B: หน้าจอดิจิทัลเรโทร 7 ส่วน (Retro 7-Segment SVG)
สร้างหน้าปัดตัวเลขดิจิทัลแบบ 7 ส่วนเรืองแสงสไตล์นีออนเรโทร โดยควบคุมชิ้นส่วนของเส้น segment (a, b, c, d, e, f, g) ให้เปิด-ปิดตามตัวเลขเปอร์เซ็นต์ที่อ่านได้

---

### 🛢️ ตัวเลือก C: ถังระดับของเหลวอุตสาหกรรม (Liquid Level Tank)
สร้างภาพแท็งก์น้ำทรงกระบอก ที่ระดับน้ำสีฟ้าเรืองแสงจะกระเพื่อมและสูงขึ้น-ลดลงในถังตามระดับการหมุนตัวต้านทาน

---

## 📋 ส่งงานและประเมินผล (Submission Check)
1. บันทึกวิดีโอคลิปสั้น (15-30 วินาที) โดยในคลิปต้องเห็น:
   - นิ้วมือนักศึกษากำลังหมุนตัวต้านทานปรับค่าได้บนบอร์ด ESP32
   - หน้าจอคอมพิวเตอร์ที่เข็มไมล์ Speedometer / VU Meter กวาดตามมืออย่างชัดเจน
2. แนบภาพหน้าจอซอร์สโค้ดและรายงานการทดลอง
