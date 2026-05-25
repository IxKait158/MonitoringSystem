// js/page-anomalies.js — сторінка "Аномалії"

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

        // Клієнтська фільтрація по сервісу і часу
        const svcFilter  = document.getElementById('an-service').value;
        const fromFilter = document.getElementById('an-from').value ? new Date(document.getElementById('an-from').value) : null;
        const toFilter   = document.getElementById('an-to').value   ? new Date(document.getElementById('an-to').value)   : null;

        let filtered = anomalies;
        if (svcFilter)  filtered = filtered.filter(a => a.serviceName === svcFilter);
        if (fromFilter) filtered = filtered.filter(a => new Date(a.detectedAt) >= fromFilter);
        if (toFilter)   filtered = filtered.filter(a => new Date(a.detectedAt) <= toFilter);

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

// Порівняння алгоритмів
async function compareAlgorithms() {
    const btn    = document.getElementById('cmp-btn');
    const result = document.getElementById('cmp-result');
    const service = document.getElementById('cmp-service').value?.trim();
    const metric  = document.getElementById('cmp-metric').value?.trim();
    const from    = localToUtcIso(document.getElementById('cmp-from').value);
    const to      = localToUtcIso(document.getElementById('cmp-to').value);

    if (!service || !metric) { toast('Вкажіть сервіс і метрику', 'error'); return; }

    btn.disabled = true;
    btn.innerHTML = '<span class="spinner"></span> Аналіз...';
    result.innerHTML = '<div class="loading" style="padding:20px 0">Виконується порівняння алгоритмів...</div>';

    try {
        const body = { serviceName: service, metricName: metric, from, to };
        const data = await apiFetch('/api/anomalies/compare', {
            method: 'POST',
            body: JSON.stringify(body),
        });

        const zs  = data.zScore  || {};
        const src = data.srCnn   || {};

        result.innerHTML = `
      <div class="compare-grid">
        <div class="compare-card">
          <div class="compare-card-title">Z-score (real-time)</div>
          <div class="stat-row"><span class="stat-label">Аномалій виявлено</span><span class="stat-value">${zs.anomalyCount ?? '—'}</span></div>
          <div class="stat-row"><span class="stat-label">Precision</span><span class="stat-value">${zs.precision != null ? fmtNum(zs.precision, 3) : '—'}</span></div>
          <div class="stat-row"><span class="stat-label">Recall</span><span class="stat-value">${zs.recall    != null ? fmtNum(zs.recall,    3) : '—'}</span></div>
          <div class="stat-row"><span class="stat-label">F1-score</span><span class="stat-value" style="color:var(--accent)">${zs.f1Score   != null ? fmtNum(zs.f1Score,   3) : '—'}</span></div>
          <div class="stat-row"><span class="stat-label">Час обробки</span><span class="stat-value">${zs.processingTimeMs != null ? zs.processingTimeMs + ' мс' : '—'}</span></div>
        </div>
        <div class="compare-card">
          <div class="compare-card-title">SrCnn (пакетний)</div>
          <div class="stat-row"><span class="stat-label">Аномалій виявлено</span><span class="stat-value">${src.anomalyCount ?? '—'}</span></div>
          <div class="stat-row"><span class="stat-label">Precision</span><span class="stat-value">${src.precision != null ? fmtNum(src.precision, 3) : '—'}</span></div>
          <div class="stat-row"><span class="stat-label">Recall</span><span class="stat-value">${src.recall    != null ? fmtNum(src.recall,    3) : '—'}</span></div>
          <div class="stat-row"><span class="stat-label">F1-score</span><span class="stat-value" style="color:var(--purple)">${src.f1Score   != null ? fmtNum(src.f1Score,   3) : '—'}</span></div>
          <div class="stat-row"><span class="stat-label">Час обробки</span><span class="stat-value">${src.processingTimeMs != null ? src.processingTimeMs + ' мс' : '—'}</span></div>
        </div>
      </div>`;
        toast('Порівняння завершено', 'success');

    } catch (err) {
        result.innerHTML = `<div class="empty" style="color:var(--red)">${err.message}</div>`;
        toast(err.message, 'error');
    } finally {
        btn.disabled = false;
        btn.innerHTML = 'Порівняти';
    }
}

// Init
document.addEventListener('DOMContentLoaded', () => {
    anomaliesLoadServices();

    // Дефолт: остання година
    const now = new Date();
    const hourAgo = new Date(now - 3600_000);
    document.getElementById('an-to').value   = now.toISOString().slice(0, 16);
    document.getElementById('an-from').value = hourAgo.toISOString().slice(0, 16);
    document.getElementById('cmp-to').value   = now.toISOString().slice(0, 16);
    document.getElementById('cmp-from').value = new Date(now - 86400_000).toISOString().slice(0, 16);
});