import { debounce, qs, qsa } from '../core/dom.js';
import { onPageDispose, pageSignal } from '../core/page-scope.js';

/**
 * Kullanım kılavuzu: içindekilerde okunan bölümü işaretler (scroll-spy), aramayla bölümleri süzer ve
 * eşleşmeleri vurgular. Arama yalnızca tarayıcıda çalışır; sunucuya istek gitmez.
 */
const root = qs('[data-guide]');

if (root) {
    const signal = pageSignal();
    const listen = signal ? { signal } : undefined;
    const content = qs('[data-guide-content]', root);
    const search = qs('[data-guide-search]', root);
    const status = qs('[data-guide-search-status]', root);
    const empty = qs('[data-guide-empty]', root);
    const sections = qsa('[data-guide-section]', content);
    const intros = qsa('[data-guide-intro]', content);
    const tocLinks = new Map(qsa('[data-toc-link]', root).map(link => [link.dataset.tocLink, link]));
    const tocItems = qsa('[data-toc-section]', root);
    const originals = new Map();
    const locale = 'tr';

    const normalize = text => text.toLocaleLowerCase(locale);

    // --- Scroll-spy -------------------------------------------------------------------------------------------
    const headings = qsa('h1[id], h2[id], h3[id]', content).filter(heading => tocLinks.has(heading.id));
    let activeLink = null;

    function setActive(id) {
        const link = tocLinks.get(id);
        if (!link || link === activeLink) return;
        activeLink?.classList.remove('is-active');
        activeLink = link;
        link.classList.add('is-active');
        const toc = link.closest('[data-guide-toc]');
        if (toc && toc.scrollHeight > toc.clientHeight) {
            const top = link.offsetTop - toc.offsetTop;
            if (top < toc.scrollTop || top > toc.scrollTop + toc.clientHeight - 32) toc.scrollTop = top - toc.clientHeight / 3;
        }
    }

    function updateActive() {
        const offset = 120;
        let current = null;
        for (const heading of headings) {
            if (heading.closest('[hidden]')) continue;
            if (heading.getBoundingClientRect().top - offset <= 0) current = heading;
            else break;
        }
        const first = headings.find(heading => !heading.closest('[hidden]'));
        if (current ?? first) setActive((current ?? first).id);
    }

    let frame = 0;
    const onScroll = () => {
        if (frame) return;
        frame = requestAnimationFrame(() => {
            frame = 0;
            updateActive();
        });
    };
    window.addEventListener('scroll', onScroll, { passive: true, ...listen });
    onPageDispose(() => cancelAnimationFrame(frame));

    // --- Arama ------------------------------------------------------------------------------------------------
    function restore(element) {
        const html = originals.get(element);
        if (html !== undefined) element.innerHTML = html;
    }

    function highlight(element, terms) {
        if (!originals.has(element)) originals.set(element, element.innerHTML);
        const walker = document.createTreeWalker(element, NodeFilter.SHOW_TEXT);
        const nodes = [];
        while (walker.nextNode()) nodes.push(walker.currentNode);
        for (const node of nodes) {
            const text = node.nodeValue;
            const lower = normalize(text);
            const ranges = [];
            for (const term of terms) {
                let index = lower.indexOf(term);
                while (index >= 0) {
                    ranges.push([index, index + term.length]);
                    index = lower.indexOf(term, index + term.length);
                }
            }
            if (ranges.length === 0 || lower.length !== text.length) continue;
            ranges.sort((a, b) => a[0] - b[0]);
            const fragment = document.createDocumentFragment();
            let cursor = 0;
            for (const [start, end] of ranges) {
                if (start < cursor) continue;
                fragment.append(text.slice(cursor, start));
                const mark = document.createElement('mark');
                mark.textContent = text.slice(start, end);
                fragment.append(mark);
                cursor = end;
            }
            fragment.append(text.slice(cursor));
            node.replaceWith(fragment);
        }
    }

    function applySearch() {
        const query = normalize(search.value.trim());
        const terms = query.split(/\s+/).filter(term => term.length >= 2);

        for (const element of [...sections, ...intros]) restore(element);

        if (terms.length === 0) {
            sections.forEach(section => { section.hidden = false; });
            intros.forEach(intro => { intro.hidden = false; });
            tocItems.forEach(item => { item.hidden = false; });
            status.hidden = true;
            empty.hidden = true;
            updateActive();
            return;
        }

        let matches = 0;
        for (const section of sections) {
            const text = normalize(section.textContent);
            const match = terms.every(term => text.includes(term));
            section.hidden = !match;
            const item = tocItems.find(entry => entry.dataset.tocSection === section.dataset.guideSection);
            if (item) item.hidden = !match;
            if (match) {
                matches += 1;
                highlight(section, terms);
            }
        }
        intros.forEach(intro => { intro.hidden = true; });

        status.hidden = false;
        status.textContent = matches === 0 ? 'Eşleşen bölüm yok.' : `${matches} bölüm eşleşti.`;
        empty.hidden = matches > 0;
        updateActive();
    }

    search?.addEventListener('input', debounce(applySearch, 150), listen);
    search?.addEventListener('keydown', event => {
        if (event.key === 'Escape') {
            search.value = '';
            applySearch();
        }
    }, listen);

    // Arama süzgeci açıkken gizli bir bölüme bağlantı tıklanırsa süzgeç temizlenir.
    function reveal(id) {
        const target = id ? document.getElementById(id) : null;
        if (target && target.closest('[hidden]') && search) {
            search.value = '';
            applySearch();
        }
        return target;
    }

    root.addEventListener('click', event => {
        const link = event.target instanceof Element ? event.target.closest('a[href^="#"]') : null;
        if (!link) return;
        const id = decodeURIComponent(link.getAttribute('href').slice(1));
        const target = reveal(id);
        if (!target) return;
        event.preventDefault();
        history.replaceState(null, '', `#${encodeURIComponent(id)}`);
        target.scrollIntoView({ behavior: 'smooth', block: 'start' });
        setActive(id);
    }, listen);

    qs('[data-guide-print]')?.addEventListener('click', () => window.print(), listen);

    // Yardım bağlantısıyla (#bolum) gelindiyse bölüme git.
    const initial = decodeURIComponent(window.location.hash.slice(1));
    const initialTarget = initial ? reveal(initial) : null;
    if (initialTarget) requestAnimationFrame(() => initialTarget.scrollIntoView({ block: 'start' }));
    updateActive();
}
