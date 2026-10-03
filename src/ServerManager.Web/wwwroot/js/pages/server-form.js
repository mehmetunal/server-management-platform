import { qs } from '../core/dom.js';
import { bindAjaxForm } from '../core/forms.js';
import { navigate } from '../core/navigation.js';
import { initAuthFields } from '../features/servers/auth-fields.js';

const form = qs('[data-server-form]');

initAuthFields(form);
bindAjaxForm(form, {
    onSuccess: response => navigate(response.data, response.message)
});
