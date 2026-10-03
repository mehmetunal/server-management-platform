import { qs, qsa } from '../../core/dom.js';

/** Seçilen build türüne göre ilgili alanları ve açıklamayı gösterir. */
export function initBuildFields(form) {
    const select = qs('[data-build-type]', form);
    if (!select) return;

    const update = () => {
        qsa('[data-build-field]', form).forEach(field => {
            field.hidden = field.dataset.buildField !== select.value;
        });
        qsa('[data-build-hint]', form).forEach(hint => {
            hint.hidden = hint.dataset.buildHint !== select.value;
        });
    };

    select.addEventListener('change', update);
    update();
}
