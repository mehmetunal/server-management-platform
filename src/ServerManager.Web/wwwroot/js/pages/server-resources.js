import { startAutoRefresh } from '../components/auto-refresh.js';
import { createRemotePanels } from '../components/remote-panels.js';
import { element, on, qs, qsa, setBusy } from '../core/dom.js';
import { formatClock } from '../core/format.js';
import { getHtml } from '../core/http.js';
import { initResourceHistory } from '../features/monitoring/resource-history.js';
import { initServerPage } from '../features/servers/server-page.js';

const updated = qs('[data-panel-updated]');
const autoToggle = qs('[data-auto-refresh-toggle]');
const diskForm = qs('[data-disk-form]');
const diskResult = qs('[data-disk-result]');

let containerSort = 'cpu';
let autoRefresh = null;

function sortContainers(scope) {
    const body = qs('[data-container-rows]', scope);
    if (!body) return;
    const select = qs('[data-container-sort]', scope);
    if (select) select.value = containerSort;
    const rows = qsa('tr', body);
    rows.sort((a, b) => Number(b.dataset[containerSort] || 0) - Number(a.dataset[containerSort] || 0));
    rows.forEach(row => body.appendChild(row));
}

const panels = createRemotePanels({
    onLoaded: scope => {
        sortContainers(scope);
        if (updated) updated.textContent = `Güncellendi: ${formatClock()}`;
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

async function scanDisk(path) {
    if (!diskForm || !diskResult) return;
    const input = qs('input[name="path"]', diskForm);
    if (path) input.value = path;
    const button = qs('[type="submit"]', diskForm);
    setBusy(button, true, 'Taranıyor…');
    diskResult.setAttribute('aria-busy', 'true');
    diskResult.replaceChildren(element('p', 'panel-body text-sm text-slate-500', `${input.value} taranıyor… Büyük disklerde bir dakika kadar sürebilir.`));
    const params = new URLSearchParams({ path: input.value.trim() || '/' });
    const response = await getHtml(`${diskForm.getAttribute('action')}?${params}`);
    diskResult.removeAttribute('aria-busy');
    setBusy(button, false);
    if (response.ok || response.status === 404) {
        diskResult.innerHTML = response.html;
    } else if (response.status !== 401) {
        diskResult.replaceChildren(element('p', 'panel-body text-sm text-red-600 dark:text-red-400', response.message || 'Disk taraması yapılamadı.'));
    }
}

initServerPage({ onActionSuccess: () => panels.reload() });
on(document, 'click', '[data-panel-refresh]', (event, button) => reload(button));
on(document, 'change', '[data-container-sort]', (event, select) => {
    containerSort = select.value;
    sortContainers(select.closest('[data-remote-panel]'));
});
on(document, 'click', '[data-disk-scan-path]', (event, button) => {
    qs('#disk-scan')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    scanDisk(button.dataset.diskScanPath);
});

if (autoToggle) {
    autoToggle.addEventListener('change', () => {
        autoRefresh?.stop();
        autoRefresh = autoToggle.checked
            ? startAutoRefresh(() => panels.reload(), Number(autoToggle.dataset.interval || 15) * 1000)
            : null;
    });
}

if (diskForm) {
    diskForm.addEventListener('submit', event => {
        event.preventDefault();
        scanDisk();
    });
}

initResourceHistory();
