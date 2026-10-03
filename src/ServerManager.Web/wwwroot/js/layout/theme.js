import { on, qsa } from '../core/dom.js';

const THEME_KEY = 'sm-theme';
const media = window.matchMedia('(prefers-color-scheme: dark)');

const currentTheme = () => localStorage.getItem(THEME_KEY) || 'system';

function applyTheme(theme) {
    const dark = theme === 'dark' || (theme === 'system' && media.matches);
    document.documentElement.classList.toggle('dark', dark);
    qsa('[data-theme-icon]').forEach(icon => {
        icon.hidden = icon.dataset.themeIcon !== theme;
    });
}

export function initTheme({ onChange } = {}) {
    applyTheme(currentTheme());
    media.addEventListener('change', () => {
        if (currentTheme() === 'system') applyTheme('system');
    });
    on(document, 'click', '[data-theme-value]', (event, button) => {
        const theme = button.dataset.themeValue;
        localStorage.setItem(THEME_KEY, theme);
        applyTheme(theme);
        onChange?.(theme);
    });
}
