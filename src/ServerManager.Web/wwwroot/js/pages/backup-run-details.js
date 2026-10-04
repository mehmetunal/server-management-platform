import { bindAjaxActions } from '../components/ajax-actions.js';
import { startAutoRefresh } from '../components/auto-refresh.js';
import { qs } from '../core/dom.js';
import { refreshRegions } from '../core/regions.js';

const REFRESH_MS = 3000;
const REGIONS = ['backup-run-actions', 'backup-run-body'];

const isRunning = () => qs('[data-backup-run]')?.dataset.running === 'true';

function scrollLog() {
    const log = qs('[data-backup-log]');
    if (log) log.scrollTop = log.scrollHeight;
}

let refresher = null;

async function refresh() {
    await refreshRegions(REGIONS);
    scrollLog();
    if (!isRunning()) refresher?.stop();
}

bindAjaxActions(document, { onSuccess: refresh });

scrollLog();
if (isRunning()) refresher = startAutoRefresh(refresh, REFRESH_MS);
