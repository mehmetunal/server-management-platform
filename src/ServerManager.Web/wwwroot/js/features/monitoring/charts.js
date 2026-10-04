import { qsa } from '../../core/dom.js';
import { getJson } from '../../core/http.js';
import { formatBytes, formatNumber } from '../../core/format.js';
import { onPageDispose } from '../../core/page-scope.js';

const COLORS = {
    cpu: '#6366f1',
    memory: '#10b981',
    disk: '#f59e0b',
    rx: '#0ea5e9',
    tx: '#ec4899',
    load: '#8b5cf6'
};

const PERCENT_CHARTS = new Set(['cpu', 'memory', 'disk', 'fleet']);

function themeColors() {
    return document.documentElement.classList.contains('dark')
        ? { text: '#94a3b8', grid: 'rgba(148, 163, 184, 0.12)' }
        : { text: '#64748b', grid: 'rgba(100, 116, 139, 0.12)' };
}

function dataset(label, data, color, fill) {
    const count = data ? data.length : 0;
    return {
        label,
        data,
        borderColor: color,
        backgroundColor: `${color}22`,
        borderWidth: 2,
        pointRadius: count > 0 && count <= 30 ? 2.5 : 0,
        pointHoverRadius: 3,
        tension: 0.3,
        fill
    };
}

function buildDatasets(key, series) {
    switch (key) {
        case 'cpu': return [dataset('CPU', series.cpu, COLORS.cpu, true)];
        case 'memory': return [dataset('RAM', series.memory, COLORS.memory, true)];
        case 'disk': return [dataset('Disk', series.disk, COLORS.disk, true)];
        case 'network': return [dataset('Gelen', series.rx, COLORS.rx, false), dataset('Giden', series.tx, COLORS.tx, false)];
        case 'load': return [dataset('Load (1 dk)', series.load, COLORS.load, true)];
        case 'fleet': return [
            dataset('CPU', series.cpu, COLORS.cpu, false),
            dataset('RAM', series.memory, COLORS.memory, false),
            dataset('Disk', series.disk, COLORS.disk, false)
        ];
        default: return [];
    }
}

function formatValue(key, value) {
    if (key === 'network') return `${formatBytes(value)}/s`;
    if (PERCENT_CHARTS.has(key)) return `%${formatNumber(value)}`;
    return formatNumber(value);
}

function chartOptions(key) {
    const colors = themeColors();
    return {
        responsive: true,
        maintainAspectRatio: false,
        animation: false,
        interaction: { mode: 'index', intersect: false },
        plugins: {
            legend: {
                display: key === 'network' || key === 'fleet',
                labels: { color: colors.text, boxWidth: 12, usePointStyle: true }
            },
            tooltip: {
                callbacks: { label: context => `${context.dataset.label}: ${formatValue(key, context.parsed.y)}` }
            }
        },
        scales: {
            x: {
                ticks: { color: colors.text, maxTicksLimit: 8, maxRotation: 0, autoSkip: true },
                grid: { display: false }
            },
            y: {
                beginAtZero: true,
                max: PERCENT_CHARTS.has(key) ? 100 : undefined,
                ticks: { color: colors.text, maxTicksLimit: 6, callback: value => formatValue(key, value) },
                grid: { color: colors.grid }
            }
        }
    };
}

function createGroup(container) {
    let closed = false;
    const group = {
        url: container.dataset.seriesUrl,
        live: container.dataset.liveRange === 'true',
        charts: qsa('[data-metric-chart]', container).map(canvas => {
            const key = canvas.dataset.metricChart;
            return {
                key,
                chart: new window.Chart(canvas, { type: 'line', data: { labels: [], datasets: buildDatasets(key, {}) }, options: chartOptions(key) }),
                empty: canvas.parentElement.querySelector('[data-chart-empty]')
            };
        })
    };

    group.refresh = async () => {
        const response = await getJson(group.url);
        if (closed) return;
        if (!response.isSuccess) {
            group.charts.forEach(({ empty }) => {
                if (!empty) return;
                empty.textContent = 'Grafik verisi alınamadı.';
                empty.hidden = false;
            });
            return;
        }
        const series = response.data ?? {};
        const labels = series.labels ?? [];
        group.charts.forEach(({ key, chart, empty }) => {
            chart.data.labels = labels;
            chart.data.datasets = buildDatasets(key, series);
            chart.update('none');
            if (!empty) return;
            empty.textContent = 'Bu aralık için veri yok.';
            empty.hidden = labels.length > 0;
        });
    };

    group.setSource = (url, live) => {
        group.url = url;
        group.live = live;
        return group.refresh();
    };

    group.refresh();
    group.close = () => { closed = true; };
    return group;
}

/** [data-metric-charts] gruplarını oluşturur; tema değişince eksen renkleri güncellenir. */
export function createChartGroups() {
    if (!window.Chart) return [];
    const groups = qsa('[data-metric-charts]').map(createGroup);
    const observer = groups.length
        ? new MutationObserver(() => groups.forEach(group => group.charts.forEach(({ key, chart }) => {
            chart.options = chartOptions(key);
            chart.update('none');
        })))
        : null;
    observer?.observe(document.documentElement, { attributes: true, attributeFilter: ['class'] });
    onPageDispose(() => {
        observer?.disconnect();
        groups.forEach(group => {
            group.close();
            group.charts.forEach(({ chart }) => chart.destroy());
        });
    });
    return groups;
}
