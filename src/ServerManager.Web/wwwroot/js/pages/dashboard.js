import { debounce, qs, qsa } from '../core/dom.js';
import { getJson } from '../core/http.js';
import { createChartGroups } from '../features/monitoring/charts.js';
import { startLiveUpdates } from '../features/monitoring/live.js';

const charts = createChartGroups();
const fleet = qs('[data-live-fleet]');

async function refreshSummary() {
    const url = fleet?.dataset.dashboardUrl;
    if (!url) return;
    const response = await getJson(url);
    if (!response.isSuccess || !response.data) return;
    qsa('[data-live-dashboard]').forEach(element => {
        const value = response.data[element.dataset.liveDashboard];
        if (value !== undefined && value !== null) element.textContent = value;
    });
}

startLiveUpdates({
    onUpdate: debounce(() => {
        refreshSummary();
        charts.filter(group => group.live).forEach(group => group.refresh());
    }, 3000)
});
