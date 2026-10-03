import { qs } from '../core/dom.js';
import { initDeploymentList } from '../features/deployments/deployment-list.js';
import { initServerPage } from '../features/servers/server-page.js';

initServerPage();
initDeploymentList(qs('[data-ajax-list]'), { embedded: true });
