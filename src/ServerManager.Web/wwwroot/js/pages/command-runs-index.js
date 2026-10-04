import { createAjaxList } from '../components/ajax-list.js';
import { startAutoRefresh } from '../components/auto-refresh.js';
import { qs } from '../core/dom.js';

const REFRESH_MS = 5000;

const root = qs('[data-ajax-list]');
const list = createAjaxList(root, { form: qs('[data-list-filter]') });

const hasRunning = () => qs('[data-command-rows]', root)?.dataset.running === 'true';

startAutoRefresh(async () => {
    if (hasRunning()) await list.reload();
}, REFRESH_MS);
