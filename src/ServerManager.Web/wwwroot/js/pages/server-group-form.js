import { qs, qsa } from '../core/dom.js';
import { bindAjaxForm } from '../core/forms.js';
import { navigate } from '../core/navigation.js';

bindAjaxForm(qs('[data-group-form]'), {
    onSuccess: response => navigate(response.data, response.message)
});

const search = qs('[data-server-search]');
search?.addEventListener('input', () => {
    const term = search.value.trim().toLowerCase();
    qsa('[data-server-option]').forEach(option => {
        option.hidden = term.length > 0 && !option.dataset.search.includes(term);
    });
});
