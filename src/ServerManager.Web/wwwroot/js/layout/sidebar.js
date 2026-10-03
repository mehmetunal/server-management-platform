import { on, qs } from '../core/dom.js';

export function initSidebar() {
    const sidebar = qs('[data-sidebar]');
    const overlay = qs('[data-sidebar-overlay]');
    if (!sidebar || !overlay) return;

    const setOpen = open => {
        sidebar.classList.toggle('is-open', open);
        overlay.classList.toggle('is-open', open);
    };

    on(document, 'click', '[data-sidebar-toggle]', () => setOpen(!sidebar.classList.contains('is-open')));
    overlay.addEventListener('click', () => setOpen(false));
}
