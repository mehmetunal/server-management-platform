import { debounce, on, qs, qsa } from '../core/dom.js';
import { getHtml } from '../core/http.js';
import { createChartGroups } from '../features/monitoring/charts.js';
import { initServerPage } from '../features/servers/server-page.js';

const [charts] = createChartGroups();
const panel = qs('[data-metrics-panel]');

async function refreshPanel() {
    if (!panel) return;
    const response = await getHtml(panel.dataset.panelUrl);
    if (response.ok) panel.innerHTML = response.html;
}

const refreshLiveViews = debounce(() => {
    if (charts?.live) charts.refresh();
    refreshPanel();
}, 1500);

initServerPage({ onUpdate: refreshLiveViews, regions: ['server-header'] });

on(document, 'click', '[data-range-link]', (event, link) => {
    event.preventDefault();
    if (!charts || link.classList.contains('is-active')) return;
    qsa('[data-range-link]').forEach(item => item.classList.toggle('is-active', item === link));
    qs('[data-range-description]').textContent = link.dataset.description;
    window.history.replaceState(window.history.state, '', link.href);
    charts.setSource(link.dataset.seriesUrl, link.dataset.live === 'true');
});
