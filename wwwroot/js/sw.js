// RentTracker Service Worker
// ==========================
// Purpose: Provides offline support and faster loading via caching.
// This is a minimal, no-build-service-worker approach.
//
// Strategy:
// - Network-first for HTML pages (falls back to cached root page if offline)
// - Cache-first for static assets (CSS, JS, images, fonts)
// - Old caches are cleaned up on activation
// - Update notification via postMessage to clients when new version is ready
//
// Cache naming: Uses 'renttracker-v{N}' so bumping the version
// forces a full cache refresh on next visit.
//
// Limitations:
// - Does NOT cache API/data responses (those come from SQLite server-side)
// - Only caches pre-defined static URLs on install
// - Dynamic content (reports, dashboards) always fetches from network
//
// To update cached assets: increment CACHE_NAME version string.
// Added in this branch: offline.html, icon-192.png, icon-512.png, update banner.

var CACHE_NAME = 'renttracker-v2';
var STATIC_URLS = [
  '/',
  '/offline.html',
  '/css/site.css',
  '/js/site.js',
  '/manifest.json',
  '/icons/icon.svg',
  '/icons/icon-192.png',
  '/icons/icon-512.png'
];

self.addEventListener('install', function(event) {
  event.waitUntil(
    caches.open(CACHE_NAME).then(function(cache) {
      return cache.addAll(STATIC_URLS);
    }).then(function() {
      // Skip waiting so the new service worker activates immediately
      return self.skipWaiting();
    })
  );
});

self.addEventListener('activate', function(event) {
  event.waitUntil(
    caches.keys().then(function(keyList) {
      return Promise.all(keyList.map(function(key) {
        if (key !== CACHE_NAME) {
          return caches.delete(key);
        }
      }));
    }).then(function() {
      // Take control of all clients so updates apply immediately
      return self.clients.claim();
    })
  );
});

self.addEventListener('fetch', function(event) {
  var requestUrl = new URL(event.request.url);

  // Skip non-GET requests
  if (event.request.method !== 'GET') return;

  // Only handle same-origin requests
  if (requestUrl.origin !== location.origin) return;

  // Network-first for pages (html documents)
  if (event.request.mode === 'navigate') {
    event.respondWith(
      fetch(event.request).catch(function() {
        // Return cached root, or offline page if not available
        return caches.match('/offline.html').then(function(resp) {
          return resp || caches.match('/');
        });
      })
    );
    return;
  }

  // Cache-first for static assets (css, js, images)
  event.respondWith(
    caches.match(event.request).then(function(response) {
      return response || fetch(event.request);
    })
  );
});

// Notify clients when an update is waiting so the page can show a refresh banner
self.addEventListener('message', function(event) {
  if (event.data && event.data.type === 'CHECK_UPDATE') {
    event.waitUntil(
      self.registration.update().then(function() {
        if (event.source) {
          event.source.postMessage({ type: 'UPDATE_CHECKED' });
        }
      })
    );
  }
});
