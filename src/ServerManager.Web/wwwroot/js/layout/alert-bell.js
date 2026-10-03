import { startAutoRefresh } from '../components/auto-refresh.js';
import { element, qs } from '../core/dom.js';
import { notify } from '../core/notify.js';

const POLL_INTERVAL_MS = 60_000;
const SEEN_KEY = 'sm-alerts-seen';
const MAX_TOASTS = 3;
const CRITICAL = 2;

function readSeen() {
    try {
        const raw = sessionStorage.getItem(SEEN_KEY);
        return raw === null ? null : new Set(JSON.parse(raw));
    } catch {
        return null;
    }
}

function writeSeen(ids) {
    try {
        sessionStorage.setItem(SEEN_KEY, JSON.stringify(ids));
    } catch {
        // Gizli sekmede depolama kapalı olabilir; yeni alarm bildirimi tekrar gösterilebilir.
    }
}

function formatTime(iso) {
    const date = new Date(iso.endsWith('Z') ? iso : `${iso}Z`);
    return date.toLocaleString('tr-TR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });
}

function renderList(list, alerts, alertsUrl) {
    list.replaceChildren();
    if (alerts.length === 0) {
        list.append(element('p', 'alert-bell-empty', 'Açık alarm yok.'));
        return;
    }
    for (const alert of alerts) {
        const item = element('a', 'alert-bell-item');
        item.href = alertsUrl;
        item.title = alert.message;
        const dot = element('span', `alert-bell-dot ${alert.severity === CRITICAL ? 'is-critical' : 'is-warning'}`);
        const body = element('span', 'min-w-0 flex-1');
        body.append(
            element('span', 'block truncate text-sm font-medium', alert.ruleName),
            element('span', 'block truncate text-xs text-slate-500', alert.targetName),
            element('span', 'block text-[11px] text-slate-400', formatTime(alert.startedAt))
        );
        item.append(dot, body);
        list.append(item);
    }
}

function announce(summary, seen) {
    if (seen === null) {
        if (summary.firingCount > 0) notify.warning(`${summary.firingCount} açık alarm var.`, 'Alarmlar');
        return;
    }
    const fresh = summary.latest.filter(alert => !seen.has(alert.id)).slice(0, MAX_TOASTS);
    for (const alert of fresh) {
        const show = alert.severity === CRITICAL ? notify.error : notify.warning;
        show(`${alert.targetName}: ${alert.message}`, alert.ruleName);
    }
}

export function initAlertBell() {
    const root = qs('[data-alert-bell]');
    if (!root) return;

    const count = qs('[data-alert-bell-count]', root);
    const summaryText = qs('[data-alert-bell-summary]', root);
    const list = qs('[data-alert-bell-list]', root);
    let poller = null;

    const poll = async () => {
        let response;
        try {
            response = await fetch(root.dataset.url, {
                credentials: 'same-origin',
                headers: { Accept: 'application/json', 'X-Requested-With': 'XMLHttpRequest', 'X-Background-Poll': '1' }
            });
        } catch {
            return;
        }
        if (response.status === 401 || response.status === 403) {
            poller?.stop();
            return;
        }
        if (!response.ok) return;

        const payload = await response.json().catch(() => null);
        const summary = payload?.isSuccess ? payload.data : null;
        if (!summary) return;

        count.hidden = summary.firingCount === 0;
        count.textContent = summary.firingCount > 99 ? '99+' : String(summary.firingCount);
        count.classList.toggle('is-critical', summary.criticalCount > 0);
        summaryText.textContent = summary.firingCount === 0
            ? 'Her şey yolunda'
            : `${summary.firingCount} açık · ${summary.criticalCount} kritik`;
        renderList(list, summary.latest, root.dataset.alertsUrl);

        const seen = readSeen();
        announce(summary, seen);
        const ids = new Set(seen ?? []);
        for (const alert of summary.latest) ids.add(alert.id);
        writeSeen([...ids].slice(-200));
    };

    poll();
    poller = startAutoRefresh(poll, POLL_INTERVAL_MS);
}
