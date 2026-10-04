import { createRemotePanels } from '../components/remote-panels.js';
import { on, qs, qsa, setBusy } from '../core/dom.js';
import { formatClock } from '../core/format.js';
import { notify } from '../core/notify.js';
import { initServerPage } from '../features/servers/server-page.js';

const panel = qs('[data-remote-panel]');
const updated = qs('[data-panel-updated]');
const logForm = qs('[data-log-form]');

function filterRows(scope) {
    const search = qs('[data-table-search]', scope)?.value.trim().toLowerCase() ?? '';
    const state = qs('[data-table-state]', scope)?.value ?? '';
    let visible = 0;
    for (const row of qsa('[data-row]', scope)) {
        const match = (!search || row.dataset.search.includes(search)) && (!state || row.dataset.state === state);
        row.hidden = !match;
        if (match) visible++;
    }
    const empty = qs('[data-table-empty]', scope);
    if (empty) empty.hidden = visible > 0;
}

function syncLogFields() {
    if (!logForm) return;
    const source = qs('[data-log-source]', logForm).value;
    const isFile = source === '2';
    qsa('[data-log-journal-only]', logForm).forEach(field => { field.hidden = isFile; field.disabled = isFile; });
    qsa('[data-log-file-only]', logForm).forEach(field => { field.hidden = !isFile; field.disabled = !isFile; });
}

function fillLogFiles(scope) {
    const select = logForm && qs('[data-log-path]', logForm);
    const template = qs('[data-log-files]', scope);
    if (!select || !template) return;
    const selected = select.value || select.dataset.selected || '';
    select.replaceChildren(select.options[0]);
    select.appendChild(template.content.cloneNode(true));
    if (selected) select.value = selected;
    const logPanel = qs('[data-log-panel]', scope);
    if (logPanel && logPanel.dataset.hasJournal === 'false') {
        const journal = qs('[data-log-source] option[value="1"]', logForm);
        if (journal) journal.disabled = true;
        qs('[data-log-source]', logForm).value = '2';
        syncLogFields();
    }
}

const panels = createRemotePanels({
    onLoaded: scope => {
        filterRows(scope);
        fillLogFiles(scope);
        const output = qs('[data-log-output]', scope);
        if (output) output.scrollTop = output.scrollHeight;
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

initServerPage({ onActionSuccess: () => panels.reload() });
on(document, 'click', '[data-panel-refresh]', (event, button) => reload(button));
on(document, 'input', '[data-table-search]', (event, input) => filterRows(input.closest('[data-remote-panel]')));
on(document, 'change', '[data-table-state]', (event, select) => filterRows(select.closest('[data-remote-panel]')));

on(document, 'click', '[data-log-copy]', async () => {
    const output = qs('[data-log-output]');
    if (!output) return;
    try {
        await navigator.clipboard.writeText(output.textContent);
        notify.success('Log satırları panoya kopyalandı.');
    } catch {
        notify.error('Kopyalanamadı; satırları elle seçip kopyalayın.');
    }
});

if (logForm) {
    syncLogFields();
    qs('[data-log-source]', logForm).addEventListener('change', syncLogFields);
    logForm.addEventListener('submit', event => {
        event.preventDefault();
        const params = new URLSearchParams();
        for (const [key, value] of new FormData(logForm)) {
            if (typeof value === 'string' && value.trim() !== '') params.append(key, value.trim());
        }
        const query = params.toString();
        panel.dataset.url = `${logForm.getAttribute('action')}?${query}`;
        window.history.replaceState(null, '', `${logForm.dataset.pageUrl}?${query}`);
        reload(logForm.querySelector('[type="submit"]'));
    });
}
