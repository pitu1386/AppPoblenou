// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development more difficult (changes would not
// be reflected on the first load after each change).
self.addEventListener('fetch', () => { });

// Notificaciones push también en desarrollo (para probar contra Supabase real).
self.addEventListener('push', event => {
    let data = {};
    try { data = event.data ? event.data.json() : {}; } catch (e) { data = { body: event.data ? event.data.text() : '' }; }
    event.waitUntil(self.registration.showNotification(data.title || 'Atlètic Poblenou (dev)', {
        body: data.body || '',
        icon: new URL('icon-192.png', self.registration.scope).href,
        badge: new URL('notif-badge.png', self.registration.scope).href,
        tag: data.tag || undefined, data: { url: data.url || './' }
    }));
});
self.addEventListener('notificationclick', event => {
    event.notification.close();
    event.waitUntil(self.clients.openWindow(new URL('./', self.registration.scope).href));
});
