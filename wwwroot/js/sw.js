// RentTracker Service Worker
// Minimal cache-first strategy for static assets, network-first for pages

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
