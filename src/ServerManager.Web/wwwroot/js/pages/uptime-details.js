import { bindAjaxActions } from '../components/ajax-actions.js';
import { startAutoRefresh } from '../components/auto-refresh.js';
import { qs } from '../core/dom.js';
import { getHtml } from '../core/http.js';

const region = qs('[data-uptime-details]');

async function refresh() {
    const response = await getHtml(region.dataset.url);
    if (response.ok) region.innerHTML = response.html;
}

bindAjaxActions(document, { onSuccess: refresh });
startAutoRefresh(refresh, Number(region.dataset.autoRefresh || 30) * 1000);
