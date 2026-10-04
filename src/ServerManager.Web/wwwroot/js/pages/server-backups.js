import { createAjaxList } from '../components/ajax-list.js';
import { qs } from '../core/dom.js';
import { initBackupRunList } from '../features/backups/backup-run-list.js';
import { initServerPage } from '../features/servers/server-page.js';

const jobRoot = qs('[data-job-list]');
const jobs = createAjaxList(jobRoot, { history: false, url: jobRoot.dataset.url });
const runs = initBackupRunList(qs('[data-ajax-list]'), { embedded: true });

initServerPage({ onActionSuccess: () => Promise.all([jobs.reload(), runs?.reload()]) });
