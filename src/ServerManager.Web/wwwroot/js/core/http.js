const NETWORK_ERROR = 'Ağ hatası: sunucuya ulaşılamadı.';

const STATUS_MESSAGES = {
    400: 'İstek doğrulanamadı. Sayfayı yenileyip tekrar deneyin.',
    403: 'Bu işlem için yetkiniz yok.',
    404: 'Kayıt bulunamadı.',
    429: 'Çok fazla istek gönderildi. Lütfen biraz bekleyin.'
};

function antiforgeryToken() {
    const input = document.querySelector('input[name="__RequestVerificationToken"]');
    return input ? input.value : '';
}

function toParams(data) {
    if (data instanceof URLSearchParams) return data;
    const params = new URLSearchParams();
    if (data instanceof FormData) {
        for (const [key, value] of data) {
            if (typeof value === 'string' && key !== '__RequestVerificationToken') params.append(key, value);
        }
        return params;
    }
    for (const [key, value] of Object.entries(data ?? {})) {
        if (value !== undefined && value !== null) params.append(key, String(value));
    }
    return params;
}

function failure(status, message) {
    return { isSuccess: false, status, message, data: null, errors: {}, validationMessages: [] };
}

function statusMessage(status) {
    if (status >= 500) return 'Sunucuda beklenmeyen bir hata oluştu.';
    return STATUS_MESSAGES[status] ?? `Sunucudan beklenmeyen bir yanıt alındı (${status}).`;
}

// Oturum düştüğünde sayfa yeniden istenir; sunucu giriş sayfasına dönüş adresiyle yönlendirir.
function handleUnauthorized(response) {
    if (response.status !== 401) return false;
    window.location.reload();
    return true;
}

async function parseJson(response) {
    if (handleUnauthorized(response)) return failure(401, 'Oturum süresi doldu. Lütfen tekrar giriş yapın.');
    const contentType = response.headers.get('content-type') ?? '';
    if (contentType.includes('application/json')) {
        try {
            const body = await response.json();
            return {
                isSuccess: body.isSuccess === true,
                status: response.status,
                message: body.message || (body.isSuccess ? '' : statusMessage(response.status)),
                data: body.data ?? null,
                errors: body.errors ?? {},
                validationMessages: body.validationMessages ?? []
            };
        } catch {
            // Gövde JSON değilse durum koduna göre mesaj üretilir.
        }
    }
    return failure(response.status, statusMessage(response.status));
}

async function send(url, init) {
    try {
        return await parseJson(await fetch(url, { credentials: 'same-origin', ...init }));
    } catch {
        return failure(0, NETWORK_ERROR);
    }
}

export function getJson(url) {
    return send(url, { headers: { Accept: 'application/json', 'X-Requested-With': 'XMLHttpRequest' } });
}

export function postForm(url, data) {
    return send(url, {
        method: 'POST',
        headers: {
            Accept: 'application/json',
            'X-Requested-With': 'XMLHttpRequest',
            'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
            RequestVerificationToken: antiforgeryToken()
        },
        body: toParams(data)
    });
}

function parseXhr(xhr) {
    if (xhr.status === 401) {
        window.location.reload();
        return failure(401, 'Oturum süresi doldu. Lütfen tekrar giriş yapın.');
    }
    if ((xhr.getResponseHeader('content-type') ?? '').includes('application/json')) {
        try {
            const body = JSON.parse(xhr.responseText);
            return {
                isSuccess: body.isSuccess === true,
                status: xhr.status,
                message: body.message || (body.isSuccess ? '' : statusMessage(xhr.status)),
                data: body.data ?? null,
                errors: body.errors ?? {},
                validationMessages: body.validationMessages ?? []
            };
        } catch {
            // Gövde JSON değilse durum koduna göre mesaj üretilir.
        }
    }
    return failure(xhr.status, xhr.status === 413 ? 'Dosya izin verilen boyutu aşıyor.' : statusMessage(xhr.status));
}

/**
 * multipart/form-data gönderir; fetch yükleme ilerlemesi vermediği için XMLHttpRequest kullanılır.
 * onProgress(loaded, total) yükleme sürerken çağrılır. signal ile istek iptal edilebilir.
 */
export function uploadForm(url, formData, { onProgress, signal } = {}) {
    return new Promise(resolve => {
        const xhr = new XMLHttpRequest();
        xhr.open('POST', url);
        xhr.setRequestHeader('Accept', 'application/json');
        xhr.setRequestHeader('X-Requested-With', 'XMLHttpRequest');
        xhr.setRequestHeader('RequestVerificationToken', antiforgeryToken());
        xhr.upload.addEventListener('progress', event => {
            if (event.lengthComputable) onProgress?.(event.loaded, event.total);
        });
        xhr.addEventListener('load', () => resolve(parseXhr(xhr)));
        xhr.addEventListener('error', () => resolve(failure(0, NETWORK_ERROR)));
        xhr.addEventListener('abort', () => resolve(failure(0, 'Yükleme iptal edildi.')));
        signal?.addEventListener('abort', () => xhr.abort());
        xhr.send(formData);
    });
}

/**
 * HTML parçası ister. partial: true iken liste action'ları yalnızca partial view döner;
 * false iken tam sayfa alınır (bölge yenileme için).
 */
export async function getHtml(url, { partial = true } = {}) {
    try {
        const headers = { Accept: 'text/html' };
        if (partial) headers['X-Requested-With'] = 'XMLHttpRequest';
        const response = await fetch(url, { credentials: 'same-origin', headers });
        if (handleUnauthorized(response)) return { ok: false, status: 401, html: '', message: '' };
        const html = await response.text();
        return { ok: response.ok, status: response.status, html, message: response.ok ? '' : statusMessage(response.status) };
    } catch {
        return { ok: false, status: 0, html: '', message: NETWORK_ERROR };
    }
}

export function failureMessage(response, fallback = 'İşlem başarısız.') {
    if (response.validationMessages?.length) return response.validationMessages.join(' ');
    return response.message || fallback;
}
