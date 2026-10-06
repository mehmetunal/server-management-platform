import { createRemotePanels } from '../components/remote-panels.js';
import { confirmAction } from '../core/dialog.js';
import { element, on, qs, qsa, setBusy } from '../core/dom.js';
import { formatBytes, formatClock } from '../core/format.js';
import { notify } from '../core/notify.js';
import { initServerPage } from '../features/servers/server-page.js';

const panel = qs('[data-remote-panel]');
const updated = qs('[data-panel-updated]');
const optionsForm = qs('[data-cleanup-options]');
const runPanel = qs('[data-cleanup-run]');
const runLog = qs('[data-cleanup-log]');
const runTitle = qs('[data-cleanup-run-title]');
const runSummary = qs('[data-cleanup-run-summary]');

const LEVEL_CLASS = {
    info: 'cleanup-log-info',
    success: 'cleanup-log-success',
    warning: 'cleanup-log-warning',
    error: 'cleanup-log-error'
};

let running = false;

function selectedItems() {
    return qsa('[data-cleanup-item]', panel).filter(input => input.checked);
}

function syncGroupToggle(group) {
    const toggle = qs('[data-cleanup-group-toggle]', group);
    if (!toggle) return;
    const items = qsa('[data-cleanup-item]', group);
    const checked = items.filter(input => input.checked).length;
    toggle.checked = items.length > 0 && checked === items.length;
    toggle.indeterminate = checked > 0 && checked < items.length;
}

function updateSelection() {
    const items = selectedItems();
    const size = items.reduce((sum, input) => sum + Number(input.dataset.size || 0), 0);
    const caution = items.filter(input => input.dataset.caution === 'true').length;
    const sizeLabel = qs('[data-cleanup-selected-size]', panel);
    const countLabel = qs('[data-cleanup-selected-count]', panel);
    const text = qs('[data-cleanup-selected-text]', panel);
    if (sizeLabel) sizeLabel.textContent = items.length ? formatBytes(size) : '—';
    if (countLabel) countLabel.textContent = items.length ? `${items.length} öğe${caution ? ` · ${caution} Dikkat` : ''}` : 'Seçim yok';
    if (text) text.textContent = items.length ? `${items.length} öğe seçili · tahmini ${formatBytes(size)}` : 'Seçim yok';
    qsa('[data-cleanup-run-button]', panel).forEach(button => { button.disabled = running || items.length === 0; });
    qsa('[data-cleanup-group]', panel).forEach(syncGroupToggle);
}

const panels = createRemotePanels({
    onLoaded: () => {
        updateSelection();
        if (updated) updated.textContent = `Tarandı: ${formatClock()}`;
    }
});

async function reload(button) {
    if (button) setBusy(button, true);
    try {
        await panels.reload();
    } finally {
        if (button) setBusy(button, false);
    }
}

function optionParams() {
    const params = new URLSearchParams();
    if (!optionsForm) return params;
    for (const [key, value] of new FormData(optionsForm)) {
        if (typeof value === 'string' && value.trim() !== '') params.append(key, value.trim());
    }
    return params;
}

function appendLog(level, message, output) {
    const item = element('li', `cleanup-log-line ${LEVEL_CLASS[level] ?? LEVEL_CLASS.info}`);
    item.appendChild(element('span', 'cleanup-log-time', formatClock()));
    item.appendChild(element('span', 'cleanup-log-message', message));
    if (output) item.appendChild(element('pre', 'cleanup-log-output', output));
    runLog.appendChild(item);
    item.scrollIntoView({ block: 'nearest' });
}

function antiforgeryToken() {
    return qs('input[name="__RequestVerificationToken"]')?.value ?? '';
}

async function readFailure(response) {
    if ((response.headers.get('content-type') ?? '').includes('application/json')) {
        try {
            const body = await response.json();
            return body.message || 'Temizlik çalıştırılamadı.';
        } catch {
            // JSON okunamazsa genel mesaj gösterilir.
        }
    }
    if (response.status === 401) {
        window.location.reload();
        return 'Oturum süresi doldu.';
    }
    if (response.status === 403) return 'Bu işlem için yetkiniz yok.';
    if (response.status === 429) return 'Çok fazla istek gönderildi. Lütfen biraz bekleyin.';
    return `Temizlik çalıştırılamadı (${response.status}).`;
}

/** Yanıt NDJSON akışıdır: her satır bir günlük kaydı, son satır özet. */
async function streamRun(url, params) {
    const response = await fetch(url, {
        method: 'POST',
        credentials: 'same-origin',
        headers: {
            Accept: 'application/x-ndjson, application/json',
            'X-Requested-With': 'XMLHttpRequest',
            'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
            RequestVerificationToken: antiforgeryToken()
        },
        body: params
    });

    if (!response.ok || !(response.headers.get('content-type') ?? '').includes('ndjson')) {
        return { isSuccess: false, message: await readFailure(response) };
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';
    let done = null;
    const handle = line => {
        if (!line.trim()) return;
        const entry = JSON.parse(line);
        if (entry.type === 'log') appendLog(entry.level, entry.message, entry.output);
        else if (entry.type === 'done') done = entry;
    };

    for (;;) {
        const { value, done: finished } = await reader.read();
        if (finished) break;
        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split('\n');
        buffer = lines.pop();
        lines.forEach(handle);
    }
    handle(buffer + decoder.decode());
    return done ?? { isSuccess: false, message: 'Bağlantı kesildi; işlem sunucuda sürüyor olabilir. Sonucu Activity sekmesinden kontrol edin.' };
}

async function run(button) {
    const items = selectedItems();
    if (items.length === 0 || running) return;
    const dryRun = button.dataset.dryRun === 'true';
    const size = items.reduce((sum, input) => sum + Number(input.dataset.size || 0), 0);
    const caution = items.filter(input => input.dataset.caution === 'true');

    if (!dryRun) {
        const names = items.slice(0, 12).map(input => `${input.dataset.caution === 'true' ? '⚠ ' : ''}${input.dataset.name}`);
        if (items.length > names.length) names.push(`… ve ${items.length - names.length} öğe daha`);
        const confirmed = await confirmAction({
            title: 'Seçilenler silinsin mi?',
            message: `${items.length} öğe sunucudan silinecek (tahmini ${formatBytes(size)}). Bu işlem geri alınamaz.` +
                (caution.length ? ` ${caution.length} öğe "Dikkat" işaretli; gerekçelerini okuduğunuzdan emin olun.` : ''),
            code: names.join('\n'),
            confirmText: 'Temizle',
            expected: caution.length ? 'temizle' : undefined
        });
        if (!confirmed) return;
    }

    const params = optionParams();
    items.forEach(input => params.append('keys', input.value));
    params.append('dryRun', dryRun ? 'true' : 'false');

    running = true;
    updateSelection();
    setBusy(button, true, dryRun ? 'Önizleniyor…' : 'Temizleniyor…');
    runPanel.hidden = false;
    runLog.replaceChildren();
    runTitle.textContent = dryRun ? 'Önizleme günlüğü' : 'Temizlik günlüğü';
    runSummary.textContent = 'Çalışıyor…';
    runPanel.scrollIntoView({ behavior: 'smooth', block: 'start' });

    let result;
    try {
        result = await streamRun(panel.dataset.executeUrl, params);
    } catch {
        result = { isSuccess: false, message: 'Ağ hatası: sunucuya ulaşılamadı.' };
    } finally {
        running = false;
        setBusy(button, false);
        updateSelection();
    }

    runSummary.textContent = result.message || '';
    if (result.isSuccess) {
        appendLog(result.data?.failed ? 'warning' : 'success', result.message);
        if (result.data?.failed) notify.warning(result.message);
        else notify.success(result.message);
        if (!dryRun) await reload();
    } else {
        appendLog('error', result.message);
        notify.error(result.message);
    }
}

initServerPage();
on(document, 'click', '[data-panel-refresh]', (event, button) => reload(button));
on(document, 'change', '[data-cleanup-item]', () => updateSelection());
on(document, 'change', '[data-cleanup-group-toggle]', (event, toggle) => {
    const group = toggle.closest('[data-cleanup-group]');
    qsa('[data-cleanup-item]', group).forEach(input => { input.checked = toggle.checked; });
    updateSelection();
});
on(document, 'click', '[data-cleanup-run-button]', (event, button) => run(button));
on(document, 'click', '[data-cleanup-run-close]', () => { runPanel.hidden = true; });

if (optionsForm) {
    optionsForm.addEventListener('submit', event => {
        event.preventDefault();
        const query = optionParams().toString();
        panel.dataset.url = `${optionsForm.getAttribute('action')}?${query}`;
        window.history.replaceState(null, '', `${optionsForm.dataset.pageUrl}?${query}`);
        reload(optionsForm.querySelector('[type="submit"]'));
    });
}
