import { flash } from './notify.js';

/** Yeni bir kaynağa geçerken sonucu hedef sayfada toastr ile göstermek için kullanılır. */
export function navigate(url, message, type = 'success') {
    flash(type, message);
    window.location.assign(url);
}
