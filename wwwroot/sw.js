// SmartSlot Service Worker — handles background push notifications
// Place this file at: wwwroot/sw.js

const CACHE_NAME = 'smartslot-v1';

// ── Install ──────────────────────────────────────────────────────────────────
self.addEventListener('install', function (event) {
    console.log('[SW] Installed');
    self.skipWaiting();
});

// ── Activate ─────────────────────────────────────────────────────────────────
self.addEventListener('activate', function (event) {
    console.log('[SW] Activated');
    event.waitUntil(self.clients.claim());
});

// ── Push received ─────────────────────────────────────────────────────────────
self.addEventListener('push', function (event) {
    console.log('[SW] Push received');

    var data = {};
    if (event.data) {
        try {
            data = event.data.json();
        } catch (e) {
            data = {
                title: 'SmartSlot',
                body: event.data.text(),
                icon: '/images/icon-192.png'
            };
        }
    }

    var title = data.title || 'SmartSlot';
    var options = {
        body: data.body || 'You have a new notification',
        icon: data.icon || '/images/icon-192.png',
        badge: '/images/badge-72.png',
        tag: data.tag || 'smartslot-notification',
        renotify: true,
        data: {
            url: data.url || '/'
        },
        vibrate: [200, 100, 200],
        requireInteraction: false
    };

    event.waitUntil(
        self.registration.showNotification(title, options)
    );
});

// ── Notification click ────────────────────────────────────────────────────────
self.addEventListener('notificationclick', function (event) {
    console.log('[SW] Notification clicked');
    event.notification.close();

    var targetUrl = (event.notification.data && event.notification.data.url)
        ? event.notification.data.url
        : '/';

    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true }).then(function (clientList) {
            for (var i = 0; i < clientList.length; i++) {
                var client = clientList[i];
                if (client.url.includes(self.location.origin) && 'focus' in client) {
                    client.focus();
                    client.navigate(targetUrl);
                    return;
                }
            }
            if (clients.openWindow) {
                return clients.openWindow(targetUrl);
            }
        })
    );
});

// ── Push subscription change ──────────────────────────────────────────────────
self.addEventListener('pushsubscriptionchange', function (event) {
    console.log('[SW] Push subscription changed — re-subscribing');
    event.waitUntil(
        self.registration.pushManager.subscribe(event.oldSubscription.options)
            .then(function (sub) {
                return fetch('/Parking/SavePushSubscription', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        endpoint: sub.endpoint,
                        keys: sub.toJSON().keys
                    })
                });
            })
    );
});