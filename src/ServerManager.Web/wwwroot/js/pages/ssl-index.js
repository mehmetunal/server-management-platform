import { bindAjaxActions } from '../components/ajax-actions.js';
import { createAjaxList } from '../components/ajax-list.js';
import { qs } from '../core/dom.js';

const list = createAjaxList(qs('[data-ajax-list]'), { form: qs('[data-list-filter]') });

bindAjaxActions(document, { onSuccess: () => list.reload() });
