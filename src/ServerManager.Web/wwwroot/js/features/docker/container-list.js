import { qs, qsa } from '../../core/dom.js';
import { getJson } from '../../core/http.js';
import { formatBytes, formatNumber } from '../../core/format.js';

const STATS_INTERVAL_MS = 10000;
const HIDE_STOPPED_KEY = 'sm-docker-hide-stopped';

/** Container tablosunda arama ve "yalnızca çalışanlar" filtresi; tablo yeniden yüklendikçe apply() çağrılır. */
export function createContainerFilter() {
    const input = qs('[data-table-filter]');
    const hideStopped = qs('[data-hide-stopped]');

    const apply = () => {
        const rows = qsa('[data-container-row]');
        if (!rows.length) return;
        const query = input ? input.value.trim().toLowerCase() : '';
        const onlyRunning = hideStopped?.checked ?? false;
        let visible = 0;
        rows.forEach(row => {
            const matches = (!query || (row.dataset.search ?? '').includes(query))
                && (!onlyRunning || row.dataset.state === 'running');
            row.hidden = !matches;
            if (matches) visible++;
        });
        qs('[data-filter-empty]')?.classList.toggle('hidden', visible > 0);
    };

    input?.addEventListener('input', apply);
    if (hideStopped) {
        hideStopped.checked = localStorage.getItem(HIDE_STOPPED_KEY) === '1';
        hideStopped.addEventListener('change', () => {
            localStorage.setItem(HIDE_STOPPED_KEY, hideStopped.checked ? '1' : '0');
            apply();
        });
    }

    return { apply };
}

/** Çalışan container'ların CPU/RAM değerlerini periyodik olarak tabloya yazar. */
export function createContainerStats(panel) {
    const url = panel?.dataset.statsUrl;
    if (!url) return null;
    let inFlight = false;

    const refresh = async () => {
        if (inFlight || document.hidden || !qs('[data-stat-cpu]', panel)) return;
        inFlight = true;
        try {
            const response = await getJson(url);
            if (!response.isSuccess || !response.data) return;
            const byName = new Map(response.data.map(stat => [stat.name, stat]));
            qsa('[data-stat-cpu]', panel).forEach(cell => {
                const stat = byName.get(cell.dataset.statCpu);
                cell.textContent = stat ? `${stat.cpuPercent.toFixed(1)}%` : '—';
            });
            qsa('[data-stat-memory]', panel).forEach(cell => {
                const stat = byName.get(cell.dataset.statMemory);
                cell.textContent = stat ? `${formatBytes(stat.memoryUsageBytes)} / ${formatBytes(stat.memoryLimitBytes)}` : '—';
                cell.title = stat ? `%${formatNumber(stat.memoryPercent)}` : '';
            });
        } finally {
            inFlight = false;
        }
    };

    setInterval(refresh, STATS_INTERVAL_MS);
    return { refresh };
}
