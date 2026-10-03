import { qs, qsa } from '../../core/dom.js';

/** Seçilen kimlik doğrulama tipine ve sudo seçimine göre ilgili alanları gösterir. */
export function initAuthFields(form) {
    const select = qs('[data-auth-type]', form);
    if (!select) return;
    const sudo = qs('[data-use-sudo]', form);

    const update = () => {
        qsa('[data-auth-field]', form).forEach(field => {
            field.hidden = !field.dataset.authField.split(',').includes(select.value);
        });
        qsa('[data-sudo-field]', form).forEach(field => {
            field.hidden = !(sudo && sudo.checked);
        });
    };

    select.addEventListener('change', update);
    sudo?.addEventListener('change', update);
    update();
}
