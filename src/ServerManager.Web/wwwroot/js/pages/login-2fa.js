import { qs, qsa } from '../core/dom.js';
import { bindAjaxForm, clearErrors } from '../core/forms.js';

const form = qs('[data-two-factor-form]');
const flag = qs('[data-recovery-flag]', form);
const input = qs('[data-code-input]', form);
const label = qs('[data-code-label]', form);
const toggle = qs('[data-toggle-mode]');

function setMode(recovery) {
    flag.value = recovery ? 'true' : 'false';
    qsa('[data-mode-lead]').forEach(node => { node.hidden = node.dataset.modeLead !== (recovery ? 'recovery' : 'code'); });
    qsa('[data-mode-only]', form).forEach(node => { node.hidden = recovery; });
    label.textContent = recovery ? 'Kurtarma kodu' : 'Doğrulama kodu';
    input.placeholder = recovery ? 'xxxxx-xxxxx' : '123456';
    input.inputMode = recovery ? 'text' : 'numeric';
    toggle.textContent = recovery ? 'Doğrulama kodu kullan' : 'Kurtarma kodu kullan';
    toggle.title = recovery ? 'Doğrulama uygulamasındaki kodu kullan' : 'Doğrulama kodu yerine kurtarma kodu kullan';
    input.value = '';
    clearErrors(form);
    input.focus();
}

toggle.addEventListener('click', () => setMode(flag.value !== 'true'));

bindAjaxForm(form, {
    onSuccess: response => window.location.assign(response.data || '/')
});
