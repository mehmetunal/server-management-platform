import { setBusy } from './dom.js';
import { postForm } from './http.js';
import { notify } from './notify.js';

function fieldSelector(attribute, name) {
    return `[${attribute}="${CSS.escape(name)}"]`;
}

export function clearErrors(form) {
    form.querySelectorAll('.input-validation-error').forEach(field => field.classList.remove('input-validation-error'));
    form.querySelectorAll('[data-valmsg-for]').forEach(slot => {
        slot.textContent = '';
        slot.classList.add('field-validation-valid');
    });
    const summary = form.querySelector('[data-form-error]');
    if (summary) {
        summary.textContent = '';
        summary.hidden = true;
    }
}

/** Alan hatalarını ilgili alanın altına, alana bağlanamayanları formun genel hata kutusuna yazar. */
export function showErrors(form, response) {
    const general = [];
    let firstField = null;

    for (const [name, messages] of Object.entries(response.errors ?? {})) {
        const text = messages.join(' ');
        const field = name ? form.querySelector(fieldSelector('name', name)) : null;
        const slot = name ? form.querySelector(fieldSelector('data-valmsg-for', name)) : null;
        if (field) {
            field.classList.add('input-validation-error');
            firstField ??= field;
        }
        if (slot) {
            slot.textContent = text;
            slot.classList.remove('field-validation-valid');
            slot.classList.add('field-validation-error');
        } else {
            general.push(text);
        }
    }

    const summary = form.querySelector('[data-form-error]');
    const hasFieldErrors = firstField !== null || general.length < Object.keys(response.errors ?? {}).length;
    if (summary && (general.length || !hasFieldErrors)) {
        summary.textContent = general.length ? general.join(' ') : response.message;
        summary.hidden = false;
    }

    // Uzun formlarda hatalı alan ekran dışında kalabileceği için hata yalnızca alan altlarındaysa ayrıca toastr gösterilir.
    if (!summary || (hasFieldErrors && general.length === 0)) notify.error(response.message || 'İşlem başarısız.');
    firstField?.focus();
}

/**
 * Formu AJAX ile gönderir. onSuccess(response, form) başarılı yanıtta çağrılır.
 * Doğrulama hataları formda gösterilir; sayfa yenilenmez.
 */
export function bindAjaxForm(form, { onSuccess } = {}) {
    if (!form) return;
    form.addEventListener('submit', async event => {
        event.preventDefault();
        if (form.dataset.submitting === 'true') return;

        const submit = form.querySelector('[type="submit"]');
        clearErrors(form);
        form.dataset.submitting = 'true';
        setBusy(submit, true);
        try {
            const response = await postForm(form.action, new FormData(form));
            if (!response.isSuccess) {
                showErrors(form, response);
                return;
            }
            await onSuccess?.(response, form);
        } finally {
            setBusy(submit, false);
            delete form.dataset.submitting;
        }
    });
}
