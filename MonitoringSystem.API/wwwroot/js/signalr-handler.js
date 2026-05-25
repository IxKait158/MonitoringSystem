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


    const grid = document.getElementById('services-grid');
        const rtClass  = rt  > 1000 ? 'crit' : rt  > 200 ? 'warn' : 'ok';
        const cpuClass = cpu > 80   ? 'crit' : cpu > 50  ? 'warn' : 'ok';
          <div class="service-card ${s.anomalyCount > 0 ? 'anomaly' : ''} ${s.isHealthy ? '' : 'unhealthy'}">
            <div class="service-name">${s.serviceName}</div>
            <div class="metric-row"><span class="metric-label">Response Time</span><span class="metric-value ${rtClass}">${rt.toFixed(0)}ms</span></div>
            <div class="metric-row"><span class="metric-label">Memory</span><span class="metric-value ok">${mem.toFixed(0)}MB</span></div>
            <div class="metric-row"><span class="metric-label">CPU</span><span class="metric-value ${cpuClass}">${cpu.toFixed(1)}%</span></div>
            <div class="metric-row"><span class="metric-label">Health</span><span class="metric-value ${s.isHealthy ? 'ok' : 'warn'}">${s.isHealthy ? 'Healthy' : 'Unhealthy'}</span></div>
            <div class="metric-row"><span class="metric-label">Аномалії</span><span class="metric-value ${s.anomalyCount > 0 ? 'crit' : 'ok'}">${s.anomalyCount}</span></div>
          </div>`;

    const list = document.getElementById('anomaly-list');

    const item = document.createElement('div');
    item.className = 'anomaly-item';
    item.innerHTML = `
      <span class="anomaly-severity severity-${a.severity}">${a.severity}</span>
      <span class="anomaly-service">${a.serviceName}</span>
    list.prepend(item);
    if (list.children.length > 20) list.lastChild.remove();
    });
});

connection.on('ServiceStatusUpdated', renderServices);

connection.on('AnomaliesDetected', (anomalies) => {
    anomalies.forEach(prependAnomaly);
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

// ===== Initial REST load: щоб дашборд показав поточний стан без чекання SignalR =====

async function dashboardInitialLoad() {
    try {
        const statuses = await apiFetch('/api/metrics/services');
        renderServices(statuses);
    } catch { /* без ключа empty state залишається */ }

    try {
        const anomalies = await apiFetch('/api/anomalies?count=20');
        if (Array.isArray(anomalies) && anomalies.length) {
            // У зворотному порядку — щоб найсвіжіше потрапило вгору списку
            [...anomalies].reverse().forEach(prependAnomaly);
        }
    } catch { /* без ключа empty state залишається */ }
}

dashboardInitialLoad();
start();
