import { initTooltips } from './components/tooltip.js';
import { showPendingFlash } from './core/notify.js';
import { activatePageScope } from './core/page-scope.js';
import { initSoftNav } from './features/navigation/soft-nav.js';
import { initAlertBell } from './layout/alert-bell.js';
import { closeAllDropdowns, initDropdowns } from './layout/dropdowns.js';
import { initLogout } from './layout/session.js';
import { initSidebar } from './layout/sidebar.js';
import { initTheme } from './layout/theme.js';

initDropdowns();
initTheme({ onChange: () => closeAllDropdowns() });
initSidebar();
initLogout();
initTooltips();
showPendingFlash();
initAlertBell();
// Kabuk dinleyicileri yukarıda, sayfa kapsamı dışında bağlandı. Sekme geçişi onları kesmez.
activatePageScope();
initSoftNav();
