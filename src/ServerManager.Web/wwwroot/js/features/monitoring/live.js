import { qs, qsa } from '../../core/dom.js';

const BAR_CLASSES = ['bg-emerald-500', 'bg-amber-500', 'bg-red-500', 'bg-slate-300', 'dark:bg-slate-700'];

function updateResource(scope, key, percent, text) {
    const element = qs(`[data-resource="${key}"]`, scope);
    if (!element) return;
    const warning = parseFloat(element.dataset.warning);
    const critical = parseFloat(element.dataset.critical);
    const label = qs('[data-resource-text]', element);
    const bar = qs('[data-resource-bar]', element);
    if (label) label.textContent = text;
    if (!bar) return;
    bar.style.width = `${Math.max(0, Math.min(100, percent))}%`;
    bar.classList.remove(...BAR_CLASSES);
    bar.classList.add(percent >= critical ? 'bg-red-500' : percent >= warning ? 'bg-amber-500' : 'bg-emerald-500');
}

function setText(scope, selector, value) {
    const element = qs(selector, scope);
    if (element && value !== undefined && value !== null) element.textContent = value;
}

/** Sunucu durum rozeti ve kaynak çubuklarını SignalR yüküyle günceller. */
export function applyUpdate(scope, payload) {
    const badge = qs('[data-live-status]', scope);
    if (badge) {
        badge.className = payload.statusBadgeClass;
        setText(badge, '[data-live-status-text]', payload.statusText);
    }
    const latest = payload.latest;
    if (!latest) return;
    updateResource(scope, 'cpu', latest.cpu, latest.cpuText);
    updateResource(scope, 'memory', latest.memory, latest.memoryText);
    updateResource(scope, 'disk', latest.disk, latest.diskText);
    setText(scope, '[data-live-uptime]', latest.uptimeText);
    setText(scope, '[data-live-load]', latest.loadText);
    setText(scope, '[data-live-collected]', latest.collectedAtText);
}

/**
 * Monitoring hub'ına bağlanır. [data-live-server] varsa o sunucunun grubuna, yoksa filo grubuna katılır.
 * Her güncellemede satırlar ([data-live-server-row]) ve sunucu başlığı güncellenir, ardından
 * onUpdate(payload) çağrılır. Bölgeler AJAX ile değişebildiği için öğeler her seferinde yeniden aranır.
 */
export function startLiveUpdates({ onUpdate } = {}) {
    const root = qs('[data-live-server]') ?? qs('[data-live-fleet]');
    if (!root || !window.signalR) return null;

    const serverId = root.dataset.liveServer ?? null;
    const connection = new window.signalR.HubConnectionBuilder()
        .withUrl(root.dataset.hubUrl)
        .withAutomaticReconnect()
        .configureLogging(window.signalR.LogLevel.Warning)
        .build();

    const handleUpdate = payload => {
        const serverRoot = serverId ? qs(`[data-live-server="${payload.serverId}"]`) : null;
        if (serverRoot) applyUpdate(serverRoot, payload);
        qsa(`[data-live-server-row="${payload.serverId}"]`).forEach(row => applyUpdate(row, payload));
        if (!serverId || serverRoot) onUpdate?.(payload);
    };

    const join = () => (serverId ? connection.invoke('JoinServer', serverId) : connection.invoke('JoinFleet'));

    connection.on('serverUpdated', handleUpdate);
    connection.onreconnected(() => join().catch(() => { }));
    connection.start().then(join).catch(() => {
        // Canlı güncelleme olmadan sayfa çalışmaya devam eder.
    });

    return { handleUpdate };
}
