import { on, qs } from '../../core/dom.js';
import { notify } from '../../core/notify.js';

const UPPER = 'ABCDEFGHJKLMNPQRSTUVWXYZ';
const LOWER = 'abcdefghijkmnopqrstuvwxyz';
const DIGITS = '23456789';
const ALL = UPPER + LOWER + DIGITS;

function pick(chars) {
    const values = new Uint32Array(1);
    crypto.getRandomValues(values);
    return chars[values[0] % chars.length];
}

/** Harf ve rakamlardan 24 karakterlik parola (büyük, küçük harf ve rakam garantili; SQL Server kuralına da uyar). */
export function generatePassword(length = 24) {
    const chars = [pick(UPPER), pick(LOWER), pick(DIGITS)];
    while (chars.length < length) chars.push(pick(ALL));
    for (let i = chars.length - 1; i > 0; i--) {
        const values = new Uint32Array(1);
        crypto.getRandomValues(values);
        const j = values[0] % (i + 1);
        [chars[i], chars[j]] = [chars[j], chars[i]];
    }
    return chars.join('');
}

export async function copyText(text, label = 'Panoya kopyalandı.') {
    try {
        await navigator.clipboard.writeText(text);
        notify.success(label);
    } catch {
        notify.error('Panoya kopyalanamadı; değeri elle seçip kopyalayın.');
    }
}

/** Parola alanının göster/gizle, kopyala ve yeniden üret düğmeleri. */
export function bindPasswordField(root) {
    on(root, 'click', '[data-password-toggle]', (event, button) => {
        const input = qs('[data-password-input]', button.closest('[data-password-field]'));
        input.type = input.type === 'password' ? 'text' : 'password';
    });
    on(root, 'click', '[data-password-copy]', (event, button) => {
        const input = qs('[data-password-input]', button.closest('[data-password-field]'));
        if (input.value) copyText(input.value, 'Parola panoya kopyalandı.');
    });
    on(root, 'click', '[data-password-generate]', (event, button) => {
        const input = qs('[data-password-input]', button.closest('[data-password-field]'));
        input.value = generatePassword();
        input.type = 'text';
        notify.info('Yeni parola üretildi; kaydetmeyi unutmayın.');
    });
}
