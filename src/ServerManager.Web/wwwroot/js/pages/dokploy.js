import { createRemotePanels } from '../components/remote-panels.js';
import { element, on, qs, setBusy } from '../core/dom.js';
import { formatClock } from '../core/format.js';
import { bindAjaxForm } from '../core/forms.js';
import { failureMessage, getHtml, postForm } from '../core/http.js';
import { notify } from '../core/notify.js';
import { initServerPage } from '../features/servers/server-page.js';

const updated = qs('[data-panel-updated]');

async function loadProjects(button) {
    const target = qs('[data-projects-panel]');
    if (!target) return;
    setBusy(button, true);
    try {
        const response = await getHtml(target.dataset.url);
        if (response.ok || response.status === 404) {
            target.innerHTML = response.html;
        } else if (response.status !== 401) {
            target.replaceChildren(element('p', 'panel-body text-sm text-red-600 dark:text-red-400', response.message || 'Projeler alınamadı.'));
        }
    } finally {
        setBusy(button, false);
    }
}

const panels = createRemotePanels({
    onLoaded: () => {
        if (updated) updated.textContent = `Güncellendi: ${formatClock()}`;
        bindAjaxForm(qs('[data-dokploy-settings]'), {
            onSuccess: async response => {
                notify.success(response.message || 'Ayarlar kaydedildi.');
                await panels.reload();
            }
        });
        loadProjects();
    }
});

async function reloadPanels(button) {
    setBusy(button, true);
    try {
        await panels.reload();
    } finally {
        setBusy(button, false);
    }
}

async function runHealthCheck(button) {
    setBusy(button, true, 'Kontrol ediliyor…');
    try {
        const response = await postForm(button.dataset.url);
        if (!response.isSuccess) {
            notify.error(failureMessage(response, 'Sağlık kontrolü yapılamadı.'));
            return;
        }
        if (response.data?.isHealthy) notify.success(response.message || 'Dokploy çalışıyor.');
        else notify.warning(response.message || 'Dokploy sorunlu görünüyor.');
        await panels.reload();
    } finally {
        setBusy(button, false);
    }
}

initServerPage({ onActionSuccess: () => panels.reload() });
on(document, 'click', '[data-panel-refresh]', (event, button) => reloadPanels(button));
on(document, 'click', '[data-health-check]', (event, button) => runHealthCheck(button));
on(document, 'click', '[data-projects-refresh]', (event, button) => loadProjects(button));
