// js/page-srcnn.js — сторінка "SrCnn архівний аналіз"

let srcnnChart = null;
let srcnnScoreChart = null;
let srcnnLastResult = null;
let srcnnInitialized = false;

const SRCNN_METRICS = [
    'http.response_time_ms',
    'system.memory_mb',
    'system.cpu_percent',
    'http.requests_total',
    'http.errors_total',
];

// Ініціалізація сторінки (викликається при першому відкритті)
async function srcnnInit() {
    if (srcnnInitialized) return;
    srcnnInitialized = true;

    // Заповнити селект метрик
    const metricSel = document.getElementById('src-metric');
    SRCNN_METRICS.forEach(m => {
        const opt = document.createElement('option');
        opt.value = opt.textContent = m;
        metricSel.appendChild(opt);
    });

    // Завантажити сервіси
    const svcSel = document.getElementById('src-service');
    svcSel.innerHTML = '<option value="">Оберіть сервіс...</option>';
    try {
        const services = await apiFetch('/api/metrics/services');
        if (Array.isArray(services)) {
            services.forEach(s => {
                const opt = document.createElement('option');
                opt.value = s.serviceName;
                opt.textContent = s.serviceName;
                svcSel.appendChild(opt);
            });
        }
    } catch { }

    // Дефолтні дати: остання доба (в локальному часі ПК)
    const now = new Date();
    const hourAgo = new Date(now - 3600_000);
    document.getElementById('src-to').value = toLocalInputValue(now);
    document.getElementById('src-from').value = toLocalInputValue(hourAgo);
}

// Запуск SrCnn аналізу
async function srcnnRun() {
    const service = document.getElementById('src-service').value?.trim();
    const metric  = document.getElementById('src-metric').value?.trim();
    const from    = localToUtcIso(document.getElementById('src-from').value);
    const to      = localToUtcIso(document.getElementById('src-to').value);
    const sensitivity = parseFloat(document.getElementById('src-sensitivity').value);
    const btn = document.getElementById('src-run-btn');

    if (!service) { toast('Оберіть сервіс', 'error'); return; }
    if (!metric)  { toast('Оберіть метрику', 'error'); return; }

    btn.disabled = true;
    btn.innerHTML = '<span class="spinner"></span> Аналіз...';

    // Сховати попередні результати
    document.getElementById('src-stats-wrap').style.display = 'none';
    document.getElementById('src-chart-card').style.display = 'none';
    document.getElementById('src-score-card').style.display = 'none';
    document.getElementById('src-table-card').style.display = 'none';
    document.getElementById('src-empty').style.display = 'block';
    document.getElementById('src-empty').querySelector('.empty').innerHTML =
        '<span class="spinner"></span> SrCnn обробляє часовий ряд...';

    try {
        const body = {
            serviceName: service,
            metricName: metric,
            from, to,
            sensitivity
        };
        const data = await apiFetch('/api/anomalies/srcnn-batch', {
            method: 'POST',
            body: JSON.stringify(body),
        });

        srcnnLastResult = data;
        srcnnRender(data);
        toast(`SrCnn: знайдено ${data.anomalyCount} аномалій з ${data.totalPoints} точок`, 'success');

    } catch (err) {
        document.getElementById('src-empty').querySelector('.empty').innerHTML =
            `<span style="color:var(--red)">⚠ ${err.message}</span>`;
        toast(err.message, 'error');
    } finally {
        btn.disabled = false;
        btn.innerHTML = `
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="width:13px;height:13px">
              <polygon points="5 3 19 12 5 21 5 3"/>
            </svg>
            Запустити SrCnn`;
    }
}

// Рендер результату
function srcnnRender(data) {
    document.getElementById('src-empty').style.display = 'none';

    // === Статистика ===
    document.getElementById('src-stats-wrap').style.display = 'block';
    document.getElementById('src-stat-total').textContent = data.totalPoints;
    document.getElementById('src-stat-anom').textContent  = data.anomalyCount;
    const pct = data.totalPoints > 0 ? (data.anomalyCount / data.totalPoints * 100).toFixed(1) : 0;
    document.getElementById('src-stat-anom-pct').textContent = `${pct}% від ряду`;
    document.getElementById('src-stat-crit').textContent = data.criticalCount;
    document.getElementById('src-stat-warn').textContent = data.warningCount;
    document.getElementById('src-stat-info').textContent = data.infoCount;
    document.getElementById('src-stat-max').textContent  = fmtNum(data.maxScore, 3);
    document.getElementById('src-stat-avg').textContent  = fmtNum(data.averageScore, 3);
    document.getElementById('src-stat-time').textContent = `${data.processingTimeMs} мс`;

    // === Графік: часовий ряд + аномалії ===
    document.getElementById('src-chart-card').style.display = 'block';
    document.getElementById('src-chart-legend').textContent =
        `${data.serviceName} • ${data.metricName} • чутливість ${fmtNum(data.sensitivity, 2)}`;

    const labels = data.points.map(p => fmtTime(p.timestamp));
    const values = data.points.map(p => p.value);

    // Точки-аномалії на основному графіку (null там, де нема)
    const anomalyOverlay = data.points.map(p => p.isAnomaly ? p.value : null);

    if (srcnnChart) srcnnChart.destroy();
    srcnnChart = new Chart(document.getElementById('src-chart'), {
        type: 'line',
        data: {
            labels,
            datasets: [
                {
                    label: data.metricName,
                    data: values,
                    borderColor: '#a78bfa',
                    backgroundColor: 'rgba(167,139,250,.08)',
                    borderWidth: 1.5,
                    tension: 0.25,
                    pointRadius: 0,
                    pointHoverRadius: 4,
                    fill: true,
                    order: 2,
                },
                {
                    label: 'Аномалії',
                    data: anomalyOverlay,
                    borderColor: 'transparent',
                    backgroundColor: '#f87171',
                    pointBackgroundColor: '#f87171',
                    pointBorderColor: '#fff',
                    pointBorderWidth: 1.5,
                    pointRadius: 6,
                    pointHoverRadius: 9,
                    showLine: false,
                    order: 1,
                }
            ]
        },
        options: {
            responsive: true,
            animation: false,
            interaction: { mode: 'index', intersect: false },
            plugins: {
                legend: {
                    display: true,
                    position: 'top',
                    align: 'end',
                    labels: { color: '#64748b', font: { family: 'JetBrains Mono', size: 10 }, boxWidth: 12 }
                },
                tooltip: {
                    backgroundColor: '#1a2235',
                    borderColor: '#2a3f58',
                    borderWidth: 1,
                    titleFont: { family: 'JetBrains Mono', size: 11 },
                    bodyFont: { family: 'JetBrains Mono', size: 11 },
                    callbacks: {
                        afterBody: (items) => {
                            const i = items[0].dataIndex;
                            const p = data.points[i];
                            if (p?.isAnomaly) {
                                return [`Score: ${p.anomalyScore.toFixed(3)}`, `Severity: ${p.severity}`];
                            }
                            return [`Score: ${p.anomalyScore.toFixed(3)}`];
                        }
                    }
                }
            },
            scales: {
                x: { ticks: { maxTicksLimit: 14, color: '#64748b', font: { size: 10 } }, grid: { color: '#1f2d40' } },
                y: { grid: { color: '#1f2d40' }, ticks: { color: '#64748b', font: { size: 10 } } }
            }
        }
    });

    // === Графік score ===
    document.getElementById('src-score-card').style.display = 'block';
    const scores = data.points.map(p => p.anomalyScore);
    const scoreColors = data.points.map(p =>
        p.severity === 'Critical' ? '#f87171' :
        p.severity === 'Warning'  ? '#fbbf24' :
        p.isAnomaly ? '#38bdf8' : 'rgba(100,116,139,.5)'
    );

    if (srcnnScoreChart) srcnnScoreChart.destroy();
    srcnnScoreChart = new Chart(document.getElementById('src-score-chart'), {
        type: 'bar',
        data: {
            labels,
            datasets: [{
                label: 'Anomaly Score',
                data: scores,
                backgroundColor: scoreColors,
                borderWidth: 0,
                barPercentage: 1.0,
                categoryPercentage: 1.0,
            }]
        },
        options: {
            responsive: true,
            animation: false,
            plugins: {
                legend: { display: false },
                tooltip: {
                    backgroundColor: '#1a2235',
                    borderColor: '#2a3f58',
                    borderWidth: 1,
                    titleFont: { family: 'JetBrains Mono', size: 11 },
                    bodyFont: { family: 'JetBrains Mono', size: 11 },
                }
            },
            scales: {
                x: { ticks: { maxTicksLimit: 14, color: '#64748b', font: { size: 10 } }, grid: { display: false } },
                y: { min: 0, max: 1, grid: { color: '#1f2d40' }, ticks: { color: '#64748b', font: { size: 10 } } }
            }
        }
    });

    // === Таблиця аномалій ===
    const tbody = document.getElementById('src-tbody');
    if (!data.anomalies.length) {
        document.getElementById('src-table-card').style.display = 'block';
        tbody.innerHTML = `<tr><td colspan="5" class="empty">Аномалій не виявлено за цим порогом чутливості</td></tr>`;
        return;
    }

    document.getElementById('src-table-card').style.display = 'block';
    tbody.innerHTML = data.anomalies.map((a, i) => `
        <tr>
          <td class="td-muted">${i + 1}</td>
          <td>${fmtDate(a.timestamp)}</td>
          <td style="font-weight:700">${fmtNum(a.value, 2)}</td>
          <td>
            <div style="display:flex;align-items:center;gap:8px">
              <div style="flex:0 0 60px;height:6px;background:var(--surface2);border-radius:3px;overflow:hidden">
                <div style="width:${(a.anomalyScore * 100).toFixed(0)}%;height:100%;background:${
                    a.severity === 'Critical' ? '#f87171' :
                    a.severity === 'Warning'  ? '#fbbf24' : '#38bdf8'}"></div>
              </div>
              <span>${fmtNum(a.anomalyScore, 3)}</span>
            </div>
          </td>
          <td><span class="badge badge-${
              a.severity === 'Critical' ? 'crit' :
              a.severity === 'Warning'  ? 'warn' : 'info'}">${a.severity}</span></td>
        </tr>`).join('');
}

// Експорт CSV
function srcnnExportCsv() {
    if (!srcnnLastResult || !srcnnLastResult.anomalies.length) {
        toast('Немає аномалій для експорту', 'error');
        return;
    }
    const rows = [
        ['timestamp', 'service', 'metric', 'value', 'anomaly_score', 'severity'],
        ...srcnnLastResult.anomalies.map(a => [
            a.timestamp,
            srcnnLastResult.serviceName,
            srcnnLastResult.metricName,
            a.value,
            a.anomalyScore,
            a.severity
        ])
    ];
    const csv = rows.map(r => r.map(v => `"${String(v).replace(/"/g, '""')}"`).join(',')).join('\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    const stamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, 19);
    a.href = url;
    a.download = `srcnn-${srcnnLastResult.serviceName}-${srcnnLastResult.metricName}-${stamp}.csv`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
    toast('CSV завантажено', 'success');
}
