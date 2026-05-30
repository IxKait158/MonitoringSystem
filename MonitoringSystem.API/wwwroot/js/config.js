// js/config.js — загальна конфігурація та утиліти

const API_URL = "http://localhost:5169";

// Поточний API ключ — зберігається у localStorage
function getApiKey() {
    return localStorage.getItem('apiKey') || '';
}

function setApiKey(value) {
    if (value) localStorage.setItem('apiKey', value);
    else localStorage.removeItem('apiKey');
}

// HTTP помічник
async function apiFetch(path, options = {}) {
    const apiKey = getApiKey();
    const res = await fetch(`${API_URL}${path}`, {
        headers: {
            'Content-Type': 'application/json',
            ...(apiKey ? {'X-API-KEY': apiKey} : {}),
        },
        ...options,
    });

    const text = await res.text();
    let data;
    try {
        data = text ? JSON.parse(text) : null;
    } catch {
        data = {message: text};
    }

    // Невалідний/відсутній ключ — викидаємо у "вихідний" стан.
    if (res.status === 401 || res.status === 403) {
        handleUnauthorized(data?.error || data?.message || 'Сесія завершена');
        throw new Error(data?.error || data?.message || `HTTP ${res.status}`);
    }

    if (!res.ok) throw new Error(data?.message || `HTTP ${res.status}`);
    return data;
}

// Реакція на 401/403 — очищаємо ключ, блокуємо навігацію, кидаємо на дашборд
let _unauthorizedHandled = false;

function handleUnauthorized(message) {
    if (_unauthorizedHandled) return;
    _unauthorizedHandled = true;

    setApiKey('');
    toast(`${message}. Будь ласка, увійдіть знову.`, 'error');

    const input = document.getElementById('header-api-key');
    if (input) input.value = '';
    const logoutBtn = document.getElementById('header-logout-btn');
    if (logoutBtn) logoutBtn.style.display = 'none';

    if (typeof refreshNavLockState === 'function') refreshNavLockState();

    const current = window._currentPage;
    if (current && !isPagePublic(current)) {
        showPage('dashboard');
    }

    // Дозволимо нові спроби після короткої паузи
    setTimeout(() => {
        _unauthorizedHandled = false;
    }, 2000);
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
    return new Date(iso).toLocaleTimeString('uk', {hour: '2-digit', minute: '2-digit', second: '2-digit'});
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

// Локальний час у форматі для <input type="datetime-local"> — "YYYY-MM-DDTHH:MM"
function toLocalInputValue(date) {
    const pad = n => String(n).padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

// Сторінки, доступні без API ключа
const PUBLIC_PAGES = ['dashboard', 'keys'];

function isPagePublic(pageId) {
    return PUBLIC_PAGES.includes(pageId);
}

// Навігація сторінками
function showPage(pageId) {
    // Якщо ключа немає — пропускаємо лише дашборд та реєстрацію ключа
    if (!getApiKey() && !isPagePublic(pageId)) {
        toast('Спочатку увійдіть за API ключем', 'error');
        return;
    }

    document.querySelectorAll('.page').forEach(p => p.classList.remove('active'));
    document.querySelectorAll('.nav-btn').forEach(b => b.classList.remove('active'));
    const page = document.getElementById(`page-${pageId}`);
    if (page) page.classList.add('active');
    const btn = document.querySelector(`.nav-btn[data-page="${pageId}"]`);
    if (btn) btn.classList.add('active');
    window._currentPage = pageId;
}

// Оновити стан навігації відносно наявності API ключа.
// Без ключа кнопки закритих сторінок повністю ховаємо.
function refreshNavLockState() {
    const hasKey = !!getApiKey();
    document.querySelectorAll('.nav-btn[data-page]').forEach(btn => {
        const page = btn.dataset.page;
        const hidden = !hasKey && !isPagePublic(page);
        btn.style.display = hidden ? 'none' : '';
        btn.disabled = hidden;
        btn.setAttribute('aria-hidden', hidden ? 'true' : 'false');
    });
}

// Перехоплюємо кліки по nav у фазі capture — навіть якщо у кнопки лишився inline onclick,
// ми зупиняємо подію до її виклику, поки користувач не авторизований.
function installNavGuard() {
    const nav = document.querySelector('header nav');
    if (!nav) return;

    nav.addEventListener('click', (e) => {
        const btn = e.target.closest('.nav-btn[data-page]');
        if (!btn) return;

        const page = btn.dataset.page;
        if (!getApiKey() && !isPagePublic(page)) {
            e.preventDefault();
            e.stopImmediatePropagation();
            toast('Спочатку увійдіть за API ключем', 'error');
        }
    }, true); // <-- capture: блокуємо ДО спрацювання inline onclick

    // Те саме для keyboard (Enter/Space на кнопці)
    nav.addEventListener('keydown', (e) => {
        if (e.key !== 'Enter' && e.key !== ' ') return;
        const btn = e.target.closest?.('.nav-btn[data-page]');
        if (!btn) return;

        const page = btn.dataset.page;
        if (!getApiKey() && !isPagePublic(page)) {
            e.preventDefault();
            e.stopImmediatePropagation();
            toast('Спочатку увійдіть за API ключем', 'error');
        }
    }, true);
}

// Автоініціалізація — щоб блокування з'являлось одразу, незалежно від інших скриптів
function _initNavLock() {
    refreshNavLockState();
    installNavGuard();
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', _initNavLock);
} else {
    _initNavLock();
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

// Перевірити, що ключ існує — інакше пропонуємо ввести
function requireApiKey() {
    if (!getApiKey()) {
        toast('Введіть API ключ у верхньому полі та натисніть зберегти', 'error');
        return false;
    }
    return true;
}
