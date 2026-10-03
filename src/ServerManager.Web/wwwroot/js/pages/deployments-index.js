import { qs } from '../core/dom.js';
import { initDeploymentList } from '../features/deployments/deployment-list.js';

initDeploymentList(qs('[data-ajax-list]'), { form: qs('[data-list-filter]') });
