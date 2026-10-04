import { on, qs, qsa } from '../core/dom.js';

const STORAGE_KEY = 'sm-sidebar';
const desktopQuery = window.matchMedia('(min-width: 1024px)');

export function initSidebar() {
    const sidebar = qs('[data-sidebar]');
    const overlay = qs('[data-sidebar-overlay]');
    if (!sidebar || !overlay || !qs('[data-sidebar-toggle]')) return;

    const collapsed = () => localStorage.getItem(STORAGE_KEY) === 'collapsed';

    const paint = () => {
        const desktop = desktopQuery.matches;
        const overlayOpen = !desktop && sidebar.classList.contains('is-open');
        const compact = desktop ? collapsed() : !overlayOpen;

        document.documentElement.classList.toggle('sidebar-collapsed', collapsed());
        sidebar.classList.toggle('is-open', overlayOpen);
        overlay.classList.toggle('is-open', overlayOpen);
        const label = compact ? 'Menüyü aç' : 'Menüyü kapat';
        qsa('[data-sidebar-toggle]').forEach(toggle => {
            toggle.setAttribute('aria-expanded', compact ? 'false' : 'true');
            toggle.title = label;
            toggle.setAttribute('aria-label', label);
            toggle.dataset.tooltip = label;
        });
    };

    on(document, 'click', '[data-sidebar-toggle]', () => {
        if (desktopQuery.matches)
            localStorage.setItem(STORAGE_KEY, collapsed() ? 'expanded' : 'collapsed');
        else
            sidebar.classList.toggle('is-open');
        paint();
    });

    overlay.addEventListener('click', () => {
        sidebar.classList.remove('is-open');
        paint();
    });

    desktopQuery.addEventListener('change', () => {
        sidebar.classList.remove('is-open');
        paint();
    });

    qsa('a', sidebar).forEach(link => link.addEventListener('click', () => {
        if (!desktopQuery.matches) {
            sidebar.classList.remove('is-open');
            paint();
        }
    }));

    paint();
}
