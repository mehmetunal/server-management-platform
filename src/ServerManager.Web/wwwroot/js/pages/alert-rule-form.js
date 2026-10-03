import { qs, qsa } from '../core/dom.js';
import { bindAjaxForm } from '../core/forms.js';
import { navigate } from '../core/navigation.js';

const form = qs('[data-rule-form]');
const kind = qs('[data-rule-kind]', form);
const thresholdField = qs('[data-rule-threshold]', form);
const durationField = qs('[data-rule-duration]', form);

function syncKind() {
    const option = kind.selectedOptions[0];
    const unit = option?.dataset.threshold ?? 'none';
    thresholdField.hidden = unit === 'none';
    for (const element of qsa('[data-threshold-label], [data-threshold-hint]', thresholdField)) {
        element.hidden = (element.dataset.thresholdLabel ?? element.dataset.thresholdHint) !== unit;
    }
    durationField.hidden = option?.dataset.duration !== 'true';
}

kind.addEventListener('change', syncKind);
syncKind();

bindAjaxForm(form, {
    onSuccess: response => navigate(response.data, response.message)
});
