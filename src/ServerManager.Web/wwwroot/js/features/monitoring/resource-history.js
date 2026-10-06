import { element, qs, qsa } from '../../core/dom.js';
import { formatBytes, formatNumber } from '../../core/format.js';
import { getJson } from '../../core/http.js';
import { onPageDispose } from '../../core/page-scope.js';

// Metrik grafikleriyle aynı palet (charts.js).
const PALETTE = ['#6366f1', '#10b981', '#f59e0b', '#0ea5e9', '#ec4899', '#8b5cf6'];

function themeColors() {
    return document.documentElement.classList.contains('dark')
        ? { text: '#94a3b8', grid: 'rgba(148, 163, 184, 0.12)' }
        : { text: '#64748b', grid: 'rgba(100, 116, 139, 0.12)' };
}

const formatPercent = value => `%${formatNumber(value ?? 0)}`;

function datasets(series) {
    return (series ?? []).map((item, index) => {
        const color = PALETTE[index % PALETTE.length];
        const count = item.data?.length ?? 0;
        return {
            label: item.name,
            data: item.data,
            borderColor: color,
            backgroundColor: `${color}22`,
            borderWidth: 2,
            pointRadius: count > 0 && count <= 30 ? 2.5 : 0,
            pointHoverRadius: 4,
            tension: 0.3,
            spanGaps: false,
            fill: false
        };
    });
}

function chartOptions(key, onPick) {
    const colors = themeColors();
    const format = key === 'memory' ? formatBytes : formatPercent;
    return {
        responsive: true,
        maintainAspectRatio: false,
        animation: false,
        interaction: { mode: 'index', intersect: false },
        onClick: (event, elements, chart) => {
            const points = chart.getElementsAtEventForMode(event, 'index', { intersect: false }, false);
            if (points.length) onPick(points[0].index);
        },
        plugins: {
            legend: { display: true, labels: { color: colors.text, boxWidth: 12, usePointStyle: true } },
            tooltip: {
                callbacks: {
                    label: context => `${context.dataset.label}: ${format(context.parsed.y)}`,
                    footer: () => 'Tıklayın: o andaki process listesi'
                }
            }
        },
        scales: {
            x: { ticks: { color: colors.text, maxTicksLimit: 8, maxRotation: 0, autoSkip: true }, grid: { display: false } },
            y: {
                beginAtZero: true,
                ticks: { color: colors.text, maxTicksLimit: 6, callback: value => format(value) },
                grid: { color: colors.grid }
            }
        }
    };
}

function cell(text, className = '') {
    return element('td', className, text);
}

function emptyRow(body, columns, text) {
    const row = element('tr');
    const td = cell(text, 'text-sm text-slate-500');
    td.colSpan = columns;
    row.appendChild(td);
    body.replaceChildren(row);
}

function commandCell(name, detail) {
    const td = element('td', 'resource-command');
    td.title = detail || name;
    td.appendChild(element('span', 'font-medium', name));
    if (detail && detail !== name) {
        td.append(' ');
        td.appendChild(element('span', 'text-slate-500', detail));
    }
    return td;
}

const num = 'text-right tabular-nums';
const numMd = 'hidden text-right tabular-nums md:table-cell';

function renderContainers(body, rows) {
    if (!rows.length) {
        emptyRow(body, 8, 'Bu aralıkta container örneği yok.');
        return;
    }
    body.replaceChildren(...rows.map(c => {
        const tr = element('tr');
        tr.append(
            cell(c.name, 'font-mono text-xs'),
            cell(formatPercent(c.cpuAvg), num),
            cell(formatPercent(c.cpuMax), num),
            cell(formatBytes(c.memoryAvgBytes), num),
            cell(formatBytes(c.memoryMaxBytes), num),
            cell(c.networkBytes === null ? '—' : formatBytes(c.networkBytes), numMd),
            cell(c.blockBytes === null ? '—' : formatBytes(c.blockBytes), numMd),
            cell(String(c.restarts), c.restarts > 0 ? `${num} font-semibold text-red-600 dark:text-red-400` : num));
        return tr;
    }));
}

function renderProcesses(body, rows, byMemory) {
    if (!rows.length) {
        emptyRow(body, 4, 'Bu aralıkta process anlık görüntüsü yok.');
        return;
    }
    body.replaceChildren(...rows.map(p => {
        const tr = element('tr');
        tr.append(
            commandCell(p.name, p.user ? `(${p.user})` : ''),
            byMemory ? cell(formatBytes(p.residentMaxKilobytes * 1024), num) : cell(formatPercent(p.cpuAvg), num),
            byMemory ? cell(formatPercent(p.cpuAvg), num) : cell(formatPercent(p.cpuMax), num),
            cell(String(p.seen), num));
        return tr;
    }));
}

/** Kaynak Kullanımı → Geçmiş bölümü: aralık seçimi, container grafikleri, özet tablolar ve tıklanan anın process listesi. */
export function initResourceHistory(root = qs('[data-history]')) {
    if (!root || !window.Chart) return;

    const historyUrl = root.dataset.historyUrl;
    const snapshotUrl = root.dataset.snapshotUrl;
    const description = qs('[data-history-description]', root);
    const customForm = qs('[data-history-custom]', root);
    const snapshotPanel = qs('[data-history-snapshot]', root);
    let timestamps = [];
    let closed = false;

    async function showSnapshot(index) {
        const at = timestamps[index];
        if (!at || !snapshotPanel) return;
        const body = qs('[data-snapshot-rows]', snapshotPanel);
        const title = qs('[data-snapshot-title]', snapshotPanel);
        snapshotPanel.hidden = false;
        title.textContent = 'Process anlık görüntüsü yükleniyor…';
        emptyRow(body, 5, 'Yükleniyor…');
        const response = await getJson(`${snapshotUrl}?${new URLSearchParams({ at })}`);
        if (closed) return;
        if (!response.isSuccess) {
            title.textContent = 'Process anlık görüntüsü';
            emptyRow(body, 5, response.message || 'Bu zamana yakın process kaydı yok.');
            return;
        }
        const data = response.data;
        const busy = data.cpuBusyPercent === null ? '' : ` · sunucu CPU ${formatPercent(data.cpuBusyPercent)}`;
        title.textContent = `Process anlık görüntüsü · ${data.collectedAt}${busy}`;
        if (!data.processes.length) {
            emptyRow(body, 5, 'Process listesi boş.');
            return;
        }
        body.replaceChildren(...data.processes.map(p => {
            const tr = element('tr');
            tr.append(
                cell(String(p.p), 'text-right font-mono text-xs tabular-nums'),
                cell(p.u || '—', 'text-sm'),
                commandCell(p.n, p.a),
                cell(formatPercent(p.c), num),
                cell(formatBytes(p.r * 1024), 'whitespace-nowrap text-right tabular-nums'));
            return tr;
        }));
        snapshotPanel.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }

    const charts = qsa('[data-history-chart]', root).map(canvas => {
        const key = canvas.dataset.historyChart;
        return {
            key,
            empty: canvas.parentElement.querySelector('[data-chart-empty]'),
            chart: new window.Chart(canvas, { type: 'line', data: { labels: [], datasets: [] }, options: chartOptions(key, showSnapshot) })
        };
    });

    async function load(params) {
        charts.forEach(({ empty }) => { empty.textContent = 'Yükleniyor…'; empty.hidden = false; });
        const response = await getJson(`${historyUrl}?${new URLSearchParams(params)}`);
        if (closed) return;
        if (!response.isSuccess) {
            charts.forEach(({ empty }) => { empty.textContent = response.message || 'Geçmiş alınamadı.'; empty.hidden = false; });
            return;
        }
        const data = response.data;
        timestamps = data.timestamps ?? [];
        if (description) description.textContent = data.description;
        if (customForm) {
            customForm.elements.from.value = data.from;
            customForm.elements.to.value = data.to;
        }
        charts.forEach(({ key, chart, empty }) => {
            chart.data.labels = data.labels;
            chart.data.datasets = datasets(key === 'memory' ? data.memory : data.cpu);
            chart.update('none');
            empty.textContent = 'Bu aralık için container verisi yok.';
            empty.hidden = data.labels.length > 0;
        });
        renderContainers(qs('[data-history-containers]', root), data.containers ?? []);
        renderProcesses(qs('[data-history-processes="cpu"]', root), data.processesByCpu ?? [], false);
        renderProcesses(qs('[data-history-processes="memory"]', root), data.processesByMemory ?? [], true);
    }

    function activate(button) {
        qsa('[data-history-range]', root).forEach(item => item.classList.toggle('is-active', item === button));
    }

    root.addEventListener('click', event => {
        const button = event.target.closest('[data-history-range]');
        if (button) {
            activate(button);
            const range = button.dataset.historyRange;
            if (customForm) customForm.hidden = range !== 'custom';
            if (range !== 'custom') load({ range });
            return;
        }
        if (event.target.closest('[data-snapshot-close]') && snapshotPanel) snapshotPanel.hidden = true;
    });

    customForm?.addEventListener('submit', event => {
        event.preventDefault();
        const from = customForm.elements.from.value;
        const to = customForm.elements.to.value;
        if (!from) return;
        load(to ? { from, to } : { from });
    });

    const observer = new MutationObserver(() => charts.forEach(({ key, chart }) => {
        chart.options = chartOptions(key, showSnapshot);
        chart.update('none');
    }));
    observer.observe(document.documentElement, { attributes: true, attributeFilter: ['class'] });
    onPageDispose(() => {
        closed = true;
        observer.disconnect();
        charts.forEach(({ chart }) => chart.destroy());
    });

    load({ range: '24h' });
}
