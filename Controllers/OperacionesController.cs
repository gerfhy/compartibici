using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using CompartiBici.Data;
using CompartiBici.Models;
using CompartiBici.Services;

namespace CompartiBici.Controllers;

public class OperacionesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IAlgoliaSearchService _algoliaService;
    private readonly IDistributedCache _cache;
    private readonly ILogger<OperacionesController> _logger;

    private const string CacheKeyListado = "incidencias_abiertas_listado";

    public OperacionesController(
        ApplicationDbContext context,
        IAlgoliaSearchService algoliaService,
        IDistributedCache cache,
        ILogger<OperacionesController> logger)
    {
        _context = context;
        _algoliaService = algoliaService;
        _cache = cache;
        _logger = logger;
    }

    // GET: /Operaciones/Incidencias?q=...
    public async Task<IActionResult> Incidencias(string? q)
    {
        // Caso 1: Búsqueda con texto en Algolia (se consulta directamente sin usar caché de Redis)
        if (!string.IsNullOrWhiteSpace(q))
        {
            var hitIds = await _algoliaService.BuscarIncidenciasAsync(q);

            var resultados = await _context.Incidencias
                .Where(i => hitIds.Contains(i.Id) && i.Estado == "Abierta")
                .OrderByDescending(i => i.FechaRegistro)
                .ToListAsync();

            ViewBag.Busqueda = q;
            ViewBag.OrigenLectura = "ALGOLIA DIRECTO";
            _logger.LogInformation(">>> [ALGOLIA SEARCH DIRECTO] Consulta sin caché para '{Query}'. Resultados abiertos: {Count}", q, resultados.Count);

            return View(resultados);
        }

        // Caso 2: Listado general (búsqueda vacía) cacheado por 60 segundos con Redis
        List<Incidencia>? incidencias = null;
        string origen = "BASE DE DATOS";

        var cachedData = await _cache.GetStringAsync(CacheKeyListado);
        if (!string.IsNullOrEmpty(cachedData))
        {
            try
            {
                incidencias = JsonSerializer.Deserialize<List<Incidencia>>(cachedData);
                if (incidencias != null)
                {
                    origen = "REDIS (CACHÉ)";
                    _logger.LogInformation(">>> [CACHE HIT] Incidencias obtenidas desde REDIS (Clave: {Clave}) con {Count} registros", CacheKeyListado, incidencias.Count);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Error al deserializar caché de Redis. Se consultará la base de datos.");
            }
        }

        if (incidencias == null)
        {
            incidencias = await _context.Incidencias
                .Where(i => i.Estado == "Abierta")
                .OrderByDescending(i => i.FechaRegistro)
                .ToListAsync();

            _logger.LogInformation(">>> [CACHE MISS] Incidencias obtenidas desde BASE DE DATOS SQLITE. Almacenando en Redis (Clave: {Clave}) por 60 segundos.", CacheKeyListado);

            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
            };

            var serialized = JsonSerializer.Serialize(incidencias);
            await _cache.SetStringAsync(CacheKeyListado, serialized, cacheOptions);
        }

        ViewBag.OrigenLectura = origen;
        return View(incidencias);
    }

    // POST: /Operaciones/CerrarIncidencia/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CerrarIncidencia(int id)
    {
        var incidencia = await _context.Incidencias.FindAsync(id);
        if (incidencia != null && incidencia.Estado == "Abierta")
        {
            incidencia.Estado = "Cerrada";
            await _context.SaveChangesAsync();
            _logger.LogInformation("Incidencia {Id} cerrada satisfactoriamente en base de datos", id);

            // Al cerrar una incidencia, invalidar la clave del listado antes de volver a consultarlo
            await _cache.RemoveAsync(CacheKeyListado);
            _logger.LogInformation(">>> [CACHE INVALIDATED] Clave de listado {Clave} invalidada en Redis tras el cierre de incidencia {Id}", CacheKeyListado, id);
            
            TempData["Mensaje"] = $"Incidencia #{id} cerrada exitosamente e invalidada de la caché de Redis.";
        }

        return RedirectToAction(nameof(Incidencias));
    }

    // GET: /Operaciones/ObtenerIncidenciasJson
    [HttpGet]
    public async Task<IActionResult> ObtenerIncidenciasJson()
    {
        var incidencias = await _context.Incidencias
            .Where(i => i.Estado == "Abierta")
            .OrderByDescending(i => i.FechaRegistro)
            .ToListAsync();

        return Json(incidencias);
    }
}
