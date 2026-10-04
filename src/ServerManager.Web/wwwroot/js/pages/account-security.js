import { on, qs, setBusy } from '../core/dom.js';
import { promptPassword, showSecretList } from '../core/dialog.js';
import { bindAjaxForm, clearErrors, showErrors } from '../core/forms.js';
import { failureMessage, postForm } from '../core/http.js';
import { notify } from '../core/notify.js';
import { refreshRegions } from '../core/regions.js';

const REGION = 'two-factor';

const reloadPanel = () => refreshRegions([REGION]);

async function showCodes(response) {
    await showSecretList({
        title: 'Kurtarma kodları',
        message: 'Telefonunuza erişemezseniz bu kodlarla giriş yapabilirsiniz. Her kod bir kez kullanılır ve bir daha gösterilmez; parola yöneticinize kaydedin.',
        items: response.data ?? []
    });
}

bindAjaxForm(qs('[data-change-password-form]'), {
    onSuccess: (response, form) => {
        form.reset();
        notify.success(response.message);
    }
});

on(document, 'click', '[data-setup-start]', async (event, button) => {
    setBusy(button, true);
    try {
        const response = await postForm(button.dataset.url, {});
        if (!response.isSuccess) {
            notify.error(failureMessage(response));
            return;
        }
        const area = qs('[data-setup-area]');
        qs('[data-qr]', area).src = response.data.qrCodeDataUri;
        qs('[data-shared-key]', area).textContent = response.data.sharedKey;
        area.hidden = false;
        button.hidden = true;
        qs('[name="Code"]', area).focus();
    } finally {
        setBusy(button, false);
    }
});

on(document, 'click', '[data-copy-key]', async () => {
    const key = qs('[data-shared-key]').textContent.replaceAll(' ', '');
    try {
        await navigator.clipboard.writeText(key);
        notify.success('Anahtar panoya kopyalandı.');
    } catch {
        notify.error('Kopyalanamadı; anahtarı elle seçip kopyalayın.');
    }
});

on(document, 'submit', '[data-enable-form]', async (event, form) => {
    event.preventDefault();
    const submit = qs('[type="submit"]', form);
    clearErrors(form);
    setBusy(submit, true);
    try {
        const response = await postForm(form.action, new FormData(form));
        if (!response.isSuccess) {
            showErrors(form, response);
            return;
        }
        notify.success(response.message);
        await showCodes(response);
        await reloadPanel();
    } finally {
        setBusy(submit, false);
    }
});

on(document, 'click', '[data-regenerate-codes]', async (event, button) => {
    const response = await promptPassword({
        title: 'Kurtarma kodlarını yenile',
        message: 'Yeni 10 kod oluşturulur; eski kodların hepsi geçersiz olur.',
        confirmText: 'Yenile',
        action: ({ password }) => postForm(button.dataset.url, { password })
    });
    if (!response) return;
    await showCodes(response);
    await reloadPanel();
});

on(document, 'click', '[data-disable-two-factor]', async (event, button) => {
    const response = await promptPassword({
        title: 'İki adımlı doğrulamayı kapat',
        message: 'Hesabınız yalnızca parolayla korunur. Doğrulama anahtarı ve kurtarma kodları silinir.',
        confirmText: 'Kapat',
        danger: true,
        action: ({ password }) => postForm(button.dataset.url, { password })
    });
    if (!response) return;
    notify.success(response.message);
    await reloadPanel();
});

on(document, 'click', '[data-forget-machine]', async (event, button) => {
    setBusy(button, true);
    try {
        const response = await postForm(button.dataset.url, {});
        if (!response.isSuccess) {
            notify.error(failureMessage(response));
            return;
        }
        notify.success(response.message);
        await reloadPanel();
    } finally {
        setBusy(button, false);
    }
});
