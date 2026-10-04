import { createAjaxList } from '../components/ajax-list.js';
import { bindModals, closeModal, openModal } from '../components/modal.js';
import { confirmAction } from '../core/dialog.js';
import { on, qs } from '../core/dom.js';
import { bindAjaxForm, clearErrors } from '../core/forms.js';
import { postForm } from '../core/http.js';
import { notify } from '../core/notify.js';
import { createDownloader } from '../features/files/file-download.js';
import { createUploader } from '../features/files/file-upload.js';
import { createPermissionsForm } from '../features/files/permissions-form.js';
import { initServerPage } from '../features/servers/server-page.js';

const HIDDEN_FILES_KEY = 'files:show-hidden';

const root = qs('[data-file-manager]');
const listRoot = qs('[data-file-list]', root);
const pathForm = qs('[data-file-path-form]', root);
const pathInput = qs('[data-file-path-input]', root);
const hiddenToggle = qs('[data-file-show-hidden]', root);

initServerPage({ regions: ['server-header'] });
bindModals();

const listing = () => qs('[data-file-listing]', listRoot);
const currentPath = () => listing()?.dataset.currentPath ?? '';
const listUrl = path => (path ? `${root.dataset.listUrl}?path=${encodeURIComponent(path)}` : root.dataset.listUrl);
const parentOf = path => path.replace(/\/[^/]+\/?$/, '') || '/';
const joinPath = (directory, name) => (directory === '/' ? `/${name}` : `${directory}/${name}`);

function entryOf(element) {
    const row = element.closest('[data-entry]');
    if (!row) return null;
    const { path, name, kind, mode, owner, group } = row.dataset;
    return { path, name, kind, mode, owner, group, isDirectory: kind === 'directory' };
}

function syncPathInput() {
    const path = currentPath();
    if (path) pathInput.value = path;
}

const list = createAjaxList(listRoot, { form: pathForm, onLoaded: syncPathInput });
const reload = () => list.reload();
list.load(listUrl(pathInput.value.trim()), { push: false });

function applyHiddenPreference() {
    root.classList.toggle('show-hidden', hiddenToggle.checked);
}
hiddenToggle.checked = localStorage.getItem(HIDDEN_FILES_KEY) === '1';
applyHiddenPreference();
hiddenToggle.addEventListener('change', () => {
    localStorage.setItem(HIDDEN_FILES_KEY, hiddenToggle.checked ? '1' : '0');
    applyHiddenPreference();
});

on(root, 'click', '[data-file-up]', () => {
    const parent = listing()?.dataset.parentPath;
    if (parent) list.load(listUrl(parent));
});
on(root, 'click', '[data-file-home]', () => list.load(listUrl(listing()?.dataset.homePath ?? '')));
on(root, 'click', '[data-file-refresh]', () => reload());

// Oluşturma (klasör / dosya)
const createModal = qs('#file-create-modal');
const createForm = qs('[data-file-create-form]');
on(root, 'click', '[data-file-create]', (event, button) => {
    const isDirectory = button.dataset.fileCreate === 'directory';
    const directory = currentPath();
    if (!directory) {
        notify.error('Önce bir klasör açın.');
        return;
    }
    clearErrors(createForm);
    createForm.reset();
    createForm.elements.Directory.value = directory;
    createForm.elements.IsDirectory.value = isDirectory ? 'true' : 'false';
    qs('[data-file-create-title]', createForm).textContent = isDirectory ? 'Yeni klasör' : 'Yeni dosya';
    qs('[data-file-create-location]', createForm).textContent = directory;
    openModal(createModal);
});
bindAjaxForm(createForm, {
    onSuccess: async response => {
        closeModal(createModal);
        notify.success(response.message);
        await reload();
    }
});

// Yeniden adlandırma / taşıma ve kopyalama
function openPathModal(modal, form, entry, destination) {
    clearErrors(form);
    form.elements.Source.value = entry.path;
    form.elements.Destination.value = destination;
    qs('[data-file-source]', form).textContent = entry.path;
    openModal(modal);
    const input = form.elements.Destination;
    const nameStart = destination.lastIndexOf('/') + 1;
    const dot = entry.isDirectory ? -1 : destination.lastIndexOf('.');
    input.setSelectionRange(nameStart, dot > nameStart ? dot : destination.length);
}

function copyName(entry) {
    const dot = entry.isDirectory ? -1 : entry.name.lastIndexOf('.');
    const name = dot > 0 ? `${entry.name.slice(0, dot)}-kopya${entry.name.slice(dot)}` : `${entry.name}-kopya`;
    return joinPath(parentOf(entry.path), name);
}

const moveModal = qs('#file-move-modal');
const moveForm = qs('[data-file-move-form]');
on(listRoot, 'click', '[data-file-rename]', (event, button) => {
    const entry = entryOf(button);
    if (entry) openPathModal(moveModal, moveForm, entry, entry.path);
});
bindAjaxForm(moveForm, {
    onSuccess: async response => {
        closeModal(moveModal);
        notify.success(response.message);
        await reload();
    }
});

const copyModal = qs('#file-copy-modal');
const copyForm = qs('[data-file-copy-form]');
on(listRoot, 'click', '[data-file-copy]', (event, button) => {
    const entry = entryOf(button);
    if (entry) openPathModal(copyModal, copyForm, entry, copyName(entry));
});
bindAjaxForm(copyForm, {
    onSuccess: async response => {
        closeModal(copyModal);
        notify.success(response.message);
        await reload();
    }
});

// İzinler
const permissionsModal = qs('#file-permissions-modal');
const permissions = createPermissionsForm(qs('[data-file-permissions-form]'), {
    onSuccess: async response => {
        closeModal(permissionsModal);
        notify.success(response.message);
        await reload();
    }
});
on(listRoot, 'click', '[data-file-permissions]', (event, button) => {
    const entry = entryOf(button);
    if (!entry || !permissions) return;
    permissions.load(entry);
    qs('[data-file-source]', permissionsModal).textContent = entry.path;
    openModal(permissionsModal);
});

// Silme: dosyada basit onay, klasörde adı yazarak onay
on(listRoot, 'click', '[data-file-delete]', async (event, button) => {
    const entry = entryOf(button);
    if (!entry) return;
    const response = await confirmAction({
        title: entry.isDirectory ? 'Klasörü sil' : 'Dosyayı sil',
        message: entry.isDirectory
            ? `${entry.path} klasörü içindeki tüm dosya ve klasörlerle birlikte kalıcı olarak silinir. Bu işlem geri alınamaz.`
            : `${entry.path} kalıcı olarak silinir. Bu işlem geri alınamaz.`,
        confirmText: 'Sil',
        expected: entry.isDirectory ? entry.name : undefined,
        action: ({ confirmation }) => postForm(root.dataset.deleteUrl, { Path: entry.path, ConfirmationName: confirmation })
    });
    if (!response) return;
    notify.success(response.message);
    await reload();
});

// İndirme
const downloader = createDownloader(qs('[data-file-download-frame]'));
on(listRoot, 'click', '[data-file-download]', (event, button) => {
    const entry = entryOf(button);
    if (entry) downloader.download(`${root.dataset.downloadUrl}?path=${encodeURIComponent(entry.path)}`);
});

// Yükleme: düğme ve sürükle-bırak
const uploadInput = qs('[data-file-upload-input]', root);
if (uploadInput) {
    const uploader = createUploader({
        url: root.dataset.uploadUrl,
        queue: qs('[data-upload-queue]', root),
        maxBytes: Number(root.dataset.maxUploadMb) * 1024 * 1024,
        getDirectory: currentPath,
        onUploaded: reload
    });

    on(root, 'click', '[data-file-upload]', () => uploadInput.click());
    uploadInput.addEventListener('change', () => {
        if (uploadInput.files?.length) uploader.add([...uploadInput.files]);
        uploadInput.value = '';
    });

    const dropZone = qs('[data-file-drop-zone]', root);
    const overlay = qs('[data-file-drop-overlay]', root);
    let dragDepth = 0;
    const hasFiles = event => [...(event.dataTransfer?.types ?? [])].includes('Files');
    dropZone.addEventListener('dragenter', event => {
        if (!hasFiles(event)) return;
        event.preventDefault();
        dragDepth++;
        overlay.hidden = false;
    });
    dropZone.addEventListener('dragover', event => {
        if (hasFiles(event)) event.preventDefault();
    });
    dropZone.addEventListener('dragleave', () => {
        dragDepth = Math.max(0, dragDepth - 1);
        if (!dragDepth) overlay.hidden = true;
    });
    dropZone.addEventListener('drop', event => {
        if (!hasFiles(event)) return;
        event.preventDefault();
        dragDepth = 0;
        overlay.hidden = true;
        const items = [...(event.dataTransfer?.items ?? [])].filter(item => item.kind === 'file');
        const files = items.filter(item => !item.webkitGetAsEntry?.()?.isDirectory).map(item => item.getAsFile()).filter(Boolean);
        if (files.length < items.length) notify.warning('Klasör yükleme desteklenmiyor; klasörler atlandı.');
        if (files.length) uploader.add(files);
    });
}
