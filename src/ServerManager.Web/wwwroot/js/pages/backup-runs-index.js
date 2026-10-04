import { qs } from '../core/dom.js';
import { initBackupRunList } from '../features/backups/backup-run-list.js';

initBackupRunList(qs('[data-ajax-list]'), { form: qs('[data-list-filter]') });
