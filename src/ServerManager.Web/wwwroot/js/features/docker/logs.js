import { element, qs } from '../../core/dom.js';
import { failureMessage, getJson } from '../../core/http.js';
import { formatClock, formatTimestamp } from '../../core/format.js';
import { onPageDispose } from '../../core/page-scope.js';

const FOLLOW_INTERVAL_MS = 3000;
const MAX_LINES = 5000;

const lineKey = line => `${line.rawTimestamp ?? ''}|${line.isError ? 1 : 0}|${line.text}`;

/**
 * Container log görüntüleyicisi: son N satır, arama, stderr filtresi, canlı takip ve indirme.
 * Canlı takipte yalnızca son zaman damgasından sonraki satırlar istenir; aynı damgalı tekrarlar ayıklanır.
 */
export function createLogViewer(root) {
    if (!root) return null;

    const output = qs('[data-logs-output]', root);
    const tailSelect = qs('[data-logs-tail]', root);
    const search = qs('[data-logs-search]', root);
    const errorsOnly = qs('[data-logs-errors]', root);
    const wrap = qs('[data-logs-wrap]', root);
    const follow = qs('[data-logs-follow]', root);
    const status = qs('[data-logs-status]', root);
    const baseUrl = root.dataset.url;

    let lines = [];
    let lastRaw = null;
    let seenAtLast = new Set();
    let followTimer = null;
    let loading = false;
    let started = false;

    const setStatus = text => { status.textContent = text; };

    function remember(batch) {
        batch.forEach(line => {
            if (!line.rawTimestamp) return;
            if (line.rawTimestamp !== lastRaw) {
                lastRaw = line.rawTimestamp;
                seenAtLast = new Set();
            }
            seenAtLast.add(lineKey(line));
        });
    }

    function matches(line) {
        if (errorsOnly.checked && !line.isError) return false;
        const query = search.value.trim().toLowerCase();
        return !query || line.text.toLowerCase().includes(query);
    }

    function lineElement(line) {
        const row = element('div', 'log-line');
        row.appendChild(element('span', 'log-time', formatTimestamp(line.timestamp)));
        const text = element('span', `log-text${wrap.checked ? ' is-wrapped' : ''}${line.isError ? ' is-error' : ''}`, line.text);
        row.appendChild(text);
        return row;
    }

    const isNearBottom = () => output.scrollHeight - output.scrollTop - output.clientHeight < 40;

    function render() {
        const fragment = document.createDocumentFragment();
        let shown = 0;
        lines.forEach(line => {
            if (!matches(line)) return;
            fragment.appendChild(lineElement(line));
            shown++;
        });
        if (shown === 0) {
            fragment.appendChild(element('p', 'log-empty', lines.length === 0 ? 'Bu container için log yok.' : 'Filtreyle eşleşen satır yok.'));
        }
        output.replaceChildren(fragment);
        output.scrollTop = output.scrollHeight;
    }

    function append(batch) {
        const stick = isNearBottom();
        if (lines.length === 0) output.textContent = '';
        batch.forEach(line => {
            lines.push(line);
            if (matches(line)) output.appendChild(lineElement(line));
        });
        if (lines.length > MAX_LINES) {
            lines = lines.slice(lines.length - MAX_LINES);
            render();
            return;
        }
        if (stick) output.scrollTop = output.scrollHeight;
    }

    async function load() {
        if (loading) return;
        loading = true;
        setStatus('Yükleniyor…');
        try {
            const response = await getJson(`${baseUrl}&tail=${encodeURIComponent(tailSelect.value)}`);
            if (!response.isSuccess) {
                output.replaceChildren(element('p', 'log-error', failureMessage(response, 'Loglar alınamadı.')));
                setStatus('');
                return;
            }
            lines = response.data.lines ?? [];
            lastRaw = null;
            seenAtLast = new Set();
            remember(lines);
            render();
            const truncated = response.data.truncated ? ` (son ${tailSelect.value})` : '';
            setStatus(`${lines.length} satır${truncated} · ${formatClock()}`);
        } finally {
            loading = false;
        }
    }

    async function poll() {
        if (loading || document.hidden) return;
        if (!lastRaw) {
            await load();
            return;
        }
        loading = true;
        try {
            const response = await getJson(`${baseUrl}&tail=${MAX_LINES}&since=${encodeURIComponent(lastRaw)}`);
            if (!response.isSuccess) {
                setStatus(failureMessage(response, 'Loglar alınamadı.'));
                return;
            }
            const fresh = (response.data.lines ?? []).filter(line => !seenAtLast.has(lineKey(line)));
            remember(fresh);
            if (fresh.length) append(fresh);
            setStatus(`Canlı · ${lines.length} satır · ${formatClock()}`);
        } finally {
            loading = false;
        }
    }

    function setFollow(enabled) {
        clearInterval(followTimer);
        followTimer = enabled ? setInterval(poll, FOLLOW_INTERVAL_MS) : null;
    }

    function download() {
        const text = lines.map(line => `${line.rawTimestamp ?? ''}${line.isError ? ' [stderr] ' : ' '}${line.text}`).join('\n');
        const link = element('a');
        link.href = URL.createObjectURL(new Blob([`${text}\n`], { type: 'text/plain;charset=utf-8' }));
        link.download = `${root.dataset.container}-${new Date().toISOString().replace(/[:.]/g, '-')}.log`;
        document.body.appendChild(link);
        link.click();
        setTimeout(() => {
            URL.revokeObjectURL(link.href);
            link.remove();
        }, 0);
    }

    tailSelect.addEventListener('change', load);
    search.addEventListener('input', render);
    errorsOnly.addEventListener('change', render);
    wrap.addEventListener('change', render);
    follow.addEventListener('change', () => setFollow(follow.checked));
    qs('[data-logs-reload]', root).addEventListener('click', load);
    qs('[data-logs-download]', root).addEventListener('click', download);
    onPageDispose(() => clearInterval(followTimer));

    return {
        activate() {
            if (started) return;
            started = true;
            load();
        }
    };
}
