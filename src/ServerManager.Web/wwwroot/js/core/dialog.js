import { element } from './dom.js';
import { failureMessage } from './http.js';

const BASE_OPTIONS = {
    buttonsStyling: false,
    showCancelButton: true,
    cancelButtonText: 'Vazgeç',
    customClass: {
        popup: 'swal-popup',
        title: 'swal-title',
        htmlContainer: 'swal-text',
        input: 'form-control swal-input',
        inputLabel: 'swal-input-label',
        validationMessage: 'swal-validation',
        actions: 'swal-actions',
        cancelButton: 'btn-secondary'
    }
};

function swal() {
    if (!window.Swal) throw new Error('SweetAlert2 yüklenmedi.');
    return window.Swal;
}

function buildContent(message, checkLabel, code) {
    const content = element('div');
    if (message) content.appendChild(element('p', '', message));
    if (code) content.appendChild(element('pre', 'swal-code', code));
    if (!checkLabel) return { content, check: null };
    const label = element('label', 'swal-check');
    const check = element('input', 'form-check');
    check.type = 'checkbox';
    label.append(check, document.createTextNode(checkLabel));
    content.appendChild(label);
    return { content, check };
}

/**
 * Onay diyaloğu. expected verilirse kullanıcıdan bu değeri yazması istenir.
 * action, onaydan sonra diyalog açıkken çalışır; başarısız yanıt diyalogda gösterilir.
 * code verilirse (ör. komut satırı) mesajın altında sabit genişlikli gösterilir.
 * Onaylanıp başarılı olursa action yanıtını, aksi halde null döner.
 */
export async function confirmAction({ title, message, code, confirmText = 'Onayla', danger = true, expected, checkLabel, action }) {
    const Swal = swal();
    const { content, check } = buildContent(message, checkLabel, code);

    const result = await Swal.fire({
        ...BASE_OPTIONS,
        customClass: { ...BASE_OPTIONS.customClass, confirmButton: danger ? 'btn-danger' : 'btn-primary' },
        icon: danger ? 'warning' : 'question',
        titleText: title,
        html: content,
        input: expected ? 'text' : undefined,
        inputLabel: expected ? `Onaylamak için yazın: ${expected}` : undefined,
        inputAttributes: { autocomplete: 'off', autocapitalize: 'off', spellcheck: 'false' },
        inputValidator: expected
            ? value => (value.trim() === expected ? undefined : 'Yazdığınız değer eşleşmiyor.')
            : undefined,
        confirmButtonText: confirmText,
        focusCancel: !expected,
        showLoaderOnConfirm: true,
        allowOutsideClick: () => !Swal.isLoading(),
        preConfirm: async value => {
            if (!action) return { isSuccess: true };
            const response = await action({
                confirmation: expected ? String(value).trim() : undefined,
                checked: check ? check.checked : false
            });
            if (!response.isSuccess) {
                Swal.showValidationMessage(failureMessage(response));
                return false;
            }
            return response;
        }
    });

    return result.isConfirmed ? result.value : null;
}

export function alertError(title, message) {
    return swal().fire({
        ...BASE_OPTIONS,
        showCancelButton: false,
        customClass: { ...BASE_OPTIONS.customClass, confirmButton: 'btn-primary' },
        icon: 'error',
        titleText: title,
        text: message,
        confirmButtonText: 'Tamam'
    });
}
