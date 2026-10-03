const STATUS_CLASSES = {
    connected: 'badge-success',
    connecting: 'badge-info',
    reconnecting: 'badge-info',
    detached: 'badge-warning',
    disconnected: 'badge-danger',
    error: 'badge-danger'
};

export const isBusyState = state => state === 'connecting' || state === 'reconnecting';

/** Durum rozeti: <span data-terminal-status><span class="badge-dot"></span><span data-terminal-status-text></span></span> */
export function renderStatus(badge, state, text) {
    if (!badge) return;
    badge.className = STATUS_CLASSES[state] ?? 'badge-neutral';
    const label = badge.querySelector('[data-terminal-status-text]');
    if (label) label.textContent = text || 'Bağlı değil';
}
