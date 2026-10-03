import { createTabs } from '../components/tabs.js';
import { bindModals } from '../components/modal.js';
import { createRemotePanels } from '../components/remote-panels.js';
import { on, qs, setBusy } from '../core/dom.js';
import { bindAjaxForm } from '../core/forms.js';
import { navigate } from '../core/navigation.js';
import { createLogViewer } from '../features/docker/logs.js';
import { createTerminal } from '../features/docker/terminal.js';
import { initServerPage } from '../features/servers/server-page.js';

const panels = createRemotePanels();
const logs = createLogViewer(qs('[data-logs]'));
const terminal = createTerminal(qs('[data-terminal]'));

initServerPage({ onActionSuccess: () => panels.reload() });
bindModals();

createTabs({
    defaultTab: 'info',
    activators: {
        logs: () => logs?.activate(),
        terminal: () => terminal?.activate()
    }
});

on(document, 'click', '[data-panel-refresh]', async (event, button) => {
    setBusy(button, true);
    try {
        await panels.reload();
    } finally {
        setBusy(button, false);
    }
});

const renameForm = qs('[data-rename-form]');
bindAjaxForm(renameForm, {
    onSuccess: response => {
        const newName = renameForm.elements.NewName.value.trim();
        navigate(`${renameForm.dataset.containerUrl}?name=${encodeURIComponent(newName)}`, response.message);
    }
});
