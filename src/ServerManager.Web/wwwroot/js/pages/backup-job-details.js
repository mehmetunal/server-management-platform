import { bindAjaxActions } from '../components/ajax-actions.js';
import { qs } from '../core/dom.js';
import { initBackupRunList } from '../features/backups/backup-run-list.js';

const list = initBackupRunList(qs('[data-ajax-list]'), { embedded: true });

bindAjaxActions(document, { onSuccess: () => list?.reload() });
