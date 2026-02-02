const CACHE_NAME = "v20260202";
const addResourcesToCache = async (resources) => {
    const cache = await caches.open(CACHE_NAME);
    await cache.addAll(resources);
};

const putInCache = async (request, response) => {
    const cache = await caches.open(CACHE_NAME);
    await cache.put(request, response);
};

const generateErrorResponse = () => new Response("Network error happened", {
    status: 408,
    headers: { "Content-Type": "text/plain" },
});

const cacheFirst = async ({request, preloadResponsePromise, event}) => {
    if (request.url.startsWith('chrome-extension://')) {
        try {
            return await fetch(request);
        } catch (error) {
            return generateErrorResponse();
        }
    }
    // First try to get the resource from the cache
    const responseFromCache = await caches.match(request);
    if (responseFromCache) {
        return responseFromCache;
    }

    // for navigation requests, fallback to cached index.html
    if (request.mode === 'navigate') {
        const cachedIndex = await caches.match('./index.html');
        if (cachedIndex) return cachedIndex;
        return generateErrorResponse();
    }

    // Next try to use (and cache) the preloaded response, if it's there
    if (preloadResponsePromise) {
        let preloadResponse;
        event.waitUntil(preloadResponsePromise);
        try {
            preloadResponse = await preloadResponsePromise;
        } catch (e) {
            console.error("Navigation preload failed", e);
            preloadResponse = null;
        }
        if (preloadResponse) {
            event.waitUntil(putInCache(request, preloadResponse.clone()));
            return preloadResponse;
        }
    }

    // Next try to get the resource from the network
    try {
        const responseFromNetwork = await fetch(request);
        // response may be used only once
        // we need to save clone to put one copy in cache
        // and serve second one
        if (!responseFromNetwork.redirected) {
            event.waitUntil(putInCache(request, responseFromNetwork.clone()));
        }
        return responseFromNetwork;
    } catch (error) {
        if (request.mode === 'navigate') {
            const cachedIndex = await caches.match('./index.html');
            if (cachedIndex) {
                return cachedIndex;
            }
        }
        return generateErrorResponse();
    }
};

// Enable navigation preload
const enableNavigationPreloadAndClearOldCache = async () => {
    if (self.registration.navigationPreload) {
        try {
            await self.registration.navigationPreload.enable();
        }
        catch (error) {
            console.error("Error enabling navigation preload", error);
        }
    }
    caches.keys().then(keys => Promise.all(keys.filter(k => k !== CACHE_NAME).map(k => caches.delete(k))))
};

self.addEventListener("activate", async (event) => {
    event.waitUntil(enableNavigationPreloadAndClearOldCache());
    await self.clients.claim();
});
self.addEventListener("install", async (event) => {
    event.waitUntil(addResourcesToCache(["./", "./index.html", "./main.js", "./assets/logo.webp", "./favicon.webp", "./dashboard.html"]));
    await self.skipWaiting();
});
self.addEventListener("fetch", (event) => {
    event.respondWith(cacheFirst({ request: event.request, preloadResponsePromise: event.preloadResponse, event }));
});