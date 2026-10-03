// Tüm buton ve linkler için tema uyumlu tooltip. Metin sırası: data-tooltip, title, aria-label.
// title, tarayıcının kendi tooltip'i çift görünmesin diye data-tooltip'e taşınır.
const TARGET_SELECTOR = '[data-tooltip], [title], a[aria-label], button[aria-label], [role="button"][aria-label]';
const EXCLUDED_SELECTOR = 'input, textarea, select, option, iframe, [data-tooltip-off], .CodeMirror, .xterm';
const SHOW_DELAY_MS = 350;
const GAP = 8;
const EDGE = 6;

let tooltip;
let current = null;
let showTimer = 0;
let lastPointerType = 'mouse';

function findTarget(node) {
    const target = node instanceof Element ? node.closest(TARGET_SELECTOR) : null;
    if (!target || target.closest(EXCLUDED_SELECTOR)) return null;
    return target;
}

function textOf(target) {
    const title = target.getAttribute('title');
    if (title !== null) {
        target.removeAttribute('title');
        const text = title.trim();
        if (text) {
            target.dataset.tooltip = text;
            if (!target.hasAttribute('aria-label') && !target.textContent.trim()) {
                target.setAttribute('aria-label', text);
            }
        }
    }
    const text = (target.dataset.tooltip || target.getAttribute('aria-label') || '').trim();
    const visible = target.textContent.replace(/\s+/g, ' ').trim();
    const truncated = target.scrollWidth > target.clientWidth;
    return text && (text !== visible || truncated) ? text : '';
}

function place(target) {
    const rect = target.getBoundingClientRect();
    const { offsetWidth: width, offsetHeight: height } = tooltip;
    let top = rect.top - height - GAP;
    if (top < EDGE) top = rect.bottom + GAP;
    const centered = rect.left + rect.width / 2 - width / 2;
    const left = Math.min(Math.max(centered, EDGE), window.innerWidth - width - EDGE);
    tooltip.style.transform = `translate(${Math.round(left)}px, ${Math.round(top)}px)`;
}

function show(target) {
    const text = textOf(target);
    if (!text || !target.isConnected) return;
    current = target;
    tooltip.textContent = text;
    tooltip.hidden = false;
    place(target);
    tooltip.classList.add('is-visible');
    const describedBy = target.getAttribute('aria-describedby');
    if (!describedBy?.split(' ').includes(tooltip.id)) {
        target.setAttribute('aria-describedby', describedBy ? `${describedBy} ${tooltip.id}` : tooltip.id);
    }
}

function hide() {
    clearTimeout(showTimer);
    if (!current) return;
    const remaining = (current.getAttribute('aria-describedby') ?? '').split(' ').filter(id => id && id !== tooltip.id);
    if (remaining.length) current.setAttribute('aria-describedby', remaining.join(' '));
    else current.removeAttribute('aria-describedby');
    current = null;
    tooltip.classList.remove('is-visible');
    tooltip.hidden = true;
}

function schedule(target) {
    if (target === current) return;
    hide();
    textOf(target);
    showTimer = setTimeout(() => show(target), SHOW_DELAY_MS);
}

export function initTooltips() {
    if (tooltip) return;
    tooltip = document.createElement('div');
    tooltip.id = 'app-tooltip';
    tooltip.className = 'app-tooltip';
    tooltip.setAttribute('role', 'tooltip');
    tooltip.hidden = true;
    document.body.appendChild(tooltip);

    document.addEventListener('pointerover', event => {
        lastPointerType = event.pointerType;
        if (event.pointerType === 'touch') return;
        const target = findTarget(event.target);
        if (target) schedule(target);
        else if (current || showTimer) hide();
    });
    document.addEventListener('pointerout', event => {
        const target = findTarget(event.target);
        if (target && !target.contains(event.relatedTarget)) hide();
    });
    document.addEventListener('focusin', event => {
        if (lastPointerType === 'touch') return;
        const target = findTarget(event.target);
        if (target?.matches(':focus-visible')) schedule(target);
    });
    document.addEventListener('focusout', hide);
    document.addEventListener('pointerdown', hide, true);
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape') hide();
    });
    window.addEventListener('scroll', hide, true);
    window.addEventListener('resize', hide);
}
