// js/page-metrics.js — сторінка "Метрики"

let metricsChart = null;
let metricsInitialized = false;

// Ініціалізація сторінки (викликається при першому відкритті)
async function metricsInit() {
    if (metricsInitialized) return;
    metricsInitialized = true;

    await Promise.all([metricsInitSelects(), metricsLoadServices()]);

    const now = new Date();
    const hourAgo = new Date(now - 3600_000);
    document.getElementById('met-to').value = toLocalInputValue(now);
    document.getElementById('met-from').value = toLocalInputValue(hourAgo);
}

// Заповнити select метрик
async function metricsInitSelects() {
    const sel = document.getElementById('met-metric');
    sel.innerHTML = '<option value="">Оберіть метрику...</option>';
    try {
        const names = await apiFetch('/api/metrics/names');
        if (Array.isArray(names)) {
            names.forEach(m => {
                const opt = document.createElement('option');
                opt.value = opt.textContent = m;
                sel.appendChild(opt);
            });
        }
    } catch {
    }
}

// Завантажити сервіси
async function metricsLoadServices() {
    const sel = document.getElementById('met-service');
    sel.innerHTML = '<option value="">Оберіть сервіс...</option>';
    try {
        const services = await apiFetch('/api/metrics/services');
        if (Array.isArray(services)) {
            services.forEach(s => {
                const opt = document.createElement('option');
                opt.value = s.serviceName;
                opt.textContent = s.serviceName;
                sel.appendChild(opt);
            });
        }
    } catch {
    }
}

// Завантажити метрики і відобразити
async function metricsLoad() {
    const service = document.getElementById('met-service').value?.trim();
    const metric = document.getElementById('met-metric').value?.trim();
    const from = localToUtcIso(document.getElementById('met-from').value);
    const to = localToUtcIso(document.getElementById('met-to').value);
    const btn = document.getElementById('met-load-btn');
    const tbody = document.getElementById('met-tbody');
    const chartWrap = document.getElementById('met-chart-wrap');

    if (!service) {
        toast('Оберіть сервіс', 'error');
        return;
    }
    if (!metric) {
        toast('Оберіть метрику', 'error');
        return;
    }

    btn.disabled = true;
    btn.innerHTML = '<span class="spinner"></span>';
    tbody.innerHTML = `<tr><td colspan="4" class="empty loading">Завантаження...</td></tr>`;
    chartWrap.style.display = 'none';

    try {
        const params = new URLSearchParams({service, metric});
        if (from) params.set('from', from);
        if (to) params.set('to', to);

        const data = await apiFetch(`/api/metrics?${params}`);

        if (!data || !data.length) {
            tbody.innerHTML = `<tr><td colspan="4" class="empty">Дані за вказаний період відсутні</td></tr>`;
            return;
        }

        // Таблиця
        tbody.innerHTML = data.map(m => `
      <tr>
        <td>${fmtDate(m.timestamp)}</td>
        <td style="color:var(--accent)">${m.serviceName}</td>
        <td class="td-muted">${m.metricName}</td>
        <td style="font-family:var(--mono);font-weight:700">${fmtNum(m.value, 2)}</td>
      </tr>`).join('');

        // Графік
        chartWrap.style.display = 'block';
        const labels = data.map(m => fmtTime(m.timestamp));
        const values = data.map(m => m.value);

        if (metricsChart) metricsChart.destroy();
        metricsChart = new Chart(document.getElementById('met-chart'), {
            type: 'line',
            data: {
                labels,
                datasets: [{
                    label: metric,
                    data: values,
                    borderColor: '#38bdf8',
                    backgroundColor: 'rgba(56,189,248,.1)',
                    borderWidth: 1.5,
                    tension: 0.3,
                    pointRadius: 2,
                    fill: true,
                }]
            },
            options: {
                responsive: true,
                animation: false,
                plugins: {legend: {display: false}},
                scales: {
                    x: {ticks: {maxTicksLimit: 12, color: '#64748b', font: {size: 10}}, grid: {color: '#1f2d40'}},
                    y: {grid: {color: '#1f2d40'}, ticks: {color: '#64748b', font: {size: 10}}}
                }
            }
        });

        toast(`Завантажено ${data.length} точок`, 'success');

    } catch (err) {
        tbody.innerHTML = `<tr><td colspan="4" class="empty" style="color:var(--red)">${err.message}</td></tr>`;
        toast(err.message, 'error');
    } finally {
        btn.disabled = false;
        btn.textContent = 'Завантажити';
    }
}

// Завантажити статус сервісів
async function servicesStatusLoad() {
    const tbody = document.getElementById('svc-tbody');
    tbody.innerHTML = `<tr><td colspan="5" class="empty loading">Завантаження...</td></tr>`;
    try {
        const statuses = await apiFetch('/api/metrics/services');
        if (!statuses?.length) {
            tbody.innerHTML = `<tr><td colspan="5" class="empty">Активних сервісів не знайдено</td></tr>`;
            return;
        }
        tbody.innerHTML = statuses.map(s => {
            const rt = s.latestMetrics?.['http.response_time_ms'];
            const mem = s.latestMetrics?.['system.memory_mb'];
            const rtCls = rt != null ? metricClass('http.response_time_ms', rt) : 'ok';
            return `
        <tr>
          <td style="color:var(--accent);font-family:var(--mono)">${s.serviceName}</td>
          <td><span class="badge ${s.isHealthy ? 'badge-active' : 'badge-revoked'}">${s.isHealthy ? 'OK' : 'Offline'}</span></td>
          <td style="font-family:var(--mono)" class="${rtCls}">${rt != null ? fmtNum(rt, 0) + ' ms' : '—'}</td>
          <td style="font-family:var(--mono)">${mem != null ? fmtNum(mem, 0) + ' MB' : '—'}</td>
          <td style="font-family:var(--mono);color:var(--${s.anomalyCount > 0 ? 'red' : 'muted'})">${s.anomalyCount}</td>
        </tr>`;
        }).join('');
    } catch (err) {
        tbody.innerHTML = `<tr><td colspan="5" class="empty" style="color:var(--red)">${err.message}</td></tr>`;
    }
}

