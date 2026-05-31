# MonitoringSystem

Розподілена система моніторингу мікросервісів з автоматичним виявленням аномалій. Дипломний проєкт.

Користувач реєструє API-ключ, під цим ключем заводить набір сервісів, і ці сервіси (або симулятор) надсилають метрики в REST API. API зберігає їх у PostgreSQL, виконує real-time виявлення аномалій (Z-score) і пакетний аналіз архіву (ML.NET SrCnn), стежить за health-статусом інстансів і пушить оновлення на dashboard через SignalR. Кожен користувач (API-ключ) бачить лише свої сервіси, метрики та аномалії.

## Склад рішення

`MonitoringSystem.sln` побудовано за принципом чистої архітектури з розділенням на шари:

| Проєкт | Шар | Призначення |
|---|---|---|
| `MonitoringSystem.API` | Presentation | ASP.NET Core Web API + статичний dashboard. Контролери, middleware, SignalR-hub, `Program.cs` |
| `MonitoringSystem.BLL` | Business Logic | Сервіси, інтерфейси, доменні моделі, DI-реєстрація, anomaly detection, SignalR-hub, фонові служби |
| `MonitoringSystem.DAL` | Data Access | EF Core `DbContext`, репозиторії, міграції PostgreSQL, seed демо-даних |
| `MonitoringSystem.Domain` | Domain | Entity-класи та інтерфейс `IEntity` |
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
│  └────────────────────────────────┘  │
│  ┌────────────────────────────────┐  │
│  │ Controllers                    │  │
│  │  • Metrics                     │  │
│  │  • Anomalies                   │  │
│  │  • Services                    │  │
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
│  • ServicesService                   │
│  • ApiKeysService                    │
│  • ServiceHealthBackgroundService    │
│  • MetricsHub  (SignalR)             │
└──────────────┬───────────────────────┘
               │
               ▼
┌──────────────────────────────────────┐
│  MonitoringSystem.DAL / Domain       │
│  EF Core + PostgreSQL                │
│  • ApiKeyEntity   ──┐                │
│  • ServiceEntity  ──┤ ApiKey 1:N     │
│  • MetricPointEntity   Service 1:N   │
│  • AnomalyEntity                     │
└──────────────────────────────────────┘
               │
               ▼ SignalR /hub/metrics?apiKey=...
        ┌────────────┐
        │ Dashboard  │  (групи "apiKey:{id}")
        └────────────┘
```

## Доменна модель

```
ApiKey  1 ──< N  Service  1 ──< N  MetricPoint
                          1 ──< N  Anomaly
```

- `ApiKey` ідентифікує користувача (`Owner`, `Key`, `IsActive`).
- `Service` — окремий моніторений сервіс, унікальний у межах ключа (`unique(ApiKeyId, Name)`).
- Метрики та аномалії прив'язуються до `ServiceId`, тож вся ізоляція між користувачами — на рівні зовнішнього ключа.

## Ключові компоненти

### Authentication — API ключі

`MonitoringSystem.API/Middlewares/ApiKeyMiddleware.cs`

Усе, що не починається з `/api`, проходить без перевірки (це покриває `/`, статичний UI, `/swagger`, `/hub/metrics`). Серед `/api/*` єдиний публічний endpoint — `POST /api/keys` (створення нового ключа). Решта вимагає заголовок:

```http
X-API-KEY: <key>
```

Ключ перевіряється на існування й активність у БД, поле `LastUsedAt` оновлюється при кожному запиті, сам entity кладеться в `HttpContext.Items["ApiKeyDTO"]` — контролери дістають його через `CurrentApiKey` і передають у BLL-сервіси для фільтрації по власнику.

SignalR-хаб робить аналогічну перевірку для query-параметра `?apiKey=...` і кладе підключення в групу `apiKey:{id}`.

### `ServicesService`

`MonitoringSystem.BLL/Services/ServicesService.cs`

Перед тим як надсилати метрики, користувач має зареєструвати сервіс під своїм ключем (`POST /api/services { name }`). Імена унікальні в межах ключа. Якщо метрика приходить для незареєстрованого імені — `IngestAsync` кидає виняток. Це навмисний дизайн: API не створює сервіси автоматично, аби уникнути захаращення таблиці випадковими або помилково набраними іменами.

### `MetricsService`

`MonitoringSystem.BLL/Services/MetricsService.cs`

Центральний сервіс. На кожен ingest:

1. Резолвить `ServiceEntity` за `(apiKeyId, serviceName)`.
2. У циклі будує `MetricPointEntity` і виконує online-аналіз Z-score (`AnomalyDetectionService.Analyze`); зловлені аномалії збирає в `AnomalyEntity`.
3. Оновлює in-memory кеш статусів сервісів (`ConcurrentDictionary<int serviceId, ServiceStatus>`).
4. Одним батчем зберігає метрики й аномалії в БД (`AddRangeAsync` + єдиний `SaveChangesAsync` на запит, без N+1 round-trip-ів).
5. Шле події `MetricsUpdated`, `AnomaliesDetected`, `ServiceStatusUpdated` у SignalR-групу `apiKey:{id}` — лише власник цього ключа їх бачить.

Також відповідає за batch-SrCnn (`AnalyzeSrCnnBatchAsync`) і за `RefreshServiceHealthAsync`, який викликається фоновим сервісом.

### `AnomalyDetectionService`

`MonitoringSystem.BLL/Services/AnomalyDetectionService.cs`

Дві стратегії виявлення:

| Метод | Алгоритм | Параметри | Сценарій |
|---|---|---|---|
| `Analyze(serviceId, name, point)` | **Z-score** | вікно 30 точок, мін. історія 5, поріг 2.5σ | online під час ingest |
| `AnalyzeBatchWithMlNet(...)` | **ML.NET SrCnn** (Spectral Residual + CNN, KDD 2019) | поріг 0.1–0.9, мін. 12 точок | пакетний аналіз архіву з UI |

Поріг SrCnn передається параметром і конфігурується слайдером у dashboard.

### Background-сервіси

- **`ServiceHealthBackgroundService`** — кожні `CheckIntervalSeconds` секунд перевіряє кеш статусів. Якщо для зареєстрованого сервісу останні метрики приходили давніше за `TimeoutSeconds` — перемикає `IsHealthy` і шле оновлення в SignalR-групу його власника.

### `MetricsHub` (SignalR)

`MonitoringSystem.BLL/Hubs/MetricsHub.cs` → endpoint `/hub/metrics`

Підключення з `?apiKey=<key>` потрапляє в групу `apiKey:{id}`. Сервер пушить події тільки в групу власника, тож кожен користувач бачить виключно свій трафік.

Події:

- `MetricsUpdated` — список свіжих `MetricPointDTO` (після ingest);
- `AnomaliesDetected` — список `AnomalyResult` (якщо щось зловив Z-score);
- `ServiceStatusUpdated` — повний снапшот статусів усіх сервісів власника.

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

- `ApiKeys` — згенеровані ключі (`Key` unique, `Owner`, `IsActive`, `CreatedAt`, `LastUsedAt`).
- `Services` — зареєстровані сервіси (`unique(ApiKeyId, Name)`).
- `MetricPoints` — історія метрик (індекс за `(ServiceId, MetricName, Timestamp)`). `Tags` зберігаються як JSON.
- `Anomalies` — виявлені аномалії (індекс за `(ServiceId, DetectedAt)`).

`OnDelete(Cascade)` для `ApiKey → Service → (MetricPoint, Anomaly)` — видалення ключа повністю чистить дані власника.

### Seed демо-даних

`MonitoringSystem.DAL/DependencyInjection.cs::SeedAsync` при старті:

1. Якщо ключа `mk_dev_demo_user_key_0000000000000001` ще немає — створює його (owner: `dev-team`).
2. Додає під ним три сервіси: `OrderService`, `PaymentService`, `UserService`.

Це той самий ключ і ті самі імена, які hardcoded у `ServiceSimulator/Program.cs`, тож симулятор працює одразу після першого запуску API.

Створити міграцію після зміни моделей:

```powershell
dotnet ef migrations add <Name> -p MonitoringSystem.DAL -s MonitoringSystem.API
```

## Dashboard

Single-page UI у `MonitoringSystem.API/wwwroot/`. П'ять вкладок:

| Сторінка | Доступ без ключа | Що показує |
|---|---|---|
| **Дашборд** | так | Real-time картки сервісів, графіки response time / memory, потік аномалій через SignalR (з датою і часом виявлення) |
| **Метрики** | ні | Статус інстансів + пошук метрик за діапазоном з графіком та таблицею |
| **Аномалії** | ні | Архів виявлених аномалій з фільтрами за сервісом / метрикою / часом |
| **SrCnn архів** | ні | Пакетний SrCnn-аналіз архівних даних — див. нижче |
| **Мій профіль** | так | Інфо про поточний ключ, реєстрація / список сервісів, генерація нових ключів |

Без ключа кнопки закритих вкладок ховаються (`refreshNavLockState` + capture-phase nav guard у `config.js`). При 401/403 від API ключ автоматично чиститься з `localStorage` і користувача викидає на дашборд.

Файли:

- `wwwroot/index.html` — розмітка
- `wwwroot/css/main.css` — стилі (тема, близька до Grafana dark)
- `wwwroot/js/config.js` — `apiFetch`, `toast`, форматтери, навігація, nav guard
- `wwwroot/js/page-metrics.js`, `page-anomalies.js`, `page-srcnn.js`, `page-keys.js` — логіка вкладок
- `wwwroot/js/signalr-handler.js` — підписка на події hub-а (підключається з `?apiKey=<key>`)

`API_URL` у `wwwroot/js/config.js` за замовчуванням — `http://localhost:5169`.

API-ключ для запитів зберігається в `localStorage` під ключем `apiKey` і додається в кожен fetch як `X-API-KEY`. Поле для введення ключа є в шапці dashboard-а.

### SrCnn архівний аналіз

Окрема вкладка для пакетного аналізу історичних метрик алгоритмом SrCnn. Дозволяє:

- обрати сервіс (зі списку власних), метрику та довільний часовий діапазон;
- регулювати **чутливість 0.1–0.9** через slider (поріг SrCnn);
- отримати графік ряду з виділеними червоними точками аномалій;
- окремий bar-chart `Anomaly Score за часом` з кольорами за severity;
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

Міграції застосуються автоматично, демо-ключ і три сервіси насіються при першому старті. URL — `http://localhost:5169` (див. `Properties/launchSettings.json`).

Якщо потрібен інший порт:

```powershell
$env:ASPNETCORE_URLS="http://localhost:5000"
dotnet run --project MonitoringSystem.API
```

Тоді `API_URL` у `wwwroot/js/config.js` теж треба підправити.

### 3. Запустити симулятор (готовий шлях)

Завдяки seed-у симулятор працює одразу — він використовує hardcoded ключ `mk_dev_demo_user_key_0000000000000001` і сервіси `OrderService`, `PaymentService`, `UserService`, які вже існують.

```powershell
dotnet run --project ServiceSimulator
```

Кожні 2 секунди надсилає метрики `system.cpu_percent`, `system.memory_mb`, `http.response_time_ms`, `http.requests_per_second` для трьох сервісів. Кожні 20 ітерацій — штучна аномалія для `PaymentService` (CPU 95–100%, response 2–3s).

### 4. Відкрити dashboard

```text
http://localhost:5169/
```

Вставити демо-ключ у поле `X-API-KEY` у шапці → зберегти. У вкладці **Мій профіль** з'являться три демо-сервіси, у **Дашборді** — їхні метрики в real-time.

### 5. Свій сценарій (новий користувач)

Через UI (**Мій профіль** → «Згенерувати новий API ключ»):

```http
POST /api/keys
Content-Type: application/json

{ "owner": "backend-team" }
```

У відповіді — ключ. Зберігай — він показується тільки раз. Потім:

```http
POST /api/services
X-API-KEY: <твій ключ>
Content-Type: application/json

{ "name": "PaymentService" }
```

Після цього можна слати метрики на `POST /api/metrics/ingest` від імені `PaymentService`.

## Endpoints

Усе під `/api/*` (крім `POST /api/keys`) вимагає `X-API-KEY`. Усі дані фільтруються по власнику цього ключа. Повна специфікація — Swagger UI на `/swagger` (у Development автоматично додається поле `X-API-KEY` до кожної операції).

### Сервіси

```http
POST   /api/services           # зареєструвати сервіс під поточним ключем
GET    /api/services           # список своїх сервісів
DELETE /api/services/{id}      # видалити свій сервіс
```

```json
POST /api/services
{ "name": "PaymentService" }
```

### Метрики

```http
POST /api/metrics/ingest
GET  /api/metrics?service=...&metric=...&from=...&to=...
GET  /api/metrics/services
GET  /api/metrics/names
```

- `/ingest` — приймає метрики; вимагає, щоб `serviceName` уже був зареєстрований під цим ключем.
- `GET /api/metrics` — повертає точки для конкретного сервісу й метрики (`from` / `to` опціональні: за замовчуванням `[now-1h, now]`).
- `/services` — поточний статус усіх сервісів власника (живий снапшот з in-memory кешу).
- `/names` — унікальні імена метрик, які приходили від сервісів власника (для UI-селектів).

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
GET  /api/anomalies?metricName=...&count=20
POST /api/anomalies/srcnn-batch
```

- `GET /api/anomalies` — останні `count` аномалій по всіх сервісах власника, опційно фільтрується за `metricName`.
- `POST /api/anomalies/srcnn-batch` — пакетний SrCnn-аналіз з регульованою чутливістю.

```json
POST /api/anomalies/srcnn-batch
{
  "serviceName": "PaymentService",
  "metricName": "http.response_time_ms",
  "from": "2026-05-25T00:00:00Z",
  "to": "2026-05-25T23:59:59Z",
  "sensitivity": 0.3
}
```

Відповідь містить повний ряд (`points`), окремо аномалії (`anomalies`), розподіл за severity, `processingTimeMs`, агреговані `MaxScore` / `AverageScore`.

### API ключі

```http
POST   /api/keys           # створити (без авторизації)
GET    /api/keys/me        # інфо про поточний ключ (за заголовком)
DELETE /api/keys/{id}      # деактивувати свій ключ (тільки власний id)
```

`POST /api/keys` повертає сирий ключ один раз — у GET він буде вже у вигляді `mk_XXXX...`.

## Моделі

### `MetricPoint` (DTO)

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

### `ApiKeyDTO`

```json
{
  "id": 1,
  "key": "mk_demo_...",
  "owner": "dev-team",
  "isActive": true,
  "createdAt": "2026-05-25T12:00:00Z",
  "lastUsedAt": "2026-05-25T12:34:56Z",
  "serviceCount": 3
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

Serilog → консоль + файли `MonitoringSystem.API/logs/monitoring-*.log` (rolling daily). Request-логування ввімкнено через `app.UseSerilogRequestLogging()`.

## Стек

- .NET 10, ASP.NET Core
- Entity Framework Core + Npgsql (PostgreSQL)
- ML.NET (`Microsoft.ML.TimeSeries`) — SrCnn
- SignalR — real-time push на dashboard
- Serilog — логування
- Swagger / Swashbuckle
- Chart.js — графіки на dashboard

## Що реалізовано згідно з темою диплому

- збір метрик від кількох розподілених сервісів через REST API;
- автентифікація сервісів через API ключі з ізоляцією даних кожного власника;
- явна реєстрація сервісів під ключем (whitelist), без автостворення;
- збереження історії метрик і виявлених аномалій у PostgreSQL;
- real-time dashboard через SignalR з груповою маршрутизацією (apiKey:{id});
- health-check інстансів через timeout-based фоновий сервіс;
- **два алгоритми виявлення аномалій**:
  - online Z-score для потокового аналізу,
  - batch ML.NET SrCnn для архівного аналізу з регульованою чутливістю;
- класифікація severity через `anomalyScore`;
- симуляція нормальної поведінки та штучних аномалій;
- single-page UI з ізоляцією приватних сторінок за наявністю ключа.
