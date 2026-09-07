// Caution! Be sure you understand the caveats before publishing an application with
// offline support. See https://aka.ms/blazor-offline-considerations

self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => {
    self.skipWaiting();
    event.waitUntil(onInstall(event));
});
self.addEventListener('activate', event => {
    event.waitUntil(
        onActivate(event).then(() => self.clients.claim())
    );
});
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

// ---- Notificaciones push ----
self.addEventListener('push', event => {
    let data = {};
    try { data = event.data ? event.data.json() : {}; } catch (e) { data = { body: event.data ? event.data.text() : '' }; }
    const title = data.title || 'Atlètic Poblenou';
    const options = {
        body: data.body || '',
        icon: new URL('icon-192.png', self.registration.scope).href,
        badge: new URL('icon-192.png', self.registration.scope).href,
        tag: data.tag || undefined,
        renotify: !!data.tag,
        data: { url: data.url || './' }
    };
    event.waitUntil(self.registration.showNotification(title, options));
});

self.addEventListener('notificationclick', event => {
    event.notification.close();
    const target = new URL(event.notification.data && event.notification.data.url ? event.notification.data.url : './', self.registration.scope).href;
    event.waitUntil((async () => {
        const all = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
        for (const c of all) {
            if (c.url.startsWith(self.registration.scope) && 'focus' in c) return c.focus();
        }
        return self.clients.openWindow(target);
    })());
});

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [ /\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.webmanifest$/ ];
const offlineAssetsExclude = [ /^service-worker\.js$/ ];

// Replace with your base path if you are hosting on a subfolder. Ensure there is a trailing '/'.
const base = self.location.pathname.includes('/AppPoblenou/') ? '/AppPoblenou/' : '/';
const baseUrl = new URL(base, self.origin);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);

async function onInstall(event) {
    console.info('Service worker: Install');

    // Fetch and cache all matching items from the assets manifest
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(new URL(asset.url, baseUrl).href, { cache: 'no-cache' }));
    await caches.open(cacheName).then(cache => cache.addAll(assetsRequests));
}

async function onActivate(event) {
    console.info('Service worker: Activate');

    // Delete unused caches
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
}

async function onFetch(event) {
    if (event.request.method === 'GET') {
        const shouldServeIndexHtml = event.request.mode === 'navigate'
            && !manifestUrlList.some(url => url === event.request.url);

        if (shouldServeIndexHtml) {
            try {
                const networkResponse = await fetch(event.request);
                if (networkResponse.ok) {
                    return networkResponse;
                }
            } catch (err) {
                // Offline fallback
            }
            const cache = await caches.open(cacheName);
            return (await cache.match('index.html')) || fetch(event.request);
        }

        const cache = await caches.open(cacheName);
        const cachedResponse = await cache.match(event.request);
        if (cachedResponse) {
            return cachedResponse;
        }
    }

    return fetch(event.request);
}
