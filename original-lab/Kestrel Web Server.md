
การอธิบายสถาปัตยกรรมของ **Kestrel Web Server** (Web Server หลักแบบ Cross-platform และประสิทธิภาพสูงของ ASP.NET Core) ผ่าน **4+1 Architectural View Model (Kruchten)** พร้อมแผนภาพ UML ในแต่ละมุมมอง มีรายละเอียดดังนี้ครับ:

---

### 1. Logical View (มุมมองเชิงตรรกะ)

> **เป้าหมาย:** แสดงโครงสร้าง Abstraction ภายใน, Classes, Interfaces และความสัมพันธ์ของ Core Components

Kestrel ถูกออกแบบตามสถาปัตยกรรม Interface-driven เพื่อให้ถอดเปลี่ยน Transport Layer หรือขยายการทำงานได้ง่าย

- **`IServer`**: Interface หลักของ ASP.NET Core Server
- **`KestrelServer` / `KestrelServerImpl`**: ตัวจัดการ Life-cycle ของ Kestrel
- **`IConnectionListenerFactory` & `IConnectionListener`**: คอย Bind พอร์ตและ Listen TCP/UNIX Socket
- **`HttpProtocol`**: ประมวลผลและแปลง Byte Streams เป็น HTTP Request/Response (รองรับ HTTP/1.1, HTTP/2, HTTP/3)
- **`HttpContext` & `IFeatureCollection`**: Context ส่งต่อไปยัง ASP.NET Core Middleware Pipeline

```mermaid
classDiagram
    class IServer {
        <<interface>>
        +StartAsync()
        +StopAsync()
        +Features: IFeatureCollection
    }
    class KestrelServer {
        -KestrelServerOptions _options
        -IConnectionListenerFactory _transportFactory
        +StartAsync()
        +StopAsync()
    }
    class IConnectionListener {
        <<interface>>
        +EndPoint: EndPoint
        +AcceptAsync(): ValueTask~ConnectionContext~
        +UnbindAsync()
    }
    class HttpProtocol {
        <<abstract>>
        #ConnectionContext Context
        #PipeReader Input
        #PipeWriter Output
        +ProcessRequestsAsync()
    }
    class Http1Connection
    class Http2Stream
    class Http3Stream
    IServer <|.. KestrelServer
    KestrelServer --> IConnectionListener : Uses
    IConnectionListener --> HttpProtocol : Creates
    HttpProtocol <|-- Http1Connection
    HttpProtocol <|-- Http2Stream
    HttpProtocol <|-- Http3Stream
    HttpProtocol ..> HttpContext : Builds & Dispatches
```


---

### 2. Process View (มุมมองกระบวนการและ Concurrency)

> **เป้าหมาย:** แสดงพฤติกรรมการทำงานขณะ Runtime, การจัดการ Threading, Non-blocking I/O และ Memory (`System.IO.Pipelines`)

Kestrel ใช้แนวทาง **Asynchronous Event-driven Non-blocking I/O** โดยใช้ `System.IO.Pipelines` เพื่อทำ **Zero-copy / Low-allocation**:

1. Socket Transport รับ TCP Connection และเขียน Raw Bytes ลงใน `Pipe`
2. Kestrel Parser (`HttpProtocol`) อ่าน Byte stream และ Parse Headers
3. สร้าง `HttpContext` และส่งเข้า Application Middleware Pipeline บน ThreadPool ของ .NET


```mermaid
sequenceDiagram
    autonumber
    actor Client as Client / Browser
    participant Socket as Socket Transport (IConnectionListener)
    participant Pipe as System.IO.Pipelines (Memory Pool)
    participant Kestrel as Kestrel Engine (HttpProtocol)
    participant App as ASP.NET Core Pipeline (Middleware)
    Client->>Socket: 1. TCP Handshake & Send HTTP Request Bytes
    Socket->>Pipe: 2. Write raw bytes to PipeWriter
    Pipe-->>Kestrel: 3. PipeReader notifies bytes available
    Kestrel->>Kestrel: 4. Zero-allocation Parsing (Method, Path, Headers)
    Kestrel->>App: 5. Construct HttpContext & Invoke Application
    App->>App: 6. Execute Routing / Controllers / Minimal APIs
    App->>Kestrel: 7. Write Response (Status, Headers, Body)
    Kestrel->>Pipe: 8. Flush response bytes to Pipe
    Pipe->>Socket: 9. Send TCP packets back
    Socket-->>Client: 10. HTTP Response delivered
```

---

### 3. Development View / Implementation View (มุมมองการพัฒนา)

> **เป้าหมาย:** แสดงโครงสร้าง Modules, Packages และ Layering ของ Source Code ใน .NET Runtime / ASP.NET Core

Kestrel แยก Layer ชัดเจนระหว่าง **Transport Layer**, **Protocol Engine Layer** และ **Hosting Layer**:

```mermaid
graph TD
    subgraph Hosting_Layer["Hosting Layer"]
        A[Microsoft.AspNetCore.Hosting]
        B[Microsoft.AspNetCore.Server.Kestrel]
    end
    subgraph Core_Engine["Core Engine Layer"]
        C[Microsoft.AspNetCore.Server.Kestrel.Core]
        D[Http1 / Http2 / Http3 Framing & Parsing]
        E[KestrelServerOptions & TLS / ALPN]
    end
    subgraph Transport_Layer["Transport Layer (Pluggable)"]
        F[Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets]
        G[Microsoft.AspNetCore.Server.Kestrel.Transport.Quic]
        H[Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes]
    end
    subgraph Infrastructure_Layer["Base .NET Runtime"]
        I[System.IO.Pipelines]
        J[System.Buffers / MemoryPool]
        K[System.Threading.ThreadPool]
    end
    Hosting_Layer --> Core_Engine
    Core_Engine --> Transport_Layer
    Transport_Layer --> Infrastructure_Layer
    Core_Engine --> Infrastructure_Layer
```

---

### 4. Physical / Deployment View (มุมมองทางกายภาพและการ Deploy)

> **เป้าหมาย:** แสดงว่า Kestrel วางอยู่ตรงไหนใน Network Topology เมื่อนำไปใช้งานจริง

Kestrel รองรับ 2 รูปแบบการ Deploy หลัก:

#### รูปแบบ A: Edge Server (Direct-facing)

Kestrel รับ Traffic ตรงจาก Internet โดยเปิด TLS, HTTP/2, HTTP/3 ด้วยตัวเอง (เหมาะกับ Microservices, gRPC, หรือสภาพแวดล้อม Containerized)

#### รูปแบบ B: Behind Reverse Proxy (Most Common for Enterprise)

อยู่หลัง Reverse Proxy (เช่น Nginx, Apache, IIS, Envoy, YARP หรือ Cloud Load Balancer) เพื่อจัดการ SSL Termination, Static Files, Rate Limiting และ Security Defense

```mermaid
graph TD
    subgraph Internet["Public Network"]
        Client[Clients / Mobile / Web]
    end
    subgraph Edge_Infrastructure["Reverse Proxy / Edge Layer"]
        RP[Nginx / YARP / Cloudflare / IIS]
    end
    subgraph Host_Container["Application Server / Pod"]
        Kestrel[Kestrel Web Server :5000]
        App[ASP.NET Core App Code]
    end
    Client -->|HTTPS :443 / HTTP/3| RP
    RP -->|Forwarded HTTP :5000 + Headers| Kestrel
    Kestrel <--> App
```
---

### (+1) Use Case / Scenarios View (มุมมองการใช้งานหลัก)

- **High-throughput API / Microservices:** รับ Request ปริมาณมหาศาลด้วย Latency ต่ำและใช้ CPU/RAM น้อย
- **gRPC Server:** รองรับ HTTP/2 End-to-End และ Multiplexing สตรีมข้อมูล
- **Modern Web Standards (HTTP/3 over QUIC):** รองรับ Connection Migration และลด Head-of-Line Blocking ผ่าน UDP Transport

