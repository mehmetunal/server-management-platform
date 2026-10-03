import { debounce, on } from '../core/dom.js';
import { getHtml } from '../core/http.js';
import { notify } from '../core/notify.js';

const SEARCH_DELAY_MS = 350;

function urlFromForm(form) {
    const params = new URLSearchParams();
    for (const [key, value] of new FormData(form)) {
        if (typeof value === 'string' && value.trim() !== '') params.append(key, value.trim());
    }
    const base = form.getAttribute('action') || window.location.pathname;
    const query = params.toString();
    return query ? `${base}?${query}` : base;
}

function syncForm(form, url) {
    const params = new URL(url, window.location.origin).searchParams;
    for (const field of form.elements) {
        if (!field.name || field.type === 'submit') continue;
        field.value = params.get(field.name) ?? '';
    }
}

/**
 * Filtre formu ve sayfalama ile çalışan liste. Liste action'ı AJAX isteğinde partial döner;
 * URL pushState ile güncellenir, geri/ileri tuşları listeyi yeniden yükler.
 */
export function createAjaxList(root, { form, onLoaded } = {}) {
    let sequence = 0;

    async function load(url, { push = true } = {}) {
        const current = ++sequence;
        root.setAttribute('aria-busy', 'true');
        const response = await getHtml(url);
        if (current !== sequence) return;
        root.removeAttribute('aria-busy');
        if (!response.ok) {
            notify.error(response.message || 'Liste yüklenemedi.');
            return;
        }
        root.innerHTML = response.html;
        if (push && url !== window.location.pathname + window.location.search) {
            window.history.pushState({ ajaxList: true }, '', url);
        }
        onLoaded?.(root);
    }

    if (form) {
        const submit = () => load(urlFromForm(form));
        form.addEventListener('submit', event => {
            event.preventDefault();
            submit();
        });
        on(form, 'change', '[data-auto-submit]', submit);
        on(form, 'input', '[data-live-search]', debounce(submit, SEARCH_DELAY_MS));
        on(form, 'click', '[data-list-link]', (event, link) => {
            event.preventDefault();
            syncForm(form, link.href);
            load(link.href);
        });
    }

    on(root, 'click', '.pager-link, [data-list-link]', (event, link) => {
        event.preventDefault();
        if (form) syncForm(form, link.href);
        load(link.href);
    });

    window.addEventListener('popstate', () => {
        if (form) syncForm(form, window.location.href);
        load(window.location.pathname + window.location.search, { push: false });
    });

    return {
        reload: () => load(window.location.pathname + window.location.search, { push: false })
    };
}
