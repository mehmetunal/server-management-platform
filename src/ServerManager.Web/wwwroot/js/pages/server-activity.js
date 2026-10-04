import { createAjaxList } from '../components/ajax-list.js';
import { qs } from '../core/dom.js';
import { initServerPage } from '../features/servers/server-page.js';

createAjaxList(qs('[data-ajax-list]'), { form: qs('[data-list-filter]') });
initServerPage();
