import { bindAjaxActions } from '../components/ajax-actions.js';
import { createAjaxList } from '../components/ajax-list.js';
import { startAutoRefresh } from '../components/auto-refresh.js';
import { qs } from '../core/dom.js';

const root = qs('[data-ajax-list]');
const list = createAjaxList(root, { form: qs('[data-list-filter]') });

bindAjaxActions(document, { onSuccess: () => list.reload() });
startAutoRefresh(() => list.reload(), Number(root.dataset.autoRefresh || 30) * 1000);
