using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Pharmacie.Controllers;

/// <summary>
/// Sert sw.js dynamiquement : la version change à chaque démarrage de l'app
/// pour forcer la mise à jour du Service Worker après déploiement.
/// </summary>
[AllowAnonymous]
public class ServiceWorkerController : Controller
{
    /// <summary>Figé pour la durée de vie du processus (redémarrage Azure / publish).</summary>
    private static readonly string CacheVersion = DateTime.UtcNow.Ticks.ToString();

    [HttpGet("/sw.js")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public ContentResult Index()
    {
        Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        Response.Headers.Pragma = "no-cache";

        var sw = $$"""
            const CACHE_VERSION = '{{CacheVersion}}';
            const CACHE_NAME = 'pharmacie-sjp-' + CACHE_VERSION;

            // Ne mettre en cache QUE les assets statiques (jamais le HTML)
            const STATIC_ASSETS = [
              '/css/site.css',
              '/lib/bootstrap/dist/css/bootstrap.min.css',
              '/icons/icon-192.png',
              '/icons/icon-512.png',
            ];

            self.addEventListener('install', event => {
              event.waitUntil(
                caches.open(CACHE_NAME).then(cache => {
                  return cache.addAll(STATIC_ASSETS);
                }).catch(() => {
                  // Ignorer les erreurs de cache
                })
              );
              self.skipWaiting();
            });

            self.addEventListener('activate', event => {
              event.waitUntil(
                caches.keys().then(keys =>
                  Promise.all(
                    keys.filter(k => k !== CACHE_NAME)
                        .map(k => caches.delete(k))
                  )
                )
              );
              self.clients.claim();
            });

            self.addEventListener('fetch', event => {
              // JAMAIS mettre en cache les pages HTML (navigation)
              if (event.request.mode === 'navigate') {
                event.respondWith(fetch(event.request));
                return;
              }

              if (event.request.method !== 'GET') {
                return;
              }

              // Assets statiques → cache, sinon réseau
              event.respondWith(
                caches.match(event.request).then(cached => {
                  return cached || fetch(event.request);
                })
              );
            });
            """;

        return Content(sw, "application/javascript; charset=utf-8");
    }
}
