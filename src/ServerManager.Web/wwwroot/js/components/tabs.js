import { qsa } from '../core/dom.js';

/**
 * [data-tabs] içindeki [data-tab] düğmeleriyle [data-tab-panel] bölümlerini değiştirir.
 * Seçili sekme URL hash'inde tutulur; activators[ad] sekme ilk görünür olduğunda çağrılır.
 */
export function createTabs({ defaultTab, activators = {} } = {}) {
    const tabs = qsa('[data-tabs] [data-tab]');
    if (!tabs.length) return null;

    function activate(name) {
        if (!tabs.some(tab => tab.dataset.tab === name)) return;
        tabs.forEach(tab => tab.classList.toggle('is-active', tab.dataset.tab === name));
        qsa('[data-tab-panel]').forEach(panel => {
            panel.hidden = panel.dataset.tabPanel !== name;
        });
        activators[name]?.();
    }

    tabs.forEach(tab => tab.addEventListener('click', () => {
        const name = tab.dataset.tab;
        const url = window.location.pathname + window.location.search + (name === defaultTab ? '' : `#${name}`);
        window.history.replaceState(window.history.state, '', url);
        activate(name);
    }));

    const initial = window.location.hash.replace('#', '');
    if (initial) activate(initial);

    return { activate };
}
