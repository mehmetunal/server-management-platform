import { bindAjaxActions } from '../components/ajax-actions.js';
import { qs } from '../core/dom.js';
import { bindAjaxForm } from '../core/forms.js';
import { notify } from '../core/notify.js';

const result = qs('[data-api-key-result]');
const form = qs('[data-api-key-form]');

// Anahtar yalnızca bu yanıtta gelir; sayfada gösterilir, form gizlenir. "Tamam" listeyi yeniler.
bindAjaxForm(form, {
    onSuccess: response => {
        qs('[data-api-key-token]', result).value = response.data.token;
        result.hidden = false;
        form.hidden = true;
        notify.success(response.message);
        qs('[data-api-key-token]', result).select();
    }
});

qs('[data-api-key-copy]')?.addEventListener('click', async () => {
    const input = qs('[data-api-key-token]', result);
    try {
        await navigator.clipboard.writeText(input.value);
        notify.success('Anahtar panoya kopyalandı.');
    } catch {
        input.select();
        notify.error('Kopyalanamadı; anahtarı seçip elle kopyalayın.');
    }
});

bindAjaxActions(document, { onSuccess: () => window.location.reload() });
