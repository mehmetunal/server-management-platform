import { qs, qsa } from '../core/dom.js';
import { bindAjaxForm } from '../core/forms.js';
import { navigate } from '../core/navigation.js';

const form = qs('[data-uptime-form]');
const type = qs('[data-uptime-type]', form);

function syncType() {
    for (const field of qsa('[data-uptime-mode]', form)) field.hidden = field.dataset.uptimeMode !== type.value;
}

type.addEventListener('change', syncType);
syncType();

bindAjaxForm(form, {
    onSuccess: response => navigate(response.data, response.message)
});
