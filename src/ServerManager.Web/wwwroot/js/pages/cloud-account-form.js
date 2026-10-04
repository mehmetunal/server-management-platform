import { qs } from '../core/dom.js';
import { bindAjaxForm } from '../core/forms.js';
import { navigate } from '../core/navigation.js';

bindAjaxForm(qs('[data-cloud-account-form]'), {
    onSuccess: response => navigate(response.data, response.message)
});

const provider = qs('[data-provider-select]');
const help = qs('[data-provider-help]');
provider?.addEventListener('change', () => {
    help.textContent = provider.selectedOptions[0]?.dataset.help ?? '';
});
