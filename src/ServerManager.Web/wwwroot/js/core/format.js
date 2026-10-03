const numberFormat = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 1 });
const BYTE_UNITS = ['B', 'KB', 'MB', 'GB', 'TB'];

export function formatNumber(value) {
    return numberFormat.format(value);
}

export function formatBytes(value) {
    if (value === null || value === undefined || Number.isNaN(Number(value))) return '—';
    let amount = Number(value);
    let unit = 0;
    while (Math.abs(amount) >= 1024 && unit < BYTE_UNITS.length - 1) {
        amount /= 1024;
        unit++;
    }
    return `${numberFormat.format(amount)} ${BYTE_UNITS[unit]}`;
}

const pad = (value, size = 2) => String(value).padStart(size, '0');

export function formatTimestamp(iso) {
    if (!iso) return '';
    const date = new Date(iso);
    if (Number.isNaN(date.getTime())) return '';
    return `${pad(date.getDate())}.${pad(date.getMonth() + 1)} ${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}.${pad(date.getMilliseconds(), 3)}`;
}

export function formatClock(date = new Date()) {
    return date.toLocaleTimeString('tr-TR');
}
