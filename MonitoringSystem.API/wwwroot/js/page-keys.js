// js/page-keys.js — сторінка "Мій профіль": інформація про ключ + керування сервісами

// Завантажити інфо про поточний ключ + список сервісів
async function keysLoad() {
    const infoBox = document.getElementById('key-info');
    const servicesBody = document.getElementById('services-tbody');

    if (!getApiKey()) {
        infoBox.innerHTML = `<div class="empty">Введіть API ключ у полі вгорі сторінки, щоб побачити свій профіль.</div>`;
        servicesBody.innerHTML = `<tr><td colspan="3" class="empty">—</td></tr>`;
        return;
    }

    infoBox.innerHTML = `<div class="empty loading">Завантаження...</div>`;
    servicesBody.innerHTML = `<tr><td colspan="3" class="empty loading">Завантаження...</td></tr>`;

    try {
        const me = await apiFetch('/api/keys/me');
        infoBox.innerHTML = `
          <div class="stat-row"><span class="stat-label">ID ключа</span><span class="stat-value">#${me.id}</span></div>
          <div class="stat-row"><span class="stat-label">Власник</span><span class="stat-value">${me.owner}</span></div>
          <div class="stat-row"><span class="stat-label">Превʼю ключа</span><span class="stat-value" style="font-family:var(--mono)">${me.key}</span></div>
          <div class="stat-row"><span class="stat-label">Статус</span><span class="badge ${me.isActive ? 'badge-active' : 'badge-revoked'}">${me.isActive ? 'Активний' : 'Відкликаний'}</span></div>
          <div class="stat-row"><span class="stat-label">Створено</span><span class="stat-value">${fmtDate(me.createdAt)}</span></div>
          <div class="stat-row"><span class="stat-label">Останнє використання</span><span class="stat-value">${fmtDate(me.lastUsedAt)}</span></div>
          <div class="btn-row" style="margin-top:14px">
            <button class="btn btn-danger" onclick="keysRevoke(${me.id})">Відкликати ключ</button>
          </div>`;
    } catch (err) {
        infoBox.innerHTML = `<div class="empty" style="color:var(--red)">${err.message}</div>`;
    }

    try {
        const services = await apiFetch('/api/services');
        if (!services?.length) {
            servicesBody.innerHTML = `<tr><td colspan="3" class="empty">Сервісів ще немає. Зареєструйте перший.</td></tr>`;
            return;
        }
        servicesBody.innerHTML = services.map(s => `
          <tr>
            <td>${s.id}</td>
            <td style="color:var(--accent);font-family:var(--mono)">${s.name}</td>
            <td>
              <button class="btn btn-danger" onclick="serviceDelete(${s.id})">Видалити</button>
            </td>
          </tr>`).join('');
    } catch (err) {
        servicesBody.innerHTML = `<tr><td colspan="3" class="empty" style="color:var(--red)">${err.message}</td></tr>`;
    }
}

// Зареєструвати новий сервіс під поточним ключем
async function serviceCreate() {
    if (!requireApiKey()) return;

    const name = document.getElementById('svc-name').value?.trim();
    const btn = document.getElementById('svc-create-btn');

    if (!name) { toast('Вкажіть назву сервісу', 'error'); return; }

    btn.disabled = true;
    btn.innerHTML = '<span class="spinner"></span> Створення...';

    try {
        const s = await apiFetch('/api/services', {
            method: 'POST',
            body: JSON.stringify({ name }),
        });
        toast(`Сервіс "${s.name}" зареєстровано`, 'success');
        document.getElementById('svc-name').value = '';
        keysLoad();
    } catch (err) {
        toast(err.message, 'error');
    } finally {
        btn.disabled = false;
        btn.textContent = 'Зареєструвати';
    }
}

// Видалити сервіс
async function serviceDelete(id) {
    if (!confirm(`Видалити сервіс #${id}? Усі метрики та аномалії, повʼязані з ним, теж зникнуть.`)) return;
    try {
        await apiFetch(`/api/services/${id}`, { method: 'DELETE' });
        toast('Сервіс видалено', 'success');
        keysLoad();
    } catch (err) {
        toast(err.message, 'error');
    }
}

// Згенерувати новий API ключ (без авторизації)
async function keysCreate() {
    const owner = document.getElementById('new-key-owner').value?.trim();
    const btn = document.getElementById('new-key-btn');

    btn.disabled = true;
    btn.innerHTML = '<span class="spinner"></span> Генерація...';

    try {
        const data = await apiFetch('/api/keys', {
            method: 'POST',
            body: JSON.stringify({ owner: owner || undefined }),
        });

        document.getElementById('modal-key-owner').textContent = data.owner;
        document.getElementById('modal-key-value').textContent = data.apiKey;
        document.getElementById('modal-key-usage').textContent = data.usage;
        document.getElementById('key-modal').classList.add('open');

        document.getElementById('new-key-owner').value = '';
        toast('Ключ створено. Збережіть його!', 'success');
    } catch (err) {
        toast(err.message, 'error');
    } finally {
        btn.disabled = false;
        btn.textContent = 'Створити новий ключ';
    }
}

// Деактивувати поточний ключ
async function keysRevoke(id) {
    if (!confirm(`Відкликати ключ #${id}? Дашборд перестане отримувати ваші дані.`)) return;
    try {
        const data = await apiFetch(`/api/keys/${id}`, { method: 'DELETE' });
        toast(data.message || 'Ключ відкликано', 'success');
        setApiKey('');
        location.reload();
    } catch (err) {
        toast(err.message, 'error');
    }
}

function keysCloseModal() {
    document.getElementById('key-modal').classList.remove('open');
}

// Зберегти ключ з хедера
function headerApiKeySave() {
    const val = document.getElementById('header-api-key').value?.trim();
    if (val) {
        setApiKey(val);
        toast('API ключ збережено — перезавантажуємо...', 'success');
        setTimeout(() => location.reload(), 500);
    } else {
        setApiKey('');
        toast('API ключ очищено', 'info');
        setTimeout(() => location.reload(), 500);
    }
}

// Init
document.addEventListener('DOMContentLoaded', () => {
    const saved = getApiKey();
    if (saved) {
        const el = document.getElementById('header-api-key');
        if (el) el.value = saved;
    }
});
