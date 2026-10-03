import { createAjaxList } from '../components/ajax-list.js';
import { qs } from '../core/dom.js';

createAjaxList(qs('[data-ajax-list]'), { form: qs('[data-list-filter]') });
