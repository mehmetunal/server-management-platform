import { createAjaxList } from '../components/ajax-list.js';
import { qs } from '../core/dom.js';
import { startLiveUpdates } from '../features/monitoring/live.js';

createAjaxList(qs('[data-ajax-list]'), { form: qs('[data-list-filter]') });
startLiveUpdates();
