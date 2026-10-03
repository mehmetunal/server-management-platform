import { showPendingFlash } from './core/notify.js';
import { closeAllDropdowns, initDropdowns } from './layout/dropdowns.js';
import { initLogout } from './layout/session.js';
import { initSidebar } from './layout/sidebar.js';
import { initTheme } from './layout/theme.js';

initDropdowns();
initTheme({ onChange: () => closeAllDropdowns() });
initSidebar();
initLogout();
showPendingFlash();
