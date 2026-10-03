import { qs } from '../core/dom.js';
import { bindAjaxForm } from '../core/forms.js';

bindAjaxForm(qs('[data-login-form]'), {
    onSuccess: response => window.location.assign(response.data || '/')
});
