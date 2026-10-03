import { qs } from '../core/dom.js';
import { bindAjaxForm } from '../core/forms.js';
import { navigate } from '../core/navigation.js';
import { initBuildFields } from '../features/deployments/build-fields.js';

const form = qs('[data-project-form]');

initBuildFields(form);
bindAjaxForm(form, {
    onSuccess: response => navigate(response.data, response.message)
});
