// Tiny localStorage wrapper so Blazor can read/write settings
// without importing a full JS interop surface. Keys are namespaced
// under "lt:" so we don't collide with anything else on the origin.
const PREFIX = "lt:";

export function get(key) {
    return localStorage.getItem(PREFIX + key);
}

export function set(key, value) {
    localStorage.setItem(PREFIX + key, value);
}

export function remove(key) {
    localStorage.removeItem(PREFIX + key);
}
