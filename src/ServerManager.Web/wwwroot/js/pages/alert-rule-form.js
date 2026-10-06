import { qs, qsa } from '../core/dom.js';
import { bindAjaxForm } from '../core/forms.js';
import { navigate } from '../core/navigation.js';

const form = qs('[data-rule-form]');
const kind = qs('[data-rule-kind]', form);
const thresholdField = qs('[data-rule-threshold]', form);
const durationField = qs('[data-rule-duration]', form);
const serviceField = qs('[data-rule-service]', form);
const isNew = form.hasAttribute('data-rule-new');
let touched = false;

function syncKind(applyDefaults) {
    const option = kind.selectedOptions[0];
    const unit = option?.dataset.threshold ?? 'none';
    thresholdField.hidden = unit === 'none';
    for (const element of qsa('[data-threshold-label], [data-threshold-hint]', thresholdField)) {
        element.hidden = (element.dataset.thresholdLabel ?? element.dataset.thresholdHint) !== unit;
    }
    durationField.hidden = option?.dataset.duration !== 'true';
    const hints = qsa('[data-duration-hint]', durationField);
    const specific = hints.some(hint => hint.dataset.durationHint === option?.value);
    for (const hint of hints) {
        hint.hidden = specific ? hint.dataset.durationHint !== option?.value : hint.dataset.durationHint !== 'default';
    }
    if (serviceField) serviceField.hidden = option?.dataset.service !== 'true';

    // Yeni kuralda eşik/süre elle değiştirilmediyse türün önerilen değerleri yazılır.
    if (applyDefaults && isNew && !touched && option) {
        const threshold = qs('input[name="Threshold"]', form);
        const duration = qs('input[name="DurationMinutes"]', form);
        if (threshold && option.dataset.defaultThreshold !== '0') threshold.value = option.dataset.defaultThreshold;
        if (duration) duration.value = option.dataset.defaultDuration;
    }
}

for (const input of qsa('input[name="Threshold"], input[name="DurationMinutes"]', form)) {
    input.addEventListener('input', () => { touched = true; });
}

kind.addEventListener('change', () => syncKind(true));
syncKind(false);

bindAjaxForm(form, {
    onSuccess: response => navigate(response.data, response.message)
});
