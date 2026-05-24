# MonitoringSystem

Розподілена система моніторингу мікросервісів з автоматичним виявленням аномалій.

Проєкт демонструє повний цикл моніторингу: мікросервіси або симулятор надсилають метрики в API, API зберігає їх у PostgreSQL, виконує виявлення аномалій, підтримує health-status інстансів сервісів і передає оновлення на dashboard через SignalR.

## Склад рішення

Рішення `MonitoringSystem.sln` складається з трьох основних проєктів:

| Проєкт | Призначення |
| --- | --- |
| `MonitoringSystem.API` | Основний backend: прийом метрик, збереження в БД, anomaly detection, health-check, SignalR, dashboard static files |
| `MonitoringSystem.Shared` | Спільні DTO/моделі: метрики, запити ingestion, статуси сервісів, результати аномалій |
| `ServiceSimulator` | Консольний симулятор мікросервісів, який генерує нормальні метрики та штучні аномалії |

## Архітектура

Загальний потік даних:

```text
ServiceSimulator / Microservice
        |
        | POST /api/metrics/ingest
        v
MonitoringSystem.API
        |
        | збереження MetricPointEntity
        | аналіз AnomalyDetectionService
        | оновлення ServiceStatus
        v
PostgreSQL
        |
        v
SignalR Hub /hub/metrics
        |
        v
Dashboard
```

Окремо middleware самого API збирає внутрішні HTTP-метрики API: час відповіді, кількість запитів, помилки та використання пам'яті. Ці метрики не записуються напряму під час HTTP-запиту, а потрапляють у background queue.

## Основні компоненти API

### `MetricsController`

Файл: `MonitoringSystem.API/Controllers/MetricsController.cs`

Відповідає за HTTP API для роботи з метриками:

- `POST /api/metrics/ingest` - приймає метрики від зовнішніх сервісів або симулятора.
- `GET /api/metrics` - повертає історію метрик за сервісом, інстансом, назвою метрики та часовим діапазоном.
- `GET /api/metrics/services` - повертає поточний статус усіх відомих інстансів сервісів.

### `MetricsService`

Файл: `MonitoringSystem.API/Services/MetricsService.cs`

Центральний сервіс обробки метрик:

- нормалізує `ServiceName` та `InstanceId`;
- зберігає метрики в PostgreSQL;
- запускає аналіз аномалій;
- зберігає знайдені аномалії;
- оновлює in-memory статус сервісів;
- надсилає події на dashboard через SignalR.

Статуси сервісів ведуться не тільки за назвою сервісу, а за парою:

```text
ServiceName + InstanceId
```

Це важливо для розподіленої системи, бо один мікросервіс може мати кілька інстансів.

### `MetricIngestionQueue`

Файли:

- `MonitoringSystem.API/Services/MetricIngestionQueue.cs`
- `MonitoringSystem.API/Services/MetricIngestionBackgroundService.cs`

Це background queue для метрик, які збирає middleware самого API.

Middleware не викликає `MetricsService.IngestAsync` напряму, бо `MetricsService` використовує scoped-залежності, зокрема `DbContext`. Замість цього middleware кладе `MetricIngestionRequest` у чергу, а `MetricIngestionBackgroundService` обробляє його в окремому DI scope.

Це зменшує зв'язність між HTTP request pipeline і записом метрик у БД.

### `MetricsCollectionMiddleware`

Файл: `MonitoringSystem.API/Middlewares/MetricsCollectionMiddleware.cs`

Збирає технічні метрики самого API:

- `http.response_time_ms`
- `system.memory_mb`
- `http.requests_total`
- `http.errors_total`

Для API-інстанса використовується `InstanceId` з environment variable:

```text
MONITORING_INSTANCE_ID
```

Якщо змінна не задана, використовується `Environment.MachineName`.

### `ServiceHealthBackgroundService`

Файли:

- `MonitoringSystem.API/Services/ServiceHealthBackgroundService.cs`
- `MonitoringSystem.API/Services/ServiceHealthOptions.cs`

Фоновий сервіс, який періодично перевіряє, чи давно інстанс сервісу надсилав метрики.

Налаштування в `MonitoringSystem.API/appsettings.json`:

```json
{
  "ServiceHealth": {
    "TimeoutSeconds": 30,
    "CheckIntervalSeconds": 5
  }
}
```

Логіка:

- якщо інстанс надсилав метрики протягом останніх `TimeoutSeconds`, він `Healthy`;
- якщо метрик давно не було, він стає `Unhealthy`;
- dashboard отримує оновлення через SignalR.

### `AnomalyDetectionService`

Файл: `MonitoringSystem.API/Services/AnomalyDetectionService.cs`

Виявляє аномалії у метриках.

Зараз реалізовано:

- онлайн-аналіз через Z-score;
- пакетний аналіз історичного часового ряду через ML.NET SrCnn.

Для онлайн-аналізу історія ведеться окремо для комбінації:

```text
ServiceName + InstanceId + MetricName
```

Це дозволяє не змішувати метрики різних інстансів одного сервісу.

### `MetricsHub`

Файл: `MonitoringSystem.API/Hubs/MetricsHub.cs`

SignalR hub для real-time оновлень dashboard.

Endpoint:

```text
/hub/metrics
```

Події, які отримує dashboard:

- `MetricsUpdated`
- `AnomaliesDetected`
- `ServiceStatusUpdated`

## Моделі даних

### `MetricIngestionRequest`

Запит на прийом метрик:

```json
{
  "serviceName": "PaymentService",
  "instanceId": "PaymentService-instance-1",
  "metrics": []
}
```

### `MetricPoint`

Одна точка метрики:

```json
{
  "serviceName": "PaymentService",
  "instanceId": "PaymentService-instance-1",
  "metricName": "http.response_time_ms",
  "value": 123.4,
  "timestamp": "2026-05-24T12:00:00Z",
  "tags": {
    "method": "GET",
    "path": "/api/orders"
  }
}
```

### `ServiceStatus`

Поточний стан інстанса сервісу:

```json
{
  "serviceName": "PaymentService",
  "instanceId": "PaymentService-instance-1",
  "isHealthy": true,
  "lastSeen": "2026-05-24T12:00:00Z",
  "anomalyCount": 2,
  "latestMetrics": {
    "system.cpu_percent": 42.5,
    "http.response_time_ms": 85.0
  }
}
```

### `AnomalyResult`

Результат виявлення аномалії:

```json
{
  "serviceName": "PaymentService",
  "instanceId": "PaymentService-instance-1",
  "metricName": "http.response_time_ms",
  "value": 2500.0,
  "expectedValue": 70.0,
  "anomalyScore": 1.0,
  "isAnomaly": true,
  "severity": "Critical"
}
```

## База даних

Використовується PostgreSQL через Entity Framework Core.

Connection string знаходиться в `MonitoringSystem.API/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=monitoring_db;Username=postgres;Password=1234"
  }
}
```

Основні таблиці:

- `MetricPoints` - історія метрик;
- `Anomalies` - знайдені аномалії.

Індекси оптимізовані під пошук за:

- `ServiceName`
- `InstanceId`
- `MetricName`
- `Timestamp`
- `DetectedAt`

Після зміни моделей потрібно створити та застосувати EF Core migration:

```powershell
dotnet ef migrations add AddInstanceIdAndServiceHealth -p MonitoringSystem.API
dotnet ef database update -p MonitoringSystem.API
```

## Dashboard

Файли:

- `MonitoringSystem.API/wwwroot/dashboard/index.html`
- `MonitoringSystem.API/wwwroot/js/script.js`
- `MonitoringSystem.API/wwwroot/styles/main.css`

Dashboard показує:

- картки сервісів та інстансів;
- останні значення CPU, memory, response time;
- health status інстанса;
- кількість аномалій;
- графіки response time та memory;
- список останніх аномалій.

Dashboard підключається до SignalR:

```js
const API_URL = 'http://localhost:5000';
```

Якщо API запускається не на `5000`, потрібно змінити `API_URL` у `MonitoringSystem.API/wwwroot/js/script.js`.

## ServiceSimulator

Файл: `ServiceSimulator/Program.cs`

Симулятор генерує метрики для сервісів:

- `OrderService`
- `PaymentService`
- `UserService`

Кожні 50 ітерацій для `PaymentService` створюється штучна аномалія:

- високий CPU;
- великий response time.

Симулятор використовує:

```text
MONITORING_API_URL
SIMULATOR_INSTANCE_ID
```

Якщо `MONITORING_API_URL` не заданий, використовується:

```text
http://localhost:5000
```

Приклад запуску з явним URL:

```powershell
$env:MONITORING_API_URL="http://localhost:5169"
$env:SIMULATOR_INSTANCE_ID="local-1"
dotnet run --project ServiceSimulator
```

## Запуск

### 1. Підготувати PostgreSQL

Створити базу даних:

```sql
CREATE DATABASE monitoring_db;
```

Перевірити connection string у:

```text
MonitoringSystem.API/appsettings.json
```

### 2. Застосувати міграції

```powershell
dotnet ef database update -p MonitoringSystem.API
```

Якщо міграцій ще немає після останніх змін:

```powershell
dotnet ef migrations add AddInstanceIdAndServiceHealth -p MonitoringSystem.API
dotnet ef database update -p MonitoringSystem.API
```

### 3. Запустити API

Варіант 1: стандартний launch profile:

```powershell
dotnet run --project MonitoringSystem.API
```

За `launchSettings.json` API може стартувати на:

```text
http://localhost:5169
https://localhost:7246
```

Варіант 2: запуск на `5000`, щоб збігалося з dashboard і simulator:

```powershell
$env:ASPNETCORE_URLS="http://localhost:5000"
$env:MONITORING_INSTANCE_ID="MonitoringAPI-local"
dotnet run --project MonitoringSystem.API
```

### 4. Відкрити dashboard

Якщо API запущений на `5000`:

```text
http://localhost:5000/dashboard/index.html
```

Якщо API запущений на `5169`:

```text
http://localhost:5169/dashboard/index.html
```

У цьому випадку також потрібно змінити `API_URL` у `MonitoringSystem.API/wwwroot/js/script.js` на `http://localhost:5169`.

### 5. Запустити симулятор

Якщо API на `5000`:

```powershell
dotnet run --project ServiceSimulator
```

Якщо API на `5169`:

```powershell
$env:MONITORING_API_URL="http://localhost:5169"
dotnet run --project ServiceSimulator
```

## Основні endpoints

### Health API

```http
GET /health
```

Відповідь:

```json
{
  "status": "healthy",
  "timestamp": "2026-05-24T12:00:00Z"
}
```

### Прийом метрик

```http
POST /api/metrics/ingest
```

Приклад body:

```json
{
  "serviceName": "PaymentService",
  "instanceId": "payment-1",
  "metrics": [
    {
      "metricName": "http.response_time_ms",
      "value": 2500,
      "timestamp": "2026-05-24T12:00:00Z",
      "tags": {
        "endpoint": "/api/payments"
      }
    }
  ]
}
```

### Отримати історію метрик

```http
GET /api/metrics?service=PaymentService&instance=payment-1&metric=http.response_time_ms&from=2026-05-24T11:00:00Z&to=2026-05-24T12:00:00Z
```

Параметр `instance` необов'язковий. Якщо його не передати, API поверне метрики всіх інстансів вибраного сервісу.

### Отримати статуси сервісів

```http
GET /api/metrics/services
```

### Отримати останні аномалії

```http
GET /api/anomalies?count=20
```

## Логування

Використовується Serilog.

Логи пишуться:

- у консоль;
- у файли `MonitoringSystem.API/logs/monitoring-*.log`.

## Що вже відповідає темі диплому

У системі вже реалізовано:

- збір метрик від кількох сервісів;
- підтримка кількох інстансів одного сервісу через `InstanceId`;
- збереження історії метрик;
- real-time dashboard;
- health-check інстансів сервісів;
- автоматичне виявлення аномалій;
- класифікація severity через anomaly score;
- симуляція нормальної поведінки та штучних аномалій.

## Ідеї для подальшого розвитку

Корисні наступні кроки:

- додати endpoint для порівняння алгоритмів anomaly detection;
- реалізувати додаткові алгоритми: moving average, EWMA, MAD;
- додати Docker Compose для API, PostgreSQL, simulator і dashboard;
- додати OpenTelemetry traces;
- зробити авторизацію для API ingestion;
- додати retention policy для старих метрик;
- додати unit/integration tests для anomaly detection і health-check логіки.
## Anomaly algorithm comparison endpoint

The API exposes a comparison endpoint for historical metric data:

```http
POST /api/anomalies/compare
```

Example request:

```json
{
  "serviceName": "PaymentService",
  "instanceId": "PaymentService-local-1",
  "metricName": "http.response_time_ms",
  "from": "2026-05-24T11:00:00Z",
  "to": "2026-05-24T12:00:00Z"
}
```

The response includes total points and per-algorithm results for:

- `Z-score`
- `Moving average`
- `EWMA`
- `ML.NET SrCnn`, when there are enough points for the ML.NET time-series algorithm
