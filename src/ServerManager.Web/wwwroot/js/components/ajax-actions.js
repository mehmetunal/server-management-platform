import { on, setBusy } from '../core/dom.js';
import { confirmAction } from '../core/dialog.js';
import { failureMessage, postForm } from '../core/http.js';
import { navigate } from '../core/navigation.js';
import { notify } from '../core/notify.js';

// data-field-container="web" → container=web
function fieldsOf(trigger) {
    const params = new URLSearchParams();
    for (const [key, value] of Object.entries(trigger.dataset)) {
        if (key.startsWith('field') && key.length > 5) params.set(key.charAt(5).toLowerCase() + key.slice(6), value);
    }
    return params;
}

async function runConfirmed(trigger, send) {
    const { confirmTitle, confirmMessage, confirmLabel, confirmName, confirmCheck, checkField = 'force', confirmTone } = trigger.dataset;
    return confirmAction({
        title: confirmTitle,
        message: confirmMessage,
        confirmText: confirmLabel,
        expected: confirmName,
        checkLabel: confirmCheck,
        danger: confirmTone !== 'neutral',
        action: ({ confirmation, checked }) => {
            const extra = {};
            if (confirmation !== undefined) extra.confirmationName = confirmation;
            if (confirmCheck) extra[checkField] = checked ? 'true' : 'false';
            return send(extra);
        }
    });
}

async function runDirect(trigger, send) {
    setBusy(trigger, true);
    try {
        const response = await send();
        if (!response.isSuccess) {
            notify.error(failureMessage(response));
            return null;
        }
        return response;
    } finally {
        setBusy(trigger, false);
    }
}

/**
 * [data-ajax-action] butonlarını bağlar: data-url'e data-field-* değerlerini POST eder.
 * data-confirm-title varsa önce SweetAlert2 onayı alınır (data-confirm-name: yazarak onay,
 * data-confirm-check: ek onay kutusu). data-redirect varsa başarıda o adrese geçilir,
 * yoksa toastr gösterilip onSuccess(trigger, response) çağrılır.
 */
export function bindAjaxActions(root = document, { onSuccess } = {}) {
    on(root, 'click', '[data-ajax-action]', async (event, trigger) => {
        event.preventDefault();
        if (trigger.disabled) return;

        const send = (extra = {}) => {
            const params = fieldsOf(trigger);
            for (const [key, value] of Object.entries(extra)) params.set(key, value);
            return postForm(trigger.dataset.url, params);
        };

        const response = trigger.dataset.confirmTitle ? await runConfirmed(trigger, send) : await runDirect(trigger, send);
        if (!response) return;

        if (trigger.hasAttribute('data-redirect')) {
            navigate(trigger.dataset.redirect || response.data, response.message);
            return;
        }
        notify.success(response.message || 'İşlem tamamlandı.');
        await onSuccess?.(trigger, response);
    });
}
