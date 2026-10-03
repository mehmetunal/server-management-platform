import { confirmAction } from '../../core/dialog.js';
import { element } from '../../core/dom.js';
import { formatBytes } from '../../core/format.js';
import { failureMessage, uploadForm } from '../../core/http.js';
import { notify } from '../../core/notify.js';

const CONFLICT = 409;
const DONE_ITEM_DELAY_MS = 4000;

/**
 * Sıralı dosya yükleme kuyruğu (ilerleme çubuğu, iptal, aynı adlı dosyada üzerine yazma onayı).
 * getDirectory() yükleme anındaki hedef klasörü döner; onUploaded() her başarılı yüklemeden sonra çağrılır.
 */
export function createUploader({ url, queue, maxBytes, getDirectory, onUploaded }) {
    const pending = [];
    let running = false;

    function createItem(file) {
        const item = element('li', 'upload-item');
        const header = element('div', 'upload-item-header');
        const name = element('span', 'upload-item-name', file.name);
        const status = element('span', 'upload-item-status', formatBytes(file.size));
        const cancel = element('button', 'upload-item-cancel', '×');
        cancel.type = 'button';
        cancel.title = 'İptal';
        cancel.setAttribute('aria-label', `${file.name} yüklemesini iptal et`);
        header.append(name, status, cancel);
        const track = element('div', 'usage-track');
        const bar = element('div', 'usage-bar upload-item-bar');
        bar.style.width = '0%';
        track.appendChild(bar);
        item.append(header, track);
        queue.appendChild(item);
        queue.hidden = false;
        return { item, status, bar, cancel };
    }

    function finishItem(view, state, text) {
        view.item.dataset.state = state;
        view.status.textContent = text;
        view.cancel.hidden = true;
        if (state === 'done') {
            view.bar.style.width = '100%';
            setTimeout(() => {
                view.item.remove();
                if (!queue.children.length) queue.hidden = true;
            }, DONE_ITEM_DELAY_MS);
        }
    }

    async function send(job, overwrite) {
        const data = new FormData();
        data.append('Directory', job.directory);
        data.append('FileName', job.file.name);
        data.append('Overwrite', overwrite ? 'true' : 'false');
        data.append('file', job.file, job.file.name);
        return uploadForm(url, data, {
            signal: job.controller.signal,
            onProgress: (loaded, total) => {
                const percent = Math.round((loaded / total) * 100);
                job.view.bar.style.width = `${percent}%`;
                job.view.status.textContent = `%${percent} · ${formatBytes(job.file.size)}`;
            }
        });
    }

    async function process(job) {
        if (job.controller.signal.aborted) {
            finishItem(job.view, 'cancelled', 'İptal edildi');
            return;
        }
        let response = await send(job, false);
        if (response.status === CONFLICT && !job.controller.signal.aborted) {
            const approved = await confirmAction({
                title: 'Dosya zaten var',
                message: `"${job.file.name}" hedef klasörde zaten var. Üzerine yazılsın mı?`,
                confirmText: 'Üzerine yaz'
            });
            if (!approved) {
                finishItem(job.view, 'cancelled', 'Atlandı');
                return;
            }
            job.view.bar.style.width = '0%';
            response = await send(job, true);
        }

        if (job.controller.signal.aborted) {
            finishItem(job.view, 'cancelled', 'İptal edildi');
            return;
        }
        if (!response.isSuccess) {
            const message = failureMessage(response, 'Yükleme başarısız.');
            finishItem(job.view, 'error', message);
            notify.error(`${job.file.name}: ${message}`);
            return;
        }
        finishItem(job.view, 'done', 'Yüklendi');
        await onUploaded?.(job.file);
    }

    async function drain() {
        if (running) return;
        running = true;
        try {
            while (pending.length) await process(pending.shift());
        } finally {
            running = false;
        }
    }

    return {
        add(files) {
            const directory = getDirectory();
            if (!directory) {
                notify.error('Yükleme için önce bir klasör açın.');
                return;
            }
            for (const file of files) {
                const view = createItem(file);
                if (file.size > maxBytes) {
                    finishItem(view, 'error', `En fazla ${formatBytes(maxBytes)} yüklenebilir.`);
                    continue;
                }
                const job = { file, directory, view, controller: new AbortController() };
                view.cancel.addEventListener('click', () => {
                    job.controller.abort();
                    if (!pending.includes(job)) return;
                    pending.splice(pending.indexOf(job), 1);
                    finishItem(view, 'cancelled', 'İptal edildi');
                });
                pending.push(job);
            }
            drain();
        }
    };
}
