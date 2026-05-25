// js/page-keys.js — сторінка "API Ключі"

// Завантажити список ключів
async function keysLoad() {
    const tbody = document.getElementById('keys-tbody');
    tbody.innerHTML = `<tr><td colspan="6" class="empty loading">Завантаження...</td></tr>`;
    try {
        const keys = await apiFetch('/api/keys');
        if (!keys?.length) {
            tbody.innerHTML = `<tr><td colspan="6" class="empty">Ключів ще немає. Створіть перший.</td></tr>`;
            return;
        }
        tbody.innerHTML = keys.map(k => `
      <tr>
        <td>${k.id}</td>
        <td style="color:var(--accent);font-family:var(--mono)">${k.serviceName}</td>
        <td class="td-muted">${k.owner ?? '—'}</td>
        <td style="font-family:var(--mono);font-size:11px;color:var(--muted)">${k.keyPreview ?? '***...'}</td>
        <td><span class="badge ${k.isActive ? 'badge-active' : 'badge-revoked'}">${k.isActive ? 'Активний' : 'Відкликаний'}</span></td>
        <td class="td-muted">${fmtDate(k.lastUsedAt)}</td>
        <td>
          ${k.isActive
            ? `<button class="btn btn-danger" onclick="keysRevoke(${k.id}, this)">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M18 6L6 18M6 6l12 12"/></svg>
                Відкликати
               </button>`
            : '<span class="td-muted">—</span>'}
        </td>
      </tr>`).join('');
    } catch (err) {
        tbody.innerHTML = `<tr><td colspan="6" class="empty" style="color:var(--red)">${err.message}</td></tr>`;
        toast(err.message, 'error');
    }
}

// Створити новий ключ
async function keysCreate() {
    const serviceName = document.getElementById('key-service').value?.trim();
    const owner       = document.getElementById('key-owner').value?.trim();
    const btn         = document.getElementById('key-create-btn');

    if (!serviceName) { toast('Вкажіть назву сервісу', 'error'); return; }

    btn.disabled = true;
    btn.innerHTML = '<span class="spinner"></span> Генерація...';

    try {
        const data = await apiFetch('/api/keys', {
            method: 'POST',
            body: JSON.stringify({ serviceName, owner: owner || undefined }),
        });

        // Показуємо модалку з ключем
        document.getElementById('modal-key-service').textContent = data.serviceName;
        document.getElementById('modal-key-value').textContent   = data.apiKey;
        document.getElementById('modal-key-usage').textContent   = data.usage;
        document.getElementById('key-modal').classList.add('open');

        // Очищаємо форму
        document.getElementById('key-service').value = '';
        document.getElementById('key-owner').value   = '';

        toast('Ключ успішно створено', 'success');
        keysLoad(); // оновлюємо таблицю

    } catch (err) {
        toast(err.message, 'error');
    } finally {
        btn.disabled = false;
        btn.textContent = 'Створити ключ';
    }
}

// Відкликати ключ
async function keysRevoke(id, btnEl) {
    if (!confirm(`Відкликати ключ #${id}? Дію не можна скасувати.`)) return;

    btnEl.disabled = true;
    btnEl.innerHTML = '<span class="spinner"></span>';

    try {
        const data = await apiFetch(`/api/keys/${id}`, { method: 'DELETE' });
        toast(data.message || 'Ключ відкликано', 'success');
        keysLoad();
    } catch (err) {
        toast(err.message, 'error');
        btnEl.disabled = false;
        btnEl.textContent = 'Відкликати';
    }
}

// Закрити модалку
function keysCloseModal() {
    document.getElementById('key-modal').classList.remove('open');
}

// API Key налаштування у хедері
function headerApiKeySave() {
    const val = document.getElementById('header-api-key').value?.trim();
    if (val) {
        localStorage.setItem('apiKey', val);
        toast('API ключ збережено для цієї сесії', 'success');
    } else {
        localStorage.removeItem('apiKey');
        toast('API ключ очищено', 'info');
    }
}

// Init
document.addEventListener('DOMContentLoaded', () => {
    // Підставляємо збережений ключ
    const saved = localStorage.getItem('apiKey');
    if (saved) document.getElementById('header-api-key').value = saved;
});