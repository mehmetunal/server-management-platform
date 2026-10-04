import { qs } from '../core/dom.js';
import { bindAjaxForm } from '../core/forms.js';
import { navigate } from '../core/navigation.js';

bindAjaxForm(qs('[data-storage-form]'), {
    onSuccess: response => navigate(response.data, response.message)
});
