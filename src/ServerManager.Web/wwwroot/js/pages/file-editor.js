import { confirmAction } from '../core/dialog.js';
import { on, qs, qsa, setBusy } from '../core/dom.js';
import { formatBytes } from '../core/format.js';
import { failureMessage, getJson, postForm } from '../core/http.js';
import { notify } from '../core/notify.js';
import { createDownloader } from '../features/files/file-download.js';
import { resolveMode, SELECTABLE_MODES } from '../features/files/editor-modes.js';
import { initServerPage } from '../features/servers/server-page.js';

const CONFLICT = 409;
const WRAP_KEY = 'editor:wrap';

const root = qs('[data-file-editor]');
const canEdit = root.dataset.canEdit === 'true';
const saveButton = qs('[data-editor-save]', root);
const dirtyMark = qs('[data-editor-dirty]', root);
const loading = qs('[data-editor-loading]', root);
const errorBox = qs('[data-editor-error]', root);
const modeSelect = qs('[data-editor-mode]', root);
const wrapToggle = qs('[data-editor-wrap]', root);

initServerPage({ regions: ['server-header'] });
const downloader = createDownloader(qs('[data-file-download-frame]'));

let editor = null;
let file = null;
let cleanGeneration = 0;
let saving = false;

const isDark = () => document.documentElement.classList.contains('dark');
const isDirty = () => editor !== null && !editor.isClean(cleanGeneration);

function updateDirty() {
    const dirty = isDirty();
    dirtyMark.hidden = !dirty;
    if (saveButton) saveButton.disabled = !dirty || saving;
}

function updateStatus() {
    if (!file) return;
    qs('[data-editor-encoding]', root).textContent = file.hasBom ? 'UTF-8 (BOM)' : 'UTF-8';
    qs('[data-editor-line-ending]', root).textContent = file.lineEnding;
    qs('[data-editor-size]', root).textContent = formatBytes(file.size);
}

function updateCursor() {
    const cursor = editor.getCursor();
    qs('[data-editor-cursor]', root).textContent = `Satır ${cursor.line + 1}, Sütun ${cursor.ch + 1}`;
}

function fillModes(selected) {
    const modes = SELECTABLE_MODES.some(mode => mode.mime === selected.mime) ? SELECTABLE_MODES : [selected, ...SELECTABLE_MODES];
    modeSelect.replaceChildren(...modes.map(mode => new Option(mode.label, mode.mime, false, mode.mime === selected.mime)));
}

function showError(message) {
    if (editor) editor.getWrapperElement().hidden = true;
    loading.hidden = true;
    errorBox.hidden = false;
    qs('[data-editor-error-text]', errorBox).textContent = message;
}

function createEditor() {
    const textarea = qs('[data-editor-textarea]', root);
    const instance = window.CodeMirror.fromTextArea(textarea, {
        lineNumbers: true,
        readOnly: !canEdit,
        theme: isDark() ? 'material-darker' : 'default',
        indentUnit: 4,
        tabSize: 4,
        lineWrapping: wrapToggle.checked,
        matchBrackets: true,
        autoCloseBrackets: canEdit,
        styleActiveLine: true,
        extraKeys: {
            'Ctrl-S': () => save(),
            'Cmd-S': () => save(),
            'Alt-G': 'jumpToLine',
            Tab: cm => (cm.somethingSelected() ? cm.indentSelection('add') : cm.replaceSelection(cm.getOption('indentWithTabs') ? '\t' : ' '.repeat(cm.getOption('indentUnit')), 'end'))
        }
    });
    instance.on('changes', updateDirty);
    instance.on('cursorActivity', updateCursor);
    new MutationObserver(() => instance.setOption('theme', isDark() ? 'material-darker' : 'default'))
        .observe(document.documentElement, { attributes: true, attributeFilter: ['class'] });
    return instance;
}

async function load() {
    loading.hidden = false;
    errorBox.hidden = true;
    const response = await getJson(root.dataset.readUrl);
    if (!response.isSuccess) {
        showError(failureMessage(response, 'Dosya okunamadı.'));
        return;
    }

    file = response.data;
    loading.hidden = true;
    editor ??= createEditor();
    const mode = resolveMode(file.path);
    fillModes(mode);
    editor.setOption('mode', mode.mime);
    editor.setOption('indentWithTabs', /\t/.test(file.content.slice(0, 20000)) && !/^ {2,}\S/m.test(file.content.slice(0, 20000)));
    editor.setValue(file.content);
    editor.clearHistory();
    cleanGeneration = editor.changeGeneration(true);
    editor.getWrapperElement().hidden = false;
    editor.refresh();
    editor.focus();
    updateStatus();
    updateCursor();
    updateDirty();
}

function payload(force) {
    return {
        Path: file.path,
        Content: editor.getValue(),
        HasBom: file.hasBom ? 'true' : 'false',
        LineEnding: file.lineEnding,
        Version: file.version,
        Force: force ? 'true' : 'false'
    };
}

function applySaved(response, generation) {
    file.version = response.data.version;
    file.size = response.data.size;
    cleanGeneration = generation;
    updateStatus();
    updateDirty();
    notify.success(response.message || 'Dosya kaydedildi.');
}

async function save() {
    if (!canEdit || !editor || saving || !isDirty()) return;
    saving = true;
    setBusy(saveButton, true, 'Kaydediliyor…');
    const generation = editor.changeGeneration(true);
    try {
        const response = await postForm(root.dataset.saveUrl, payload(false));
        if (response.isSuccess) {
            applySaved(response, generation);
            return;
        }
        if (response.status !== CONFLICT) {
            notify.error(failureMessage(response, 'Dosya kaydedilemedi.'));
            return;
        }
        const forced = await confirmAction({
            title: 'Dosya değişmiş',
            message: `${response.message} Sunucudaki değişiklikler kaybolur.`,
            confirmText: 'Üzerine yaz',
            action: () => postForm(root.dataset.saveUrl, payload(true))
        });
        if (forced) applySaved(forced, generation);
    } finally {
        saving = false;
        setBusy(saveButton, false);
        updateDirty();
    }
}

saveButton?.addEventListener('click', () => save());

modeSelect.addEventListener('change', () => editor?.setOption('mode', modeSelect.value));

wrapToggle.checked = localStorage.getItem(WRAP_KEY) === '1';
wrapToggle.addEventListener('change', () => {
    localStorage.setItem(WRAP_KEY, wrapToggle.checked ? '1' : '0');
    editor?.setOption('lineWrapping', wrapToggle.checked);
});

on(root, 'click', '[data-editor-search]', () => editor?.execCommand('find'));
on(root, 'click', '[data-editor-download]', () => downloader.download(root.dataset.downloadUrl));
on(root, 'click', '[data-editor-reload]', async () => {
    if (isDirty()) {
        const confirmed = await confirmAction({
            title: 'Değişiklikleri at',
            message: 'Kaydedilmemiş değişiklikler kaybolur ve dosya sunucudaki haliyle yeniden yüklenir.',
            confirmText: 'Yeniden yükle'
        });
        if (!confirmed) return;
    }
    await load();
});

window.addEventListener('beforeunload', event => {
    if (!isDirty()) return;
    event.preventDefault();
    event.returnValue = '';
});

qsa('a[href]', document).forEach(link => {
    link.addEventListener('click', async event => {
        if (!isDirty() || link.target === '_blank' || event.metaKey || event.ctrlKey) return;
        event.preventDefault();
        const confirmed = await confirmAction({
            title: 'Kaydedilmemiş değişiklikler',
            message: 'Sayfadan ayrılırsanız kaydedilmemiş değişiklikler kaybolur.',
            confirmText: 'Ayrıl'
        });
        if (!confirmed) return;
        cleanGeneration = editor.changeGeneration(true);
        window.location.href = link.href;
    });
});

load();
