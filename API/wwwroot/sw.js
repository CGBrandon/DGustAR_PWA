const CACHE_NAME = 'dgustar-pwa-v7';

const CORE_ASSETS = [
  '/',
  '/menu',
  '/admin',
  '/app.html',
  '/logo.svg',
  '/logo-192.png',
  '/logo-512.png',
  '/logo-admin.svg',
  '/logo-admin-192.png',
  '/logo-admin-512.png',
  '/manifest.json',
  '/manifest-admin.json'
];

// 1. Instalación y precaché de recursos críticos
self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => {
      console.log('[SW] Precachando App Shell...');
      return cache.addAll(CORE_ASSETS);
    }).then(() => self.skipWaiting())
  );
});

// 2. Activación y purga de versiones obsoletas
self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((cacheNames) => {
      return Promise.all(
        cacheNames.map((name) => {
          if (name !== CACHE_NAME) {
            console.log('[SW] Eliminando caché antigua:', name);
            return caches.delete(name);
          }
        })
      );
    }).then(() => self.clients.claim())
  );
});

// 3. Estrategias de captura de red
self.addEventListener('fetch', (event) => {
  const url = new URL(event.request.url);

  // Ignorar peticiones WebSocket o métodos que no sean GET
  if (event.request.method !== 'GET' || url.protocol === 'ws:' || url.protocol === 'wss:') {
    return;
  }

  // Estrategia Cache-First para Modelos 3D (.glb), fuentes e imágenes
  const is3DModel = url.pathname.endsWith('.glb');
  const isImage = /\.(png|jpg|jpeg|svg|webp|gif)$/i.test(url.pathname);
  const isFont = url.hostname.includes('fonts.gstatic.com') || url.hostname.includes('fonts.googleapis.com');

  if (is3DModel || isImage || isFont) {
    event.respondWith(
      caches.match(event.request).then((cachedResponse) => {
        if (cachedResponse) {
          return cachedResponse;
        }
        return fetch(event.request).then((networkResponse) => {
          if (networkResponse && networkResponse.status === 200) {
            const responseToCache = networkResponse.clone();
            caches.open(CACHE_NAME).then((cache) => {
              cache.put(event.request, responseToCache);
            });
          }
          return networkResponse;
        }).catch(() => {
          // Si falla la red y no hay caché, retornar fallback si es imagen
          return caches.match('/logo.svg');
        });
      })
    );
    return;
  }

  // Estrategia Stale-While-Revalidate para el catálogo /api/menu
  if (url.pathname === '/api/menu') {
    event.respondWith(
      caches.open(CACHE_NAME).then((cache) => {
        return cache.match(event.request).then((cachedResponse) => {
          const fetchPromise = fetch(event.request).then((networkResponse) => {
            if (networkResponse && networkResponse.status === 200) {
              cache.put(event.request, networkResponse.clone());
            }
            return networkResponse;
          }).catch((err) => {
            console.warn('[SW] Sin conexión para actualizar catálogo:', err);
            return cachedResponse;
          });

          return cachedResponse || fetchPromise;
        });
      })
    );
    return;
  }

  // Estrategia Network-First para páginas HTML y navegación
  event.respondWith(
    fetch(event.request).then((networkResponse) => {
      if (networkResponse && networkResponse.status === 200 && event.request.destination === 'document') {
        const responseToCache = networkResponse.clone();
        caches.open(CACHE_NAME).then((cache) => {
          cache.put(event.request, responseToCache);
        });
      }
      return networkResponse;
    }).catch(() => {
      return caches.match(event.request).then((cached) => {
        if (cached) return cached;
        if (event.request.destination === 'document') {
          return caches.match('/app.html');
        }
      });
    })
  );
});

// 4. Clic en notificación: enfocar o abrir la pantalla de seguimiento de pedido
self.addEventListener('notificationclick', (event) => {
  event.notification.close();
  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((clientList) => {
      for (const client of clientList) {
        if (client.url.includes('/pedido') && 'focus' in client) {
          return client.focus();
        }
      }
      if (self.clients.openWindow) {
        return self.clients.openWindow('/pedido');
      }
    })
  );
});

