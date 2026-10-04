import { qs } from '../core/dom.js';
import { bindAjaxForm } from '../core/forms.js';
import { notify } from '../core/notify.js';

bindAjaxForm(qs('[data-settings-form]'), {
    onSuccess: response => notify.success(response.message || 'Ayarlar kaydedildi.')
});
