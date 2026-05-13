// RentTracker Service Worker
// ==========================
// Purpose: Provides offline support and faster loading via caching.
// This is a minimal, no-build-service-worker approach.
//
// Strategy:
// - Network-first for HTML pages (falls back to cached root page if offline)
// - Cache-first for static assets (CSS, JS, images, fonts)
// - Old caches are cleaned up on activation
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

var CACHE_NAME = 'renttracker-v1';
var STATIC_URLS = [
  '/',
  '/css/site.css',
  '/js/site.js',
  '/manifest.json',
  '/icons/icon.svg'
];

self.addEventListener('install', function(event) {
  event.waitUntil(
    caches.open(CACHE_NAME).then(function(cache) {
      return cache.addAll(STATIC_URLS);
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
        return caches.match('/');
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
