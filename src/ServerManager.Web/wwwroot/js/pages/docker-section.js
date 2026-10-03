import { bindModals, closeModal } from '../components/modal.js';
import { createRemotePanels } from '../components/remote-panels.js';
import { on, qs, qsa, setBusy } from '../core/dom.js';
import { formatClock } from '../core/format.js';
import { bindAjaxForm } from '../core/forms.js';
import { notify } from '../core/notify.js';
import { createContainerFilter, createContainerStats } from '../features/docker/container-list.js';
import { initServerPage } from '../features/servers/server-page.js';

const filter = createContainerFilter();
const updated = qs('[data-panel-updated]');

const panels = createRemotePanels({
    onLoaded: () => {
        filter.apply();
        stats?.refresh();
        if (updated) updated.textContent = `Güncellendi: ${formatClock()}`;
    }
});
const stats = createContainerStats(qs('[data-remote-panel]'));

async function reloadPanels(button) {
    setBusy(button, true);
    try {
        await panels.reload();
    } finally {
        setBusy(button, false);
    }
}

initServerPage({ onActionSuccess: () => panels.reload() });
bindModals();
on(document, 'click', '[data-panel-refresh]', (event, button) => reloadPanels(button));

qsa('[data-ajax-form]').forEach(form => bindAjaxForm(form, {
    onSuccess: async response => {
        closeModal(form.closest('[data-modal]'));
        form.reset();
        notify.success(response.message || 'İşlem tamamlandı.');
        await panels.reload();
    }
}));
