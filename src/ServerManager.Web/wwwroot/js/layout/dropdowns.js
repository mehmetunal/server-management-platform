import { on, qsa } from '../core/dom.js';

export function closeAllDropdowns(except) {
    qsa('[data-dropdown-menu]').forEach(menu => {
        if (menu !== except) menu.hidden = true;
    });
}

export function initDropdowns() {
    on(document, 'click', '[data-dropdown-toggle]', (event, toggle) => {
        const menu = toggle.closest('[data-dropdown]')?.querySelector('[data-dropdown-menu]');
        if (!menu) return;
        event.stopPropagation();
        const willOpen = menu.hidden;
        closeAllDropdowns(menu);
        menu.hidden = !willOpen;
    });
    document.addEventListener('click', event => {
        if (!(event.target instanceof Element) || !event.target.closest('[data-dropdown]')) closeAllDropdowns();
    });
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape') closeAllDropdowns();
    });
}
