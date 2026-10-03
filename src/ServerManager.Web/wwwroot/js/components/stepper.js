import { qsa } from '../core/dom.js';

const STATE_CLASSES = ['is-active', 'is-done', 'is-warning', 'is-failed'];

/** Adım göstergesi: <li data-step="..."> öğelerine durum sınıfı verir (.wizard-steps). */
export function createStepper(root) {
    const steps = new Map(qsa('[data-step]', root).map(item => [item.dataset.step, item]));

    function set(step, state) {
        const item = steps.get(step);
        if (!item) return;
        item.classList.remove(...STATE_CLASSES);
        if (state) item.classList.add(`is-${state}`);
        if (state === 'active') item.setAttribute('aria-current', 'step');
        else item.removeAttribute('aria-current');
    }

    return {
        set,
        setMany(list, state) {
            list.forEach(step => set(step, state));
        },
        /** Aktif adımı başarısız olarak işaretler; aktif adım yoksa verilen adımı. */
        failActive(fallback, state = 'failed') {
            const active = [...steps.entries()].find(([, item]) => item.classList.contains('is-active'));
            set(active ? active[0] : fallback, state);
        }
    };
}
