/**
 * Sayfa betiklerinin ömrü. Kabuk (menü, tema, oturum) kapsam açılmadan bağlanır ve sekme
 * geçişinde durur. Kapsam açıldıktan sonra bağlanan dinleyiciler disposePage ile kalkar.
 */
let pageController = null;

export function pageSignal() {
    return pageController?.signal ?? null;
}

/** site.js kabuğu kurduktan sonra çağrılır; bundan sonraki dinleyiciler sayfaya aittir. */
export function activatePageScope() {
    if (!pageController) pageController = new AbortController();
}

export function onPageDispose(fn) {
    const signal = pageSignal();
    if (!signal || signal.aborted) return;
    signal.addEventListener('abort', () => {
        try {
            fn();
        } catch {
            // Bir sayfanın kapanışı diğerini bekletmez.
        }
    }, { once: true });
}

/** Açık sayfanın dinleyici, zamanlayıcı ve bağlantılarını keser; yeni sayfa için temiz kapsam açar. */
export function disposePage() {
    const previous = pageController;
    pageController = new AbortController();
    previous?.abort();
}
