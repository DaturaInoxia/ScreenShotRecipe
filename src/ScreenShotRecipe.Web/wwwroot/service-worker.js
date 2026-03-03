// ScreenShot Recipe Service Worker
// This provides PWA install capability while keeping the app online-first
// (Blazor Server requires server connection)

const CACHE_VERSION = 'v1';
const CACHE_NAME = `screenshot-recipe-${CACHE_VERSION}`;

// Assets to cache for faster loading (not offline support)
const STATIC_ASSETS = [
  '/css/app.css',
  '/icon-192.png',
  '/icon-512.png',
  '/favicon.png'
];

// Install event - cache static assets
self.addEventListener('install', event => {
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then(cache => cache.addAll(STATIC_ASSETS))
      .then(() => self.skipWaiting())
  );
});

// Activate event - clean up old caches
self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys()
      .then(keys => Promise.all(
        keys
          .filter(key => key.startsWith('screenshot-recipe-') && key !== CACHE_NAME)
          .map(key => caches.delete(key))
      ))
      .then(() => self.clients.claim())
  );
});

// Fetch event - network first, fallback to cache for static assets
self.addEventListener('fetch', event => {
  // Skip non-GET requests
  if (event.request.method !== 'GET') {
    return;
  }

  // Skip Blazor SignalR connections and API calls
  const url = new URL(event.request.url);
  if (url.pathname.startsWith('/_blazor') || 
      url.pathname.startsWith('/api/') ||
      url.pathname.startsWith('/_framework/')) {
    return;
  }

  // For static assets, try cache first then network
  if (STATIC_ASSETS.some(asset => url.pathname.endsWith(asset.replace('/', '')))) {
    event.respondWith(
      caches.match(event.request)
        .then(cached => cached || fetch(event.request))
    );
    return;
  }

  // For everything else, network first
  event.respondWith(fetch(event.request));
});
