import { qs, qsa } from '../core/dom.js';
import { bindAjaxForm } from '../core/forms.js';
import { navigate } from '../core/navigation.js';

const form = qs('[data-backup-job-form]');
const source = qs('[data-backup-source]', form);
const schedule = qs('[data-backup-schedule]', form);
const encryption = qs('[data-backup-encryption]', form);

function syncModes(attribute, value) {
    for (const field of qsa(`[${attribute}]`, form)) {
        field.hidden = !field.getAttribute(attribute).split(' ').includes(value);
    }
}

function sync() {
    syncModes('data-source-mode', source.value);
    syncModes('data-schedule-mode', schedule.value);
    for (const field of qsa('[data-encryption-field]', form)) field.hidden = !encryption.checked;
}

source.addEventListener('change', sync);
schedule.addEventListener('change', sync);
encryption.addEventListener('change', sync);
sync();

bindAjaxForm(form, {
    onSuccess: response => navigate(response.data, response.message)
});
