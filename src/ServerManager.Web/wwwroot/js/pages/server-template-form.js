import { qs, qsa } from '../core/dom.js';
import { bindAjaxForm } from '../core/forms.js';
import { navigate } from '../core/navigation.js';

bindAjaxForm(qs('[data-template-form]'), {
    onSuccess: response => navigate(response.data, response.message)
});

const kind = qs('[data-kind-select]');
kind?.addEventListener('change', () => {
    qsa('[data-kind-hint]').forEach(hint => {
        hint.hidden = hint.dataset.kindHint !== kind.value;
    });
});
