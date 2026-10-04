import { getHtml } from '../../core/http.js';
import { notify } from '../../core/notify.js';
import { disposePage } from '../../core/page-scope.js';

const CACHE_MS = 15000;

const cache = new Map();
let sequence = 0;
let soft = false;
let lastPath = window.location.pathname;

function sameDocument(url) {
    return url.origin === window.location.origin;
}

function cacheKey(url) {
    return url.pathname + url.search;
}

function tabLink(target) {
    return target instanceof Element ? target.closest('nav.tab-list a.tab') : null;
}

function prefetch(url) {
    if (!sameDocument(url)) return;
    const key = cacheKey(url);
    if (key === window.location.pathname + window.location.search) return;
    const hit = cache.get(key);
    if (hit && Date.now() - hit.at < CACHE_MS) return;
    const promise = getHtml(url.href, { partial: false });
    cache.set(key, { at: Date.now(), promise });
}

async function loadHtml(url) {
    const key = cacheKey(url);
    const hit = cache.get(key);
    cache.delete(key);
    if (hit && Date.now() - hit.at < CACHE_MS) return hit.promise;
    return getHtml(url.href, { partial: false });
}

function heading(root) {
    return root.querySelector('[data-topbar-heading]') ?? root.querySelector('.app-topbar .min-w-0');
}

function adopt(node) {
    return document.importNode(node, true);
}

function replaceChildren(current, next) {
    if (!current || !next) return;
    current.replaceChildren(...[...next.childNodes].map(adopt));
}

function scriptPath(src) {
    try {
        return new URL(src, window.location.origin).pathname;
    } catch {
        return '';
    }
}

function loadClassic(src) {
    return new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = src;
        script.onload = () => resolve();
        script.onerror = () => reject(new Error(src));
        document.body.appendChild(script);
    });
}

async function ensureScripts(doc) {
    const existing = new Set([...document.scripts].map(script => scriptPath(script.src)).filter(Boolean));
    for (const script of doc.querySelectorAll('script[src]:not([type="module"])')) {
        const raw = script.getAttribute('src');
        const path = scriptPath(raw);
        if (!path || existing.has(path)) continue;
        await loadClassic(new URL(raw, window.location.origin).href);
        existing.add(path);
    }
}

function ensureStyles(doc) {
    const links = [...document.querySelectorAll('link[rel="stylesheet"]')];
    const currentPage = links.find(link => link.href.includes('/css/pages/'));
    const nextPage = [...doc.querySelectorAll('link[rel="stylesheet"]')].find(link => link.href.includes('/css/pages/'));
    if (nextPage && currentPage) {
        if (currentPage.href !== nextPage.href) currentPage.href = nextPage.href;
    } else if (nextPage) {
        document.head.appendChild(adopt(nextPage));
    } else if (currentPage) {
        currentPage.remove();
    }

    for (const link of doc.querySelectorAll('link[rel="stylesheet"]')) {
        if (link.href.includes('/css/site.css') || link.href.includes('/css/pages/')) continue;
        const path = scriptPath(link.href);
        if (!path || links.some(item => item.href.includes(path))) continue;
        document.head.appendChild(adopt(link));
    }
}

function pageModule(doc) {
    return [...doc.querySelectorAll('script[type="module"][src]')]
        .map(script => script.getAttribute('src'))
        .find(src => src?.includes('/js/pages/')) ?? null;
}

async function show(url, { push }) {
    const id = ++sequence;
    document.documentElement.classList.add('is-tab-navigating');
    try {
        const response = await loadHtml(url);
        if (id !== sequence) return;
        if (!response.ok || !response.html) {
            notify.error(response.message || 'Sayfa açılamadı.');
            return;
        }

        const doc = new DOMParser().parseFromString(response.html, 'text/html');
        const nextMain = doc.querySelector('.app-content');
        const currentMain = document.querySelector('.app-content');
        if (!nextMain || !currentMain) {
            window.location.assign(url.href);
            return;
        }

        if (push) {
            if (!soft) window.history.replaceState({ softNav: true }, '', window.location.href);
            window.history.pushState({ softNav: true }, '', url.href);
            soft = true;
        }

        disposePage();
        document.title = doc.title;
        replaceChildren(heading(document), heading(doc));
        replaceChildren(currentMain, nextMain);
        ensureStyles(doc);
        lastPath = url.pathname;
        window.scrollTo(0, 0);

        await ensureScripts(doc);
        if (id !== sequence) return;
        const src = pageModule(doc);
        if (!src) return;
        const moduleUrl = new URL(src, window.location.origin);
        moduleUrl.searchParams.set('nav', String(id));
        await import(moduleUrl.href);
    } catch {
        if (id === sequence) notify.error('Sayfa yüklenemedi.');
    } finally {
        if (id === sequence) document.documentElement.classList.remove('is-tab-navigating');
    }
}

/** Sekme linkleri kabuğu yeniden yüklemez; içerik, başlık ve sayfa betiği yerinde değişir. */
export function initSoftNav() {
    let hoverTimer = 0;
    document.addEventListener('pointerover', event => {
        const link = tabLink(event.target);
        if (!link) return;
        const href = link.href;
        clearTimeout(hoverTimer);
        hoverTimer = window.setTimeout(() => prefetch(new URL(href, window.location.origin)), 150);
    }, true);

    document.addEventListener('pointerdown', event => {
        const link = tabLink(event.target);
        if (!link || event.button !== 0) return;
        prefetch(new URL(link.href, window.location.origin));
    }, true);

    document.addEventListener('click', event => {
        const link = tabLink(event.target);
        if (!link || event.defaultPrevented || event.button !== 0) return;
        if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
        if (link.target && link.target !== '_self') return;

        const url = new URL(link.href, window.location.origin);
        if (!sameDocument(url)) return;
        if (url.pathname === window.location.pathname && url.search === window.location.search) {
            event.preventDefault();
            return;
        }

        event.preventDefault();
        show(url, { push: true });
    });

    window.addEventListener('popstate', () => {
        if (!soft || window.location.pathname === lastPath) return;
        show(new URL(window.location.href), { push: false });
    });
}
