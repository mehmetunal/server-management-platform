import { createAjaxList } from '../components/ajax-list.js';
import { bindModals, openModal } from '../components/modal.js';
import { on, qs, setBusy } from '../core/dom.js';
import { getHtml } from '../core/http.js';
import { notify } from '../core/notify.js';
import { initServerPage } from '../features/servers/server-page.js';
import { createTerminalWorkspace } from '../features/terminal/terminal-workspace.js';

initServerPage({ regions: ['server-header'] });
bindModals();
createTerminalWorkspace(qs('[data-terminal-workspace]'));

const sessionsRoot = qs('[data-terminal-sessions]');
const sessions = createAjaxList(sessionsRoot, { history: false, url: sessionsRoot.dataset.url });
sessions.reload();

on(document, 'click', '[data-sessions-refresh]', async (event, button) => {
    setBusy(button, true);
    try {
        await sessions.reload();
    } finally {
        setBusy(button, false);
    }
});

const sessionModal = qs('#terminal-session-modal');
const sessionBody = qs('[data-session-body]', sessionModal);

on(sessionsRoot, 'click', '[data-session-open]', async (event, button) => {
    setBusy(button, true);
    const response = await getHtml(button.dataset.url);
    setBusy(button, false);
    if (!response.ok) {
        notify.error(response.message || 'Oturum komutları alınamadı.');
        return;
    }
    sessionBody.innerHTML = response.html;
    openModal(sessionModal);
});
