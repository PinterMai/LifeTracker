// IndexedDB persistence for SQLite database bytes.
// Stores the entire .db file as a single Blob under key "lifetracker".

const DB_NAME = 'lifetracker-storage';
const STORE_NAME = 'sqlite-files';
const FILE_KEY = 'lifetracker';

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
    const idb = await openIdb();
    return new Promise((resolve, reject) => {
        const tx = idb.transaction(STORE_NAME, 'readonly');
        const store = tx.objectStore(STORE_NAME);
        const req = store.get(FILE_KEY);
        req.onsuccess = () => {
            const bytes = req.result;
            resolve(bytes ? new Uint8Array(bytes) : null);
        };
        req.onerror = () => reject(req.error);
    });
}

export async function saveDb(bytes) {
    const idb = await openIdb();
    return new Promise((resolve, reject) => {
        const tx = idb.transaction(STORE_NAME, 'readwrite');
        const store = tx.objectStore(STORE_NAME);
        const req = store.put(bytes, FILE_KEY);
        req.onsuccess = () => resolve();
        req.onerror = () => reject(req.error);
    });
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
