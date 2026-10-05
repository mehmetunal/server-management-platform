import { qs, qsa } from '../core/dom.js';
import { bindAjaxForm } from '../core/forms.js';
import { navigate } from '../core/navigation.js';

const form = qs('[data-backup-job-form]');
const source = qs('[data-backup-source]', form);
const engine = qs('[data-backup-engine]', form);
const schedule = qs('[data-backup-schedule]', form);
const encryption = qs('[data-backup-encryption]', form);
const userInput = qs('[data-engine-user]', form);
const portInput = qs('[data-engine-port]', form);

const engineDefaults = JSON.parse(engine.dataset.engineDefaults || '{}');

// Bir alan, üzerindeki tüm mod koşulları (tür, veritabanı motoru, zamanlama) sağlanıyorsa görünür.
const modes = [
    ['data-source-mode', () => source.value],
    ['data-engine-mode', () => engine.value],
    ['data-schedule-mode', () => schedule.value]
];

function matches(field) {
    return modes.every(([attribute, value]) =>
        !field.hasAttribute(attribute) || field.getAttribute(attribute).split(' ').includes(value()));
}

function syncEngineDefaults() {
    const defaults = engineDefaults[engine.value] || {};
    userInput.placeholder = defaults.user ? `ör. ${defaults.user}` : '';
    portInput.placeholder = defaults.port ? String(defaults.port) : '';
}

function sync() {
    for (const field of qsa(modes.map(([attribute]) => `[${attribute}]`).join(','), form)) {
        field.hidden = !matches(field);
    }
    for (const field of qsa('[data-encryption-field]', form)) field.hidden = !encryption.checked;
    syncEngineDefaults();
}

source.addEventListener('change', sync);
engine.addEventListener('change', sync);
schedule.addEventListener('change', sync);
encryption.addEventListener('change', sync);
sync();

bindAjaxForm(form, {
    onSuccess: response => navigate(response.data, response.message)
});
