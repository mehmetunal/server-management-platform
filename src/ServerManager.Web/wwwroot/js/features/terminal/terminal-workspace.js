import { confirmAction } from '../../core/dialog.js';
import { debounce, element, on, qs } from '../../core/dom.js';
import { failureMessage, getJson } from '../../core/http.js';
import { notify } from '../../core/notify.js';
import { createTerminalHub } from './terminal-hub.js';
import { createTerminalSession } from './terminal-session.js';
import { renderStatus } from './terminal-status.js';
import { createTerminalView } from './terminal-view.js';

const MAX_HISTORY_ITEMS = 200;
const SEARCH_DELAY_MS = 200;
const HISTORY_PANEL_KEY = 'terminal:history-panel';
const RECONNECTABLE_STATES = new Set(['disconnected', 'error', 'closed']);

const nextFrame = () => new Promise(resolve => requestAnimationFrame(() => resolve()));
const pad = value => String(value).padStart(2, '0');

function fileStamp(date = new Date()) {
    return `${date.getFullYear()}${pad(date.getMonth() + 1)}${pad(date.getDate())}-${pad(date.getHours())}${pad(date.getMinutes())}${pad(date.getSeconds())}`;
}

function downloadText(text, fileName) {
    const url = URL.createObjectURL(new Blob([text], { type: 'text/plain;charset=utf-8' }));
    const link = element('a');
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}

/**
 * Sunucu terminal sayfası: sekmeli çoklu oturum, arama, pano, çıktı indirme, tam ekran ve komut geçmişi.
 * Açık oturumların kimlikleri sessionStorage'da tutulur; sayfa yenilenince aynı oturumlara yeniden bağlanılır.
 */
export function createTerminalWorkspace(root) {
    if (!root || !window.Terminal || !window.signalR) return null;

    const serverId = root.dataset.serverId;
    const storageKey = `terminal:server:${serverId}`;
    const hub = createTerminalHub(root.dataset.hubUrl);

    const tabList = qs('[data-terminal-tabs]', root);
    const screens = qs('[data-terminal-screens]', root);
    const placeholder = qs('[data-terminal-placeholder]', root);
    const statusBadge = qs('[data-terminal-status]', root);
    const reconnectButton = qs('[data-terminal-reconnect]', root);
    const takeoverButton = qs('[data-terminal-takeover]', root);
    const searchInput = qs('[data-terminal-search]', root);
    const historyPanel = qs('[data-terminal-history-panel]', root);
    const historyList = qs('[data-terminal-history-list]', root);
    const historyFilter = qs('[data-terminal-history-filter]', root);
    const historyToggle = qs('[data-terminal-history-toggle]', root);

    const tabs = [];
    let active = null;
    let counter = 0;
    let history = [];
    let historyLoaded = false;

    function persist() {
        const saved = tabs.filter(tab => tab.session.id).map(tab => ({ id: tab.session.id, number: tab.number }));
        if (saved.length) sessionStorage.setItem(storageKey, JSON.stringify(saved));
        else sessionStorage.removeItem(storageKey);
    }

    function savedTabs() {
        try {
            const value = JSON.parse(sessionStorage.getItem(storageKey) ?? '[]');
            return Array.isArray(value) ? value.filter(item => typeof item?.id === 'string') : [];
        } catch {
            return [];
        }
    }

    function renderToolbar() {
        renderStatus(statusBadge, active?.state ?? 'idle', active?.statusText);
        reconnectButton.hidden = !active || !RECONNECTABLE_STATES.has(active.state);
        reconnectButton.lastChild.textContent = active?.state === 'closed' ? ' Yeni oturum başlat' : ' Yeniden bağlan';
        takeoverButton.hidden = active?.state !== 'detached';
    }

    function updatePlaceholder() {
        placeholder.hidden = tabs.length > 0;
    }

    function activate(tab) {
        active = tab;
        for (const item of tabs) {
            const selected = item === tab;
            item.button.classList.toggle('is-active', selected);
            item.button.setAttribute('aria-selected', selected ? 'true' : 'false');
            item.screen.hidden = !selected;
            if (!selected) item.view.clearSearch();
        }
        searchInput.classList.remove('is-not-found');
        renderToolbar();
        requestAnimationFrame(() => {
            tab.view.fit();
            tab.view.focus();
        });
    }

    function createTab({ sessionId = null, number } = {}) {
        counter = Math.max(counter, number ?? counter + 1);
        const tab = { number: number ?? counter, state: 'idle', statusText: '' };

        tab.button = element('button', 'terminal-tab');
        tab.button.type = 'button';
        tab.button.setAttribute('role', 'tab');
        tab.button.title = 'Bu oturuma geç';
        const close = element('span', 'terminal-tab-close', '×');
        close.setAttribute('role', 'button');
        close.setAttribute('aria-label', 'Oturumu kapat');
        close.title = 'Oturumu sonlandır ve sekmeyi kapat';
        tab.button.append(element('span', 'terminal-tab-dot'), element('span', 'terminal-tab-label', `Terminal ${tab.number}`), close);
        tabList.appendChild(tab.button);

        tab.screen = element('div', 'terminal-screen');
        tab.screen.hidden = true;
        screens.appendChild(tab.screen);

        tab.view = createTerminalView(tab.screen, {
            onData: data => tab.session?.input(data),
            onResize: (columns, rows) => tab.session?.resize(columns, rows)
        });
        tab.session = createTerminalSession({
            hub,
            view: tab.view,
            sessionId,
            start: (columns, rows) => hub.invoke('StartServer', serverId, columns, rows),
            onSession: persist,
            onStatus: (state, text) => {
                tab.state = state;
                tab.statusText = text;
                tab.button.dataset.state = state;
                if (tab === active) renderToolbar();
            },
            onCommand: addHistory
        });

        tab.button.addEventListener('click', event => {
            if (event.target instanceof Element && event.target.closest('.terminal-tab-close')) closeTab(tab);
            else activate(tab);
        });

        tabs.push(tab);
        updatePlaceholder();
        return tab;
    }

    function removeTab(tab) {
        const index = tabs.indexOf(tab);
        if (index < 0) return;
        tab.session.dispose();
        tab.view.dispose();
        tab.button.remove();
        tab.screen.remove();
        tabs.splice(index, 1);
        persist();
        updatePlaceholder();
        if (active === tab) {
            active = null;
            const next = tabs[index] ?? tabs[index - 1];
            if (next) activate(next);
            else renderToolbar();
        }
    }

    async function closeTab(tab) {
        if (tab.session.id && tab.state === 'connected') {
            const confirmed = await confirmAction({
                title: 'Oturumu kapat',
                message: `Terminal ${tab.number} oturumu sonlandırılır; içinde çalışan komutlar durur.`,
                confirmText: 'Kapat',
                danger: false
            });
            if (!confirmed) return;
        }
        await tab.session.stop();
        removeTab(tab);
    }

    async function openNew() {
        const tab = createTab();
        activate(tab);
        await nextFrame();
        tab.view.fit();
        await tab.session.open();
    }

    async function restore() {
        const saved = savedTabs();
        if (!saved.length) {
            await openNew();
            return;
        }

        const restored = saved.map(item => createTab({ sessionId: item.id, number: Number(item.number) || undefined }));
        activate(restored[0]);
        await nextFrame();
        for (const tab of restored) {
            tab.view.fit();
            await tab.session.open({ restoreOnly: true });
            if (!tab.session.id) removeTab(tab);
        }
        if (!tabs.length) await openNew();
    }

    // Komut geçmişi
    function renderHistory() {
        const filter = historyFilter.value.trim().toLowerCase();
        const items = filter ? history.filter(command => command.toLowerCase().includes(filter)) : history;
        historyList.replaceChildren();
        if (!items.length) {
            historyList.appendChild(element('li', 'terminal-history-empty', history.length ? 'Eşleşen komut yok.' : 'Henüz komut yok.'));
            return;
        }
        for (const command of items) {
            const item = element('li');
            const button = element('button', 'terminal-history-item', command);
            button.type = 'button';
            button.title = `Terminale yaz: ${command}`;
            button.dataset.command = command;
            item.appendChild(button);
            historyList.appendChild(item);
        }
    }

    function addHistory(command) {
        if (!command) return;
        history = [command, ...history.filter(item => item !== command)].slice(0, MAX_HISTORY_ITEMS);
        if (!historyPanel.hidden) renderHistory();
    }

    async function loadHistory() {
        const response = await getJson(root.dataset.historyUrl);
        if (!response.isSuccess) {
            notify.error(failureMessage(response, 'Komut geçmişi alınamadı.'));
            return;
        }
        const loaded = Array.isArray(response.data) ? response.data : [];
        history = [...new Set([...history, ...loaded])].slice(0, MAX_HISTORY_ITEMS);
        historyLoaded = true;
        renderHistory();
    }

    function setHistoryPanel(open) {
        historyPanel.hidden = !open;
        historyToggle.setAttribute('aria-pressed', open ? 'true' : 'false');
        historyToggle.classList.toggle('is-active', open);
        localStorage.setItem(HISTORY_PANEL_KEY, open ? '1' : '0');
        if (open && !historyLoaded) loadHistory();
        else if (open) renderHistory();
        requestAnimationFrame(() => active?.view.fit());
    }

    // Arama
    function search(backwards = false) {
        const query = searchInput.value;
        if (!active || !query) {
            active?.view.clearSearch();
            searchInput.classList.remove('is-not-found');
            return;
        }
        const found = backwards ? active.view.findPrevious(query) : active.view.findNext(query);
        searchInput.classList.toggle('is-not-found', !found);
    }

    searchInput.addEventListener('input', debounce(() => search(), SEARCH_DELAY_MS));
    searchInput.addEventListener('keydown', event => {
        if (event.key === 'Enter') {
            event.preventDefault();
            search(event.shiftKey);
        } else if (event.key === 'Escape') {
            searchInput.value = '';
            search();
            active?.view.focus();
        }
    });

    on(root, 'click', '[data-terminal-new]', () => openNew());
    on(root, 'click', '[data-terminal-search-next]', () => search());
    on(root, 'click', '[data-terminal-search-prev]', () => search(true));
    reconnectButton.addEventListener('click', () => active?.session.reconnect());
    takeoverButton.addEventListener('click', () => active?.session.reconnect());

    on(root, 'click', '[data-terminal-copy]', async () => {
        if (!active) return;
        try {
            if (await active.view.copy()) notify.success('Seçim panoya kopyalandı.');
            else notify.info('Önce terminalde kopyalanacak metni seçin.');
        } catch {
            notify.error('Panoya kopyalanamadı.');
        }
        active.view.focus();
    });

    on(root, 'click', '[data-terminal-paste]', async () => {
        if (!active) return;
        try {
            await active.view.paste();
        } catch (error) {
            notify.warning(error?.message || 'Panodan okunamadı.');
        }
        active.view.focus();
    });

    on(root, 'click', '[data-terminal-download]', () => {
        if (!active) return;
        const serverName = (root.dataset.serverName || 'sunucu').replace(/[^\w.-]+/g, '_');
        downloadText(active.view.exportText(), `${serverName}-terminal-${active.number}-${fileStamp()}.txt`);
    });

    on(root, 'click', '[data-terminal-fullscreen]', async () => {
        try {
            if (document.fullscreenElement) await document.exitFullscreen();
            else await root.requestFullscreen();
        } catch {
            notify.warning('Tarayıcı tam ekran moduna izin vermedi.');
        }
    });
    document.addEventListener('fullscreenchange', () => {
        root.classList.toggle('is-fullscreen', document.fullscreenElement === root);
        requestAnimationFrame(() => {
            active?.view.fit();
            active?.view.focus();
        });
    });

    historyToggle.addEventListener('click', () => setHistoryPanel(historyPanel.hidden));
    on(root, 'click', '[data-terminal-history-refresh]', () => loadHistory());
    historyFilter.addEventListener('input', debounce(renderHistory, SEARCH_DELAY_MS));
    on(historyList, 'click', '[data-command]', (event, button) => {
        if (!active || active.state !== 'connected') {
            notify.info('Komutu yazmak için bağlı bir oturum gerekli.');
            return;
        }
        // Çok satırlı kayıt tek satıra indirilir; komut Enter'a basılmadan çalışmaz.
        active.session.input(button.dataset.command.replace(/[\r\n]+/g, ' '));
        active.view.focus();
    });

    if (localStorage.getItem(HISTORY_PANEL_KEY) === '1') setHistoryPanel(true);
    renderToolbar();
    restore();

    return { openNew };
}
