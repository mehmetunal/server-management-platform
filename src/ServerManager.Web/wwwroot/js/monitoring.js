(function () {
    'use strict';

    var COLORS = {
        cpu: '#6366f1',
        memory: '#10b981',
        disk: '#f59e0b',
        rx: '#0ea5e9',
        tx: '#ec4899',
        load: '#8b5cf6'
    };
    var BAR_CLASSES = ['bg-emerald-500', 'bg-amber-500', 'bg-red-500', 'bg-slate-300', 'dark:bg-slate-700'];
    var numberFormat = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 1 });

    function formatBytes(value) {
        var units = ['B', 'KB', 'MB', 'GB', 'TB'];
        var unit = 0;
        while (Math.abs(value) >= 1024 && unit < units.length - 1) {
            value /= 1024;
            unit++;
        }
        return numberFormat.format(value) + ' ' + units[unit];
    }

    function isDark() {
        return document.documentElement.classList.contains('dark');
    }

    function themeColors() {
        return isDark()
            ? { text: '#94a3b8', grid: 'rgba(148, 163, 184, 0.12)' }
            : { text: '#64748b', grid: 'rgba(100, 116, 139, 0.12)' };
    }

    function debounce(fn, wait) {
        var timer = null;
        return function () {
            clearTimeout(timer);
            timer = setTimeout(fn, wait);
        };
    }

    function getJson(url) {
        return fetch(url, {
            credentials: 'same-origin',
            headers: { 'Accept': 'application/json', 'X-Requested-With': 'XMLHttpRequest' }
        }).then(function (response) {
            if (!response.ok) throw new Error('HTTP ' + response.status);
            return response.json();
        });
    }

    // ---- Grafikler ----

    function dataset(label, data, color, fill) {
        var count = data ? data.length : 0;
        return {
            label: label,
            data: data,
            borderColor: color,
            backgroundColor: color + '22',
            borderWidth: 2,
            pointRadius: count > 0 && count <= 30 ? 2.5 : 0,
            pointHoverRadius: 3,
            tension: 0.3,
            fill: !!fill
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

    function isPercentChart(key) {
        return key === 'cpu' || key === 'memory' || key === 'disk' || key === 'fleet';
    }

    function chartOptions(key) {
        var colors = themeColors();
        var percent = isPercentChart(key);
        var bytes = key === 'network';
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
                    callbacks: {
                        label: function (context) {
                            var value = context.parsed.y;
                            var text = bytes ? formatBytes(value) + '/s' : percent ? '%' + numberFormat.format(value) : numberFormat.format(value);
                            return context.dataset.label + ': ' + text;
                        }
                    }
                }
            },
            scales: {
                x: {
                    ticks: { color: colors.text, maxTicksLimit: 8, maxRotation: 0, autoSkip: true },
                    grid: { display: false }
                },
                y: {
                    beginAtZero: true,
                    max: percent ? 100 : undefined,
                    ticks: {
                        color: colors.text,
                        maxTicksLimit: 6,
                        callback: function (value) {
                            if (bytes) return formatBytes(value) + '/s';
                            if (percent) return '%' + value;
                            return numberFormat.format(value);
                        }
                    },
                    grid: { color: colors.grid }
                }
            }
        };
    }

    function initCharts() {
        if (!window.Chart) return [];
        var groups = [];
        document.querySelectorAll('[data-metric-charts]').forEach(function (container) {
            var group = {
                container: container,
                url: container.getAttribute('data-series-url'),
                live: container.getAttribute('data-live-range') === 'true',
                charts: []
            };
            container.querySelectorAll('[data-metric-chart]').forEach(function (canvas) {
                var key = canvas.getAttribute('data-metric-chart');
                var chart = new window.Chart(canvas, {
                    type: 'line',
                    data: { labels: [], datasets: buildDatasets(key, {}) },
                    options: chartOptions(key)
                });
                group.charts.push({ key: key, chart: chart, empty: canvas.parentElement.querySelector('[data-chart-empty]') });
            });
            group.refresh = function () { return loadSeries(group); };
            group.refresh();
            groups.push(group);
        });

        if (groups.length > 0) {
            new MutationObserver(function () {
                groups.forEach(function (group) {
                    group.charts.forEach(function (item) {
                        item.chart.options = chartOptions(item.key);
                        item.chart.update('none');
                    });
                });
            }).observe(document.documentElement, { attributes: true, attributeFilter: ['class'] });
        }
        return groups;
    }

    function loadSeries(group) {
        return getJson(group.url).then(function (response) {
            var series = response.data || {};
            var labels = series.labels || [];
            group.charts.forEach(function (item) {
                item.chart.data.labels = labels;
                item.chart.data.datasets = buildDatasets(item.key, series);
                item.chart.update('none');
                if (item.empty) item.empty.hidden = labels.length > 0;
            });
        }).catch(function () {
            group.charts.forEach(function (item) {
                if (item.empty) {
                    item.empty.textContent = 'Grafik verisi alınamadı.';
                    item.empty.hidden = false;
                }
            });
        });
    }

    // ---- Canlı güncelleme ----

    function updateResource(scope, key, percent, text) {
        var element = scope.querySelector('[data-resource="' + key + '"]');
        if (!element) return;
        var warning = parseFloat(element.getAttribute('data-warning'));
        var critical = parseFloat(element.getAttribute('data-critical'));
        var label = element.querySelector('[data-resource-text]');
        var bar = element.querySelector('[data-resource-bar]');
        if (label) label.textContent = text;
        if (bar) {
            bar.style.width = Math.max(0, Math.min(100, percent)) + '%';
            BAR_CLASSES.forEach(function (cls) { bar.classList.remove(cls); });
            bar.classList.add(percent >= critical ? 'bg-red-500' : percent >= warning ? 'bg-amber-500' : 'bg-emerald-500');
        }
    }

    function updateStatus(scope, payload) {
        var badge = scope.querySelector('[data-live-status]');
        if (!badge) return;
        badge.className = payload.statusBadgeClass;
        var text = badge.querySelector('[data-live-status-text]');
        if (text) text.textContent = payload.statusText;
    }

    function setText(scope, selector, value) {
        var element = scope.querySelector(selector);
        if (element && value !== undefined && value !== null) element.textContent = value;
    }

    function applyUpdate(scope, payload) {
        updateStatus(scope, payload);
        var latest = payload.latest;
        if (!latest) return;
        updateResource(scope, 'cpu', latest.cpu, latest.cpuText);
        updateResource(scope, 'memory', latest.memory, latest.memoryText);
        updateResource(scope, 'disk', latest.disk, latest.diskText);
        setText(scope, '[data-live-uptime]', latest.uptimeText);
        setText(scope, '[data-live-load]', latest.loadText);
        setText(scope, '[data-live-collected]', latest.collectedAtText);
    }

    function refreshPanel() {
        var panel = document.querySelector('[data-metrics-panel]');
        if (!panel) return;
        fetch(panel.getAttribute('data-panel-url'), {
            credentials: 'same-origin',
            headers: { 'Accept': 'text/html', 'X-Requested-With': 'XMLHttpRequest' }
        }).then(function (response) {
            if (!response.ok) throw new Error('HTTP ' + response.status);
            return response.text();
        }).then(function (html) {
            panel.innerHTML = html;
        }).catch(function () { /* panel bir sonraki güncellemede yenilenir */ });
    }

    function refreshDashboard(url) {
        getJson(url).then(function (response) {
            var data = response.data;
            if (!data) return;
            document.querySelectorAll('[data-live-dashboard]').forEach(function (element) {
                var key = element.getAttribute('data-live-dashboard');
                if (data[key] !== undefined) element.textContent = data[key];
            });
        }).catch(function () { /* sonraki olayda tekrar denenir */ });
    }

    function initLive(chartGroups) {
        var serverRoot = document.querySelector('[data-live-server]');
        var fleetRoot = document.querySelector('[data-live-fleet]');
        var root = serverRoot || fleetRoot;
        if (!root || !window.signalR) return null;

        var serverId = serverRoot ? serverRoot.getAttribute('data-live-server') : null;
        var dashboardUrl = fleetRoot ? fleetRoot.getAttribute('data-dashboard-url') : null;

        var refreshServerViews = debounce(function () {
            chartGroups.forEach(function (group) { if (group.live) group.refresh(); });
            refreshPanel();
        }, 1500);
        var refreshFleetViews = debounce(function () {
            if (dashboardUrl) refreshDashboard(dashboardUrl);
            chartGroups.forEach(function (group) { if (group.live) group.refresh(); });
        }, 3000);

        var connection = new window.signalR.HubConnectionBuilder()
            .withUrl(root.getAttribute('data-hub-url'))
            .withAutomaticReconnect()
            .configureLogging(window.signalR.LogLevel.Warning)
            .build();

        function handleUpdate(payload) {
            if (serverId && payload.serverId === serverId) {
                applyUpdate(serverRoot, payload);
                refreshServerViews();
            }
            document.querySelectorAll('[data-live-server-row="' + payload.serverId + '"]').forEach(function (row) {
                applyUpdate(row, payload);
            });
            if (fleetRoot) refreshFleetViews();
        }

        connection.on('serverUpdated', handleUpdate);

        function join() {
            return serverId ? connection.invoke('JoinServer', serverId) : connection.invoke('JoinFleet');
        }

        connection.onreconnected(function () { join().catch(function () { }); });
        connection.start().then(join).catch(function () { /* canlı güncelleme olmadan sayfa çalışmaya devam eder */ });

        return handleUpdate;
    }

    function initCollectButton(handleUpdate) {
        var button = document.querySelector('[data-collect-metrics]');
        if (!button || !window.ServerManager) return;
        var label = button.querySelector('[data-label]');
        var spinner = button.querySelector('[data-spinner]');
        var originalText = label ? label.textContent : '';

        button.addEventListener('click', function () {
            button.disabled = true;
            if (spinner) spinner.hidden = false;
            if (label) label.textContent = 'Toplanıyor…';

            window.ServerManager.postJson(button.getAttribute('data-url')).then(function (response) {
                var data = response.data;
                if (!response.isSuccess || !data) {
                    window.ServerManager.showToast('error', response.message || 'Metrikler toplanamadı.');
                    return;
                }
                window.ServerManager.showToast(data.isSuccess ? 'success' : 'error', data.message);
                if (handleUpdate) handleUpdate(data);
                else window.location.reload();
            }).catch(function () {
                window.ServerManager.showToast('error', 'Ağ hatası: istek gönderilemedi.');
            }).finally(function () {
                button.disabled = false;
                if (spinner) spinner.hidden = true;
                if (label) label.textContent = originalText;
            });
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        var chartGroups = initCharts();
        var handleUpdate = initLive(chartGroups);
        initCollectButton(handleUpdate);
    });
})();
