import { on, qsa } from '../core/dom.js';

export function openModal(modal) {
    if (!modal) return;
    modal.hidden = false;
    modal.querySelector('input:not([type="hidden"]), select, textarea')?.focus();
}

export function closeModal(modal) {
    if (modal) modal.hidden = true;
}

/** Çok alanlı form diyalogları ([data-modal]); onaylar SweetAlert2 ile yapılır. */
export function bindModals(root = document) {
    on(root, 'click', '[data-modal-open]', (event, trigger) => openModal(document.getElementById(trigger.dataset.modalOpen)));
    on(root, 'click', '[data-modal-close]', (event, button) => closeModal(button.closest('[data-modal]')));
    root.addEventListener('click', event => {
        if (event.target instanceof Element && event.target.matches('[data-modal]')) closeModal(event.target);
    });
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape') qsa('[data-modal]').forEach(closeModal);
    });
}
