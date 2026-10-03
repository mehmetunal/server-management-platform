const FLASH_KEY = 'sm-flash';

const DEFAULT_OPTIONS = {
    closeButton: true,
    progressBar: true,
    newestOnTop: true,
    preventDuplicates: true,
    positionClass: 'toast-top-right',
    escapeHtml: true,
    timeOut: 5000,
    extendedTimeOut: 2000
};

let configured = false;

function client() {
    const toastr = window.toastr;
    if (toastr && !configured) {
        toastr.options = { ...DEFAULT_OPTIONS };
        configured = true;
    }
    return toastr;
}

function show(type, message, title, overrides) {
    const toastr = client();
    if (!toastr || !message) return;
    toastr[type](message, title, overrides);
}

/** toastr sarmalayıcısı; uygulama kodu toastr'a doğrudan erişmez. */
export const notify = {
    success: (message, title) => show('success', message, title),
    info: (message, title) => show('info', message, title),
    warning: (message, title) => show('warning', message, title, { timeOut: 7000 }),
    error: (message, title) => show('error', message, title, { timeOut: 8000 })
};

/** Bir sonraki sayfa açıldığında gösterilecek bildirimi saklar. */
export function flash(type, message) {
    if (!message) return;
    try {
        sessionStorage.setItem(FLASH_KEY, JSON.stringify({ type, message }));
    } catch {
        // Gizli sekmede depolama kapalı olabilir; bildirim atlanır.
    }
}

export function showPendingFlash() {
    let pending = null;
    try {
        pending = JSON.parse(sessionStorage.getItem(FLASH_KEY) ?? 'null');
        sessionStorage.removeItem(FLASH_KEY);
    } catch {
        return;
    }
    if (pending && notify[pending.type]) notify[pending.type](pending.message);
}
