import { confirmAction } from '../core/dialog.js';
import { on, qs, qsa, setBusy } from '../core/dom.js';
import { clearErrors, showErrors } from '../core/forms.js';
import { failureMessage, postForm } from '../core/http.js';
import { navigate } from '../core/navigation.js';
import { notify } from '../core/notify.js';

const SELF_LOCKOUT_KEY = 'ConfirmSelfLockout';
const form = qs('[data-role-form]');

function needsSelfConfirm(response) {
    return !response.isSuccess && Object.prototype.hasOwnProperty.call(response.errors ?? {}, SELF_LOCKOUT_KEY);
}

function selfConfirmMessage(response) {
    return (response.errors?.[SELF_LOCKOUT_KEY] ?? []).join(' ') || response.message;
}

/** Kendi yönetim yetkisini kaldıran değişiklikte kullanıcıdan ek onay alır ve isteği onayla tekrarlar. */
async function withSelfConfirm(send) {
    const response = await send(false);
    if (!needsSelfConfirm(response)) return response;

    return confirmAction({
        title: 'Kendi yetkinizi kaldırıyorsunuz',
        message: selfConfirmMessage(response),
        confirmText: 'Yine de kaydet',
        action: () => send(true)
    });
}

function updateCount() {
    const counter = qs('[data-permission-count]');
    if (!counter) return;
    const total = qsa('[data-permission]').length;
    const checked = qsa('[data-permission]:checked').length;
    counter.textContent = `${checked} / ${total} izin seçili`;
}

if (form) {
    updateCount();
    form.addEventListener('change', event => {
        if (event.target.matches?.('[data-permission]')) updateCount();
    });

    on(form, 'click', '[data-group-toggle]', (event, button) => {
        event.preventDefault();
        const boxes = qsa('[data-permission]:not(:disabled)', button.closest('[data-permission-group]'));
        const selectAll = boxes.some(box => !box.checked);
        boxes.forEach(box => { box.checked = selectAll; });
        updateCount();
    });

    form.addEventListener('submit', async event => {
        event.preventDefault();
        if (form.dataset.submitting === 'true') return;

        const submit = form.querySelector('[type="submit"]');
        const confirmField = qs('[data-confirm-self]', form);
        clearErrors(form);
        form.dataset.submitting = 'true';
        setBusy(submit, true);
        try {
            const response = await withSelfConfirm(confirmed => {
                confirmField.value = confirmed ? 'true' : 'false';
                return postForm(form.action, new FormData(form));
            });
            if (!response) return;
            if (!response.isSuccess) {
                showErrors(form, response);
                return;
            }
            navigate(response.data, response.message);
        } finally {
            confirmField.value = 'false';
            setBusy(submit, false);
            delete form.dataset.submitting;
        }
    });

    on(form, 'click', '[data-role-reset]', async (event, button) => {
        event.preventDefault();
        const send = confirmed => postForm(button.dataset.url, { confirmSelfLockout: confirmed ? 'true' : 'false' });
        const response = await confirmAction({
            title: 'Varsayılana döndür',
            message: 'Rolün izinleri panelin varsayılanlarına (eklentilerin önerdikleri dahil) döndürülür. Kaldırılan izinler varsa bu roldeki kullanıcıların oturumu yenilenir.',
            confirmText: 'Varsayılana döndür',
            danger: false,
            action: async () => {
                const first = await send(false);
                if (!needsSelfConfirm(first)) return first;
                // Diyalog açıkken ikinci onay sorulamaz; mesajı göstermek için yanıt başarısız döner, kullanıcı tekrar dener.
                return window.confirm(selfConfirmMessage(first)) ? send(true) : { ...first, errors: {}, message: 'İşlem iptal edildi.' };
            }
        });
        if (response) navigate(response.data, response.message);
    });
}

const deleteForm = qs('[data-role-delete]');
if (deleteForm) {
    deleteForm.addEventListener('submit', async event => {
        event.preventDefault();
        const response = await confirmAction({
            title: 'Rolü sil',
            message: `${deleteForm.dataset.roleName} rolü kalıcı olarak silinecek.`,
            confirmText: 'Sil',
            expected: deleteForm.dataset.roleName,
            action: () => postForm(deleteForm.action, new FormData(deleteForm))
        });
        if (!response) return;
        if (!response.isSuccess) {
            notify.error(failureMessage(response));
            return;
        }
        navigate(response.data, response.message);
    });
}
