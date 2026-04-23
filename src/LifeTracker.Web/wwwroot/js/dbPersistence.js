// IndexedDB persistence for SQLite database bytes.
// Stores the entire .db file as a single Blob under key "lifetracker".

const DB_NAME = 'lifetracker-storage';
const STORE_NAME = 'sqlite-files';
const FILE_KEY = 'lifetracker';

// Request persistent storage once. Without this, iOS Safari evicts the
// IndexedDB after ~7 days of inactivity, and Chrome/Firefox may evict
// under storage pressure. With it granted, the data is wiped only when
// the user explicitly clears site data. Best-effort — some browsers
// grant only after the PWA is installed to the home screen.
let persistRequested = false;
async function requestPersistentStorage() {
    if (persistRequested) return;
    persistRequested = true;
    if (!('storage' in navigator) || !('persist' in navigator.storage)) {
        return;
    }
    try {
        const already = await navigator.storage.persisted();
        if (!already) {
            const granted = await navigator.storage.persist();
            console.info('[LifeTracker] storage.persist() granted:', granted);
        }
    } catch (err) {
        console.warn('[LifeTracker] storage.persist() failed:', err);
    }
}

function openIdb() {
    return new Promise((resolve, reject) => {
        const req = indexedDB.open(DB_NAME, 1);
        req.onupgradeneeded = () => {
            const db = req.result;
            if (!db.objectStoreNames.contains(STORE_NAME)) {
                db.createObjectStore(STORE_NAME);
            }
        };
        req.onsuccess = () => resolve(req.result);
        req.onerror = () => reject(req.error);
    });
}

export async function loadDb() {
    await requestPersistentStorage();
    const idb = await openIdb();
    return new Promise((resolve, reject) => {
        const tx = idb.transaction(STORE_NAME, 'readonly');
        const store = tx.objectStore(STORE_NAME);
        const req = store.get(FILE_KEY);
        req.onsuccess = () => {
            const bytes = req.result;
            const size = bytes ? (bytes.byteLength ?? bytes.length) : 0;
            console.info('[LifeTracker] loadDb:', size, 'bytes');
            resolve(bytes ? new Uint8Array(bytes) : null);
        };
        req.onerror = () => reject(req.error);
    });
}

export async function saveDb(bytes) {
    await requestPersistentStorage();
    const idb = await openIdb();
    return new Promise((resolve, reject) => {
        const tx = idb.transaction(STORE_NAME, 'readwrite');
        const store = tx.objectStore(STORE_NAME);
        const req = store.put(bytes, FILE_KEY);
        req.onsuccess = () => {
            console.info('[LifeTracker] saveDb:', bytes.length, 'bytes');
            resolve();
        };
        req.onerror = () => reject(req.error);
    });
}

// Surfaced in the Settings page so the user can see whether their
// browser granted persistent storage and how large the DB actually is.
// A dbBytes of 0 after a reload means the browser wiped us.
export async function getStorageInfo() {
    const hasApi = 'storage' in navigator && 'persist' in navigator.storage;
    let persisted = null, usage = null, quota = null;
    if (hasApi) {
        try { persisted = await navigator.storage.persisted(); } catch {}
        try {
            const est = await navigator.storage.estimate();
            usage = est.usage ?? null;
            quota = est.quota ?? null;
        } catch {}
    }

    let dbBytes = 0;
    try {
        const idb = await openIdb();
        const bytes = await new Promise((res, rej) => {
            const tx = idb.transaction(STORE_NAME, 'readonly');
            const req = tx.objectStore(STORE_NAME).get(FILE_KEY);
            req.onsuccess = () => res(req.result);
            req.onerror = () => rej(req.error);
        });
        dbBytes = bytes ? (bytes.byteLength ?? bytes.length ?? 0) : 0;
    } catch {}

    return {
        apiSupported: hasApi,
        persisted: persisted,
        usageBytes: usage,
        quotaBytes: quota,
        dbBytes: dbBytes
    };
}

export async function deleteDb() {
    const idb = await openIdb();
    return new Promise((resolve, reject) => {
        const tx = idb.transaction(STORE_NAME, 'readwrite');
        const store = tx.objectStore(STORE_NAME);
        const req = store.delete(FILE_KEY);
        req.onsuccess = () => resolve();
        req.onerror = () => reject(req.error);
    });
}
