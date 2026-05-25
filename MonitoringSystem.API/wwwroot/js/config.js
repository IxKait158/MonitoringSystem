// js/config.js — загальна конфігурація та утиліти

const API_URL = "http://localhost:5169/";

// HTTP помічник
async function apiFetch(path, options = {}) {
    const apiKey = localStorage.getItem('apiKey') || '';
    const res = await fetch(`${API_URL}${path}`, {
        headers: {
            'Content-Type': 'application/json',
            ...(apiKey ? { 'X-API-KEY': apiKey } : {}),
        },
        ...options,
    });

    const text = await res.text();
    let data;
    try { data = text ? JSON.parse(text) : null; }
    catch { data = { message: text }; }

    if (!res.ok) throw new Error(data?.message || `HTTP ${res.status}`);
    return data;
}

// Спливаючі сповіщення
function toast(msg, type = 'info') {
    const container = document.getElementById('toast');
    const el = document.createElement('div');
    el.className = `toast-msg toast-${type}`;
    el.textContent = msg;
    container.appendChild(el);
    setTimeout(() => el.remove(), 4000);
}

// Помічники форматування
function fmtTime(iso) {
    if (!iso) return '—';
    return new Date(iso).toLocaleTimeString('uk', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
}

function fmtDate(iso) {
    if (!iso) return '—';
    return new Date(iso).toLocaleString('uk', {
        day: '2-digit', month: '2-digit', year: 'numeric',
        hour: '2-digit', minute: '2-digit'
    });
}

function fmtNum(n, decimals = 1) {
    if (n == null) return '—';
    return Number(n).toFixed(decimals);
}

// Місцева дата та час у форматі UTC ISO
function localToUtcIso(localStr) {
    if (!localStr) return null;
    return new Date(localStr).toISOString();
}

// Навігація сторінками
function showPage(pageId) {
    document.querySelectorAll('.page').forEach(p => p.classList.remove('active'));
    document.querySelectorAll('.nav-btn').forEach(b => b.classList.remove('active'));
    const page = document.getElementById(`page-${pageId}`);
    if (page) page.classList.add('active');
    const btn = document.querySelector(`.nav-btn[data-page="${pageId}"]`);
    if (btn) btn.classList.add('active');
    window._currentPage = pageId;
}

// Копіювати в буфер обміну
function copyText(text) {
    navigator.clipboard.writeText(text)
        .then(() => toast('Скопійовано', 'success'))
        .catch(() => toast('Не вдалося скопіювати', 'error'));
}

// Клас назви метрики
function metricClass(name, value) {
    if (name === 'http.response_time_ms') {
        return value > 1000 ? 'crit' : value > 300 ? 'warn' : 'ok';
    }
    if (name === 'system.memory_mb') {
        return value > 500 ? 'crit' : value > 300 ? 'warn' : 'ok';
    }
    if (name === 'system.cpu_percent') {
        return value > 80 ? 'crit' : value > 50 ? 'warn' : 'ok';
    }
    return 'ok';
}