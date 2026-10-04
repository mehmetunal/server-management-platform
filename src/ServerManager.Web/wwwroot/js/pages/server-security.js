import { bindCopyButtons, bindSecurityScan } from '../features/security/security-scan.js';
import { initServerPage } from '../features/servers/server-page.js';

initServerPage({ regions: ['server-header', 'server-security'] });
bindSecurityScan(['server-security']);
bindCopyButtons();
