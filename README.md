# MonitoringSystem

Розподілена система моніторингу мікросервісів з автоматичним виявленням аномалій. Дипломний проєкт.

Мікросервіси (або симулятор) надсилають метрики в API, API зберігає їх у PostgreSQL, виконує real-time виявлення аномалій (Z-score) і пакетний аналіз архіву (ML.NET SrCnn), стежить за health-статусом інстансів і передає оновлення на dashboard через SignalR.

## Склад рішення

`MonitoringSystem.sln` побудовано за принципом чистої архітектури з розділенням на шари:

| Проєкт | Шар | Призначення |
|---|---|---|
| `MonitoringSystem.API` | Presentation | ASP.NET Core Web API + статичний dashboard. Контролери, middleware, SignalR-hub, `Program.cs` |
| `MonitoringSystem.BLL` | Business Logic | Сервіси, інтерфейси, доменні моделі, DI-реєстрація, anomaly detection, SignalR-hub, фонові служби |
| `MonitoringSystem.DAL` | Data Access | EF Core `DbContext`, репозиторії, міграції PostgreSQL |
| `MonitoringSystem.Domain` | Domain | Entity-класи та інтерфейси доменного рівня |
| `ServiceSimulator` | Tool | Консольний симулятор, який генерує метрики й штучні аномалії |

## Архітектура

```text
ServiceSimulator / Microservice
        │
        │ POST /api/metrics/ingest  (X-API-KEY: ...)
        ▼
┌──────────────────────────────────────┐
│  MonitoringSystem.API                │
│  ┌────────────────────────────────┐  │
│  │ ApiKeyMiddleware (auth)        │  │
│  │ MetricsCollectionMiddleware    │  │
│  └────────────────────────────────┘  │
│  ┌────────────────────────────────┐  │
│  │ Controllers                    │  │
│  │  • Metrics                     │  │
│  │  • Anomalies                   │  │
│  │  • ApiKeys                     │  │
│  └────────────────────────────────┘  │
└──────────────┬───────────────────────┘
               │
               ▼
┌──────────────────────────────────────┐
│  MonitoringSystem.BLL                │
│  • MetricsService                    │
│  • AnomalyDetectionService           │
│      ├ Z-score (online)              │
│      └ ML.NET SrCnn (batch)          │
│  • ApiKeysService                    │
│  • MetricIngestionBackgroundService  │
│  • ServiceHealthBackgroundService    │
│  • MetricsHub  (SignalR)             │
└──────────────┬───────────────────────┘
               │
               ▼
┌──────────────────────────────────────┐
│  MonitoringSystem.DAL / Domain       │
│  EF Core + PostgreSQL                │
│  • MetricPointEntity                 │
│  • AnomalyEntity                     │
│  • ApiKeyEntity                      │
└──────────────────────────────────────┘
               │
               ▼ SignalR /hub/metrics
        ┌────────────┐
        │ Dashboard  │
        └────────────┘
```

## Ключові компоненти

### Authentication — API ключі

`MonitoringSystem.API/Middlewares/ApiKeyMiddleware.cs`

Всі endpoint-и, окрім публічних (`/health`, `/swagger`, `/hub/metrics`, `/api/keys`), вимагають заголовок:

```http
X-API-KEY: <key>
```

Ключ перевіряється на існування й активність у БД, поле `LastUsedAt` оновлюється при кожному запиті. Контролер `ApiKeysController` дозволяє створювати, переглядати та відкликати ключі.

### `MetricsService`

`MonitoringSystem.BLL/Services/MetricsService.cs`

Центральний сервіс. Зберігає метрики, виконує online-аналіз Z-score, зберігає виявлені аномалії, оновлює in-memory кеш статусів сервісів, надсилає події через SignalR (`MetricsUpdated`, `AnomaliesDetected`, `ServiceStatusUpdated`). Також відповідає за пакетне порівняння алгоритмів і за новий пакетний SrCnn-аналіз.

### `AnomalyDetectionService`

`MonitoringSystem.BLL/Services/AnomalyDetectionService.cs`

Дві стратегії виявлення:

| Метод | Алгоритм | Сценарій |
|---|---|---|
| `Analyze(MetricPoint)` | **Z-score** з ковзним вікном 30 точок, поріг 2.5σ | online-аналіз під час ingest |
| `AnalyzeBatchWithMlNet(...)` | **ML.NET SrCnn** (Spectral Residual + CNN, KDD 2019) | пакетний аналіз архіву, чутливість 0.1–0.9 |
| `CompareAlgorithms(...)` | обидва на одному часовому ряді | порівняння точності |

Поріг SrCnn передається параметром і конфігурується з UI.

### Background-сервіси

- **`MetricIngestionBackgroundService`** + **`MetricIngestionQueue`** — middleware кладе власні метрики API в чергу, фоновий сервіс читає їх в окремому DI scope (бо `DbContext` — scoped).
- **`ServiceHealthBackgroundService`** — періодично перевіряє, чи присилав інстанс метрики останніх `TimeoutSeconds` секунд; перемикає `IsHealthy` і шле оновлення в SignalR.

### `MetricsCollectionMiddleware`

`MonitoringSystem.API/Middlewares/MetricsCollectionMiddleware.cs`

API сам себе моніторить. Збирає на кожному HTTP-запиті:

- `http.response_time_ms`
- `system.memory_mb`
- `http.requests_total`
- `http.errors_total`

### `MetricsHub` (SignalR)

`MonitoringSystem.BLL/Hubs/MetricsHub.cs` → endpoint `/hub/metrics`

Події: `MetricsUpdated`, `AnomaliesDetected`, `ServiceStatusUpdated`.

## База даних

PostgreSQL через Entity Framework Core. Міграції живуть у `MonitoringSystem.DAL/Data/Migrations` і застосовуються автоматично при старті API (`app.ApplyMigrations()`).

Connection string у `MonitoringSystem.API/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=monitoring_db;Username=postgres;Password=1234"
  }
}
```

Таблиці:

- `MetricPoints` — історія метрик (індекси за `ServiceName`, `MetricName`, `Timestamp`)
- `Anomalies` — виявлені аномалії (індекс за `DetectedAt`)
- `ApiKeys` — згенеровані ключі для сервісів

Створити міграцію після зміни моделей:

```powershell
dotnet ef migrations add <Name> -p MonitoringSystem.DAL -s MonitoringSystem.API
```

## Dashboard

Single-page UI у `MonitoringSystem.API/wwwroot/`. П'ять сторінок-вкладок:

| Сторінка | Що показує |
|---|---|
| **Дашборд** | Real-time картки сервісів, графіки CPU/Memory, потік аномалій через SignalR |
| **Метрики** | Статус інстансів + пошук метрик за діапазоном з графіком та таблицею |
| **Аномалії** | Архів виявлених аномалій з фільтрами за сервісом і часом |
| **Алгоритми** | Side-by-side порівняння Z-score та SrCnn (precision, recall, F1, час) |
| **SrCnn архів** | **Новий.** Пакетний SrCnn-аналіз архівних даних — див. нижче |
| **API Ключі** | Створення, перегляд, відкликання ключів |

Файли:

- `wwwroot/index.html` — розмітка
- `wwwroot/css/main.css` — стилі (тема, аналогічна Grafana dark)
- `wwwroot/js/config.js` — `apiFetch`, `toast`, форматтери, навігація
- `wwwroot/js/page-metrics.js`, `page-anomalies.js`, `page-srcnn.js`, `page-keys.js` — логіка вкладок
- `wwwroot/js/signalr-handler.js` — підписка на події hub-а

`API_URL` у `wwwroot/js/config.js` за замовчуванням — `http://localhost:5169`.

API-ключ для запитів зберігається в `localStorage` і додається в кожен fetch як `X-API-KEY`. Поле для введення ключа є в шапці dashboard-а.

### SrCnn архівний аналіз

Окрема вкладка для пакетного аналізу історичних метрик алгоритмом SrCnn. Дозволяє:

- обрати сервіс, метрику та довільний часовий діапазон;
- регулювати **чутливість 0.1–0.9** через slider (поріг SrCnn);
- отримати графік ряду з виділеними червоними точками аномалій;
- окремий bar-chart `Anomaly Score за часом`, кольори за severity;
- 8 статистичних карток: всього точок / аномалій (з %) / Critical / Warning / Info / max score / avg score / час обробки;
- таблицю аномалій з progress-bar score та badge severity;
- **експорт у CSV**.

Мінімум для SrCnn — 12 точок у вибраному діапазоні.

## Запуск

### 1. PostgreSQL

```sql
CREATE DATABASE monitoring_db;
```

Перевірити `ConnectionStrings:DefaultConnection` в `MonitoringSystem.API/appsettings.json`.

### 2. Запустити API

```powershell
dotnet run --project MonitoringSystem.API
```

Міграції застосуються автоматично. URL — `http://localhost:5169` (див. `Properties/launchSettings.json`).

Якщо потрібно інший порт — або змінити profile, або:

```powershell
$env:ASPNETCORE_URLS="http://localhost:5000"
dotnet run --project MonitoringSystem.API
```

Тоді `API_URL` у `wwwroot/js/config.js` теж треба підправити.

### 3. Створити API-ключ

Через Swagger (`/swagger`) або з UI (вкладка **API Ключі**):

```http
POST /api/keys
Content-Type: application/json

{ "serviceName": "PaymentService", "ownerName": "backend-team" }
```

У відповіді — ключ. Зберігай — він показується тільки раз.

### 4. Відкрити dashboard

```text
http://localhost:5169/
```

Вставити отриманий ключ у поле `X-API-KEY` у шапці → зберегти.

### 5. Запустити симулятор

Симулятор використовує hardcoded ключі (`mk_dev_*`) для `OrderService`, `PaymentService`, `UserService`. Перед запуском треба створити ці ключі через `POST /api/keys` або підправити ключі в `ServiceSimulator/Program.cs`.

```powershell
dotnet run --project ServiceSimulator
```

Кожні 2 секунди надсилає метрики `system.cpu_percent`, `system.memory_mb`, `http.response_time_ms`, `http.requests_per_second`. Кожні 50 ітерацій — штучна аномалія для `PaymentService` (CPU 95–100%, response 2–3s).

## Endpoints

Всі endpoint-и (крім публічних) вимагають `X-API-KEY`. Повна специфікація — Swagger UI на `/swagger`.

### Health

```http
GET /health
```

### Метрики

```http
POST /api/metrics/ingest
GET  /api/metrics?service=...&metric=...&from=...&to=...
GET  /api/metrics/services
```

Приклад ingestion:

```json
{
  "serviceName": "PaymentService",
  "metrics": [
    {
      "metricName": "http.response_time_ms",
      "value": 2500,
      "timestamp": "2026-05-25T12:00:00Z",
      "tags": { "endpoint": "/api/payments" }
    }
  ]
}
```

### Аномалії

```http
GET  /api/anomalies?count=20
POST /api/anomalies/compare
POST /api/anomalies/srcnn-batch
```

**`/compare`** — порівнює Z-score і SrCnn на одному ряді:

```json
{
  "serviceName": "PaymentService",
  "metricName": "http.response_time_ms",
  "from": "2026-05-25T11:00:00Z",
  "to": "2026-05-25T12:00:00Z"
}
```

**`/srcnn-batch`** — пакетний SrCnn-аналіз з регульованою чутливістю:

```json
{
  "serviceName": "PaymentService",
  "metricName": "http.response_time_ms",
  "from": "2026-05-25T00:00:00Z",
  "to": "2026-05-25T23:59:59Z",
  "sensitivity": 0.3
}
```

Відповідь містить повний ряд (`points`), окремо аномалії (`anomalies`), розподіл за severity, `processingTimeMs`.

### API ключі

```http
POST   /api/keys           # створити
GET    /api/keys           # список (без розкриття)
DELETE /api/keys/{id}      # відкликати
```

## Моделі

### `MetricPoint`

```json
{
  "serviceName": "PaymentService",
  "metricName": "http.response_time_ms",
  "value": 123.4,
  "timestamp": "2026-05-25T12:00:00Z",
  "tags": { "method": "GET", "path": "/api/orders" }
}
```

### `AnomalyResult`

```json
{
  "serviceName": "PaymentService",
  "metricName": "http.response_time_ms",
  "value": 2500.0,
  "expectedValue": 70.0,
  "anomalyScore": 1.0,
  "isAnomaly": true,
  "severity": "Critical",
  "detectedAt": "2026-05-25T12:00:00Z"
}
```

`severity` обчислюється з `anomalyScore`: `> 0.8` → Critical, `> 0.5` → Warning, інакше Info.

### `ServiceStatus`

```json
{
  "serviceName": "PaymentService",
  "isHealthy": true,
  "lastSeen": "2026-05-25T12:00:00Z",
  "anomalyCount": 2,
  "latestMetrics": {
    "system.cpu_percent": 42.5,
    "http.response_time_ms": 85.0
  }
}
```

## Конфігурація

`MonitoringSystem.API/appsettings.json`:

```json
{
  "ServiceHealth": {
    "TimeoutSeconds": 30,
    "CheckIntervalSeconds": 5
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=monitoring_db;Username=postgres;Password=1234"
  }
}
```

## Логування

Serilog → консоль + файли `MonitoringSystem.API/logs/monitoring-*.log` (rolling daily).

## Стек

- .NET 10, ASP.NET Core
- Entity Framework Core + Npgsql (PostgreSQL)
- ML.NET (`Microsoft.ML.TimeSeries`) — SrCnn
- SignalR — real-time
- Serilog — логування
- Swagger / Swashbuckle
- Chart.js — графіки на dashboard

## Що реалізовано згідно з темою диплому

- збір метрик від кількох розподілених сервісів через REST API;
- автентифікація сервісів через API ключі;
- збереження історії метрик і виявлених аномалій у PostgreSQL;
- real-time dashboard через SignalR;
- health-check інстансів через timeout-based фоновий сервіс;
- **два алгоритми виявлення аномалій**:
  - online Z-score для потокового аналізу,
  - batch ML.NET SrCnn для архівного аналізу;
- порівняння алгоритмів на одних даних з метриками точності;
- класифікація severity через `anomalyScore`;
- симуляція нормальної поведінки та штучних аномалій;
- self-monitoring API через middleware + background queue.
