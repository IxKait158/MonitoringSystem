const API_URL = 'http://localhost:5169';
const MAX_POINTS = 30;

Chart.defaults.color = '#64748b';
Chart.defaults.borderColor = '#1f2d40';

const chartConfig = (label, color) => ({
    type: 'line',
    data: {
        labels: [],
        datasets: [{
            label,
            data: [],
            borderColor: color,
            backgroundColor: color + '20',
            borderWidth: 2,
            tension: 0.4,
            pointRadius: 2
        }]
    },
    options: {
        responsive: true,
        animation: false,
        scales: {x: {display: false}, y: {grid: {color: '#1f2d40'}}},
        plugins: {legend: {display: false}}
    }
});

const rtChart = new Chart(document.getElementById('responseTimeChart'), chartConfig('Response Time', '#38bdf8'));
const memChart = new Chart(document.getElementById('memoryChart'), chartConfig('Memory MB', '#34d399'));

function addChartPoint(chart, value) {
    const time = new Date().toLocaleTimeString('uk', {hour: '2-digit', minute: '2-digit', second: '2-digit'});
    chart.data.labels.push(time);
    chart.data.datasets[0].data.push(value);
    if (chart.data.labels.length > MAX_POINTS) {
        chart.data.labels.shift();
        chart.data.datasets[0].data.shift();
    }
    chart.update('none');
}

const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${API_URL}/hub/metrics`)
    .withAutomaticReconnect()
    .build();

connection.on('MetricsUpdated', (metrics) => {
    metrics.forEach(m => {
        if (m.metricName === 'http.response_time_ms') addChartPoint(rtChart, m.value);
        if (m.metricName === 'system.memory_mb') addChartPoint(memChart, m.value);
    });
});

connection.on('ServiceStatusUpdated', (statuses) => {
    const grid = document.getElementById('services-grid');
    grid.innerHTML = '';

    statuses.forEach(s => {
        const rt = s.latestMetrics['http.response_time_ms'] ?? 0;
        const mem = s.latestMetrics['system.memory_mb'] ?? 0;
        const cpu = s.latestMetrics['system.cpu_percent'] ?? 0;

        const rtClass = rt > 1000 ? 'crit' : rt > 200 ? 'warn' : 'ok';
        const cpuClass = cpu > 80 ? 'crit' : cpu > 50 ? 'warn' : 'ok';

        grid.innerHTML += `
      <div class="service-card ${s.anomalyCount > 0 ? 'anomaly' : ''} ${s.isHealthy ? '' : 'unhealthy'}">
        <div class="service-name">${s.serviceName}</div>
        <div class="metric-row"><span class="metric-label">Response Time</span><span class="metric-value ${rtClass}">${rt.toFixed(0)}ms</span></div>
        <div class="metric-row"><span class="metric-label">Memory</span><span class="metric-value ok">${mem.toFixed(0)}MB</span></div>
        <div class="metric-row"><span class="metric-label">CPU</span><span class="metric-value ${cpuClass}">${cpu.toFixed(1)}%</span></div>
        <div class="metric-row"><span class="metric-label">Health</span><span class="metric-value ${s.isHealthy ? 'ok' : 'warn'}">${s.isHealthy ? 'Healthy' : 'Unhealthy'}</span></div>
        <div class="metric-row"><span class="metric-label">Аномалії</span><span class="metric-value ${s.anomalyCount > 0 ? 'crit' : 'ok'}">${s.anomalyCount}</span></div>
      </div>`;
    });
});

connection.on('AnomaliesDetected', (anomalies) => {
    const list = document.getElementById('anomaly-list');
    if (list.children[0]?.style?.color) list.innerHTML = '';

    anomalies.forEach(a => {
        const item = document.createElement('div');
        item.className = 'anomaly-item';
        item.innerHTML = `
      <span class="anomaly-severity severity-${a.severity}">${a.severity}</span>
      <span class="anomaly-service">${a.serviceName}</span>
      <span class="anomaly-details">${a.metricName}: ${a.value.toFixed(2)} (очікувалось ~${a.expectedValue.toFixed(2)})</span>
      <span class="anomaly-time">${new Date().toLocaleTimeString('uk')}</span>`;
        list.prepend(item);
        if (list.children.length > 20) list.lastChild.remove();
    });
});

async function start() {
    try {
        await connection.start();
        document.getElementById('dot').classList.add('connected');
        document.getElementById('status-text').textContent = 'Підключено';
    } catch (e) {
        document.getElementById('status-text').textContent = 'Помилка підключення';
        setTimeout(start, 3000);
    }
}

connection.onreconnecting(() => {
    document.getElementById('dot').classList.remove('connected');
    document.getElementById('status-text').textContent = 'Перепідключення...';
});
connection.onreconnected(() => {
    document.getElementById('dot').classList.add('connected');
    document.getElementById('status-text').textContent = 'Підключено';
});

start();
