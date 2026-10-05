import { bindAjaxActions } from '../components/ajax-actions.js';
import { on, qs } from '../core/dom.js';
import { loadProbe } from '../features/managed-services/probe.js';
import { initServerPage } from '../features/servers/server-page.js';

// Sunucu sekmesinde başlık (_ServerHeader) vardır; genel listede yalnızca hızlı işlemler bağlanır.
if (qs('[data-region="server-header"]')) initServerPage();
else bindAjaxActions(document);

on(document, 'change', '[data-auto-submit]', (event, select) => select.form?.submit());

const probe = qs('[data-service-probe]');
if (probe) {
    loadProbe(probe, {
        url: probe.dataset.url,
        dockerUrl: probe.dataset.dockerUrl,
        dokployUrl: probe.dataset.dokployUrl,
        dokkuUrl: probe.dataset.dokkuUrl
    });
}
