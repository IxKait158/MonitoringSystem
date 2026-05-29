// js/page-anomalies.js — сторінка "Аномалії"

const ANOMALY_METRICS = [
    'http.response_time_ms',
    'system.memory_mb',
    'system.cpu_percent',
    'http.requests_total',
    'http.errors_total',
];

// Завантажити список сервісів у select
async function anomaliesLoadServices() {
    const sel = document.getElementById('an-service');
    sel.innerHTML = '<option value="">Всі сервіси</option>';
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
    } catch { }
}

// Заповнити селект метрик (з опцією "Всі метрики")
function anomaliesLoadMetrics() {
    const sel = document.getElementById('an-metric');
    if (!sel) return;
    sel.innerHTML = '<option value="">Всі метрики</option>';
    ANOMALY_METRICS.forEach(m => {
        const opt = document.createElement('option');
        opt.value = opt.textContent = m;
        sel.appendChild(opt);
    });
}

// Завантажити та відрендерити аномалії
async function anomaliesLoad() {
    const btn    = document.getElementById('an-load-btn');
    const tbody  = document.getElementById('an-tbody');
    const count  = parseInt(document.getElementById('an-count').value) || 50;

    btn.disabled = true;
    btn.innerHTML = '<span class="spinner"></span> Завантаження...';
    tbody.innerHTML = `<tr><td colspan="6" class="empty loading">Завантаження...</td></tr>`;

    try {
        const params = new URLSearchParams({ count });
        const anomalies = await apiFetch(`/api/anomalies?${params}`);

        if (!anomalies || !anomalies.length) {
            tbody.innerHTML = `<tr><td colspan="6" class="empty">Аномалій не знайдено</td></tr>`;
            return;
        }

        // Клієнтська фільтрація по сервісу, метриці і часу
        const svcFilter    = document.getElementById('an-service').value;
        const metricFilter = document.getElementById('an-metric')?.value || '';
        const fromFilter   = document.getElementById('an-from').value ? new Date(document.getElementById('an-from').value) : null;
        const toFilter     = document.getElementById('an-to').value   ? new Date(document.getElementById('an-to').value)   : null;

        let filtered = anomalies;
        if (svcFilter)    filtered = filtered.filter(a => a.serviceName === svcFilter);
        if (metricFilter) filtered = filtered.filter(a => a.metricName === metricFilter);
        if (fromFilter)   filtered = filtered.filter(a => new Date(a.detectedAt) >= fromFilter);
        if (toFilter)     filtered = filtered.filter(a => new Date(a.detectedAt) <= toFilter);

        if (!filtered.length) {
            tbody.innerHTML = `<tr><td colspan="6" class="empty">За вказаними фільтрами нічого не знайдено</td></tr>`;
            return;
        }

        tbody.innerHTML = filtered.map(a => `
      <tr>
        <td>${fmtDate(a.detectedAt)}</td>
        <td style="color:var(--accent);font-family:var(--mono)">${a.serviceName}</td>
        <td class="td-muted">${a.metricName}</td>
        <td style="font-weight:700">${fmtNum(a.value, 2)}</td>
        <td class="td-muted">${fmtNum(a.expectedValue, 2)}</td>
        <td><span class="badge badge-${a.severity?.toLowerCase() === 'critical' ? 'crit' : a.severity?.toLowerCase() === 'warning' ? 'warn' : 'info'}">${a.severity ?? '—'}</span></td>
      </tr>`).join('');

    } catch (err) {
        tbody.innerHTML = `<tr><td colspan="6" class="empty" style="color:var(--red)">${err.message}</td></tr>`;
        toast(err.message, 'error');
    } finally {
        btn.disabled = false;
        btn.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M4 12v-2a8 8 0 1 1 16 0v2"/></svg> Завантажити`;
    }
}

// Init
document.addEventListener('DOMContentLoaded', () => {
    anomaliesLoadServices();
    anomaliesLoadMetrics();

    // Дефолт: остання година
    const now = new Date();
    const hourAgo = new Date(now - 3600_000);
    document.getElementById('an-to').value   = toLocalInputValue(now);
    document.getElementById('an-from').value = toLocalInputValue(hourAgo);
    document.getElementById('cmp-to').value   = toLocalInputValue(now);
    document.getElementById('cmp-from').value = toLocalInputValue(new Date(now - 86400_000));
});