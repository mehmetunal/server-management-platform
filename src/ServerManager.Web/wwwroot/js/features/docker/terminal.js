import { qs } from '../../core/dom.js';
import { onPageDispose } from '../../core/page-scope.js';
import { createTerminalHub } from '../terminal/terminal-hub.js';
import { createTerminalSession } from '../terminal/terminal-session.js';
import { isBusyState, renderStatus } from '../terminal/terminal-status.js';
import { createTerminalView } from '../terminal/terminal-view.js';

/**
 * Container terminali. Oturum kimliği sessionStorage'da tutulur; sayfa yenilenince aynı oturuma yeniden bağlanılır.
 */
export function createTerminal(root) {
    if (!root || !window.Terminal || !window.signalR) return null;

    const statusBadge = qs('[data-terminal-status]', root);
    const connectButton = qs('[data-terminal-connect]', root);
    const disconnectButton = qs('[data-terminal-disconnect]', root);
    const takeoverButton = qs('[data-terminal-takeover]', root);
    // data-service-id varsa servis konsolu açılır: komut sunucuda şablondan seçilir, istemci yalnızca servis kimliğini verir.
    const serviceId = root.dataset.serviceId;
    const storageKey = serviceId
        ? `terminal:service:${serviceId}`
        : `terminal:container:${root.dataset.serverId}:${root.dataset.container}`;

    const hub = createTerminalHub(root.dataset.hubUrl);
    let session = null;
    const view = createTerminalView(qs('[data-terminal-screen]', root), {
        onData: data => session?.input(data),
        onResize: (columns, rows) => session?.resize(columns, rows)
    });

    session = createTerminalSession({
        hub,
        view,
        sessionId: sessionStorage.getItem(storageKey),
        start: (columns, rows) => (serviceId
            ? hub.invoke('StartServiceConsole', serviceId, columns, rows)
            : hub.invoke('StartContainer', root.dataset.serverId, root.dataset.container, columns, rows)),
        onSession: id => (id ? sessionStorage.setItem(storageKey, id) : sessionStorage.removeItem(storageKey)),
        onStatus: (state, text) => {
            renderStatus(statusBadge, state, text);
            connectButton.hidden = state === 'connected' || state === 'detached' || isBusyState(state);
            disconnectButton.hidden = state !== 'connected';
            if (takeoverButton) takeoverButton.hidden = state !== 'detached';
        }
    });

    let opened = false;
    connectButton.addEventListener('click', () => session.reconnect());
    disconnectButton.addEventListener('click', () => session.stop());
    takeoverButton?.addEventListener('click', () => session.reconnect());
    onPageDispose(() => {
        session.dispose();
        view.dispose();
        hub.stop().catch(() => { });
    });

    return {
        activate() {
            setTimeout(() => {
                view.fit();
                view.focus();
                if (!opened) {
                    opened = true;
                    session.open();
                }
            }, 0);
        }
    };
}
