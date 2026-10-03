export const qs = (selector, root = document) => root.querySelector(selector);

export const qsa = (selector, root = document) => Array.from(root.querySelectorAll(selector));

/** Olay yetkilendirme: AJAX ile sonradan eklenen öğeler de yakalanır. */
export function on(root, type, selector, handler) {
    root.addEventListener(type, event => {
        const target = event.target instanceof Element ? event.target.closest(selector) : null;
        if (target && root.contains(target)) handler(event, target);
    });
}

export function setBusy(button, busy, busyLabel) {
    if (!button) return;
    button.disabled = busy;
    button.setAttribute('aria-busy', busy ? 'true' : 'false');
    const spinner = button.querySelector('[data-spinner]');
    if (spinner) spinner.hidden = !busy;
    const label = button.querySelector('[data-label]');
    if (!label || !busyLabel) return;
    if (busy) {
        label.dataset.idleText ??= label.textContent;
        label.textContent = busyLabel;
    } else if (label.dataset.idleText) {
        label.textContent = label.dataset.idleText;
    }
}

export function debounce(fn, wait) {
    let timer = null;
    return (...args) => {
        clearTimeout(timer);
        timer = setTimeout(() => fn(...args), wait);
    };
}

export function element(tag, className, text) {
    const node = document.createElement(tag);
    if (className) node.className = className;
    if (text !== undefined) node.textContent = text;
    return node;
}
