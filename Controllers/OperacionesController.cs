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
    private readonly IPieSocketService _pieSocketService;
    private readonly ILogger<OperacionesController> _logger;

    private const string CacheKeyListado = "incidencias_abiertas_listado";

    public OperacionesController(
        ApplicationDbContext context,
        IAlgoliaSearchService algoliaService,
        IDistributedCache cache,
        IPieSocketService pieSocketService,
        ILogger<OperacionesController> logger)
    {
        _context = context;
        _algoliaService = algoliaService;
        _cache = cache;
        _pieSocketService = pieSocketService;
        _logger = logger;
    }

    // GET: /Operaciones/Incidencias?q=...
    public async Task<IActionResult> Incidencias(string? q)
    {
        // Pasar variables de configuración WebSocket para la conexión del cliente
        ViewBag.PieSocketCluster = _pieSocketService.ClusterId;
        ViewBag.PieSocketApiKey = _pieSocketService.ApiKey;
        ViewBag.PieSocketRoom = _pieSocketService.RoomId;

        // Caso 1: Búsqueda con texto en Algolia (consulta directa al servidor de búsqueda, sin usar caché de Redis)
        if (!string.IsNullOrWhiteSpace(q))
        {
            var hitIds = await _algoliaService.BuscarIncidenciasAsync(q);

            var resultados = await _context.Incidencias
                .Where(i => hitIds.Contains(i.Id) && i.Estado == "Abierta")
                .OrderByDescending(i => i.FechaRegistro)
                .ToListAsync();

            ViewBag.Busqueda = q;
            ViewBag.OrigenLectura = "ALGOLIA DIRECTO";
            _logger.LogInformation(">>> [ALGOLIA SEARCH DIRECTO] Consulta sin caché para '{Query}'. Coincidencias abiertas en BD: {Count}", q, resultados.Count);

            return View(resultados);
        }

        // Caso 2: Listado general habitual cacheado por 60 segundos en Redis
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
                    _logger.LogInformation(">>> [CACHE HIT] Incidencias abiertas obtenidas desde REDIS (Clave: {Clave}) con {Count} registros", CacheKeyListado, incidencias.Count);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Fallo al deserializar caché de Redis. Se consultará la base de datos.");
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
    // Secuencia requerida: 1. Cierre en base -> 2. Invalidación en Redis -> 3. Publicación en PieHost
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CerrarIncidencia(int id)
    {
        var incidencia = await _context.Incidencias.FindAsync(id);
        if (incidencia != null && incidencia.Estado == "Abierta")
        {
            // Paso 1: Cierre y persistencia en base de datos SQLite
            incidencia.Estado = "Cerrada";
            await _context.SaveChangesAsync();
            _logger.LogInformation(">>> [1. DB UPDATE] Incidencia {Id} cerrada y persistida en base de datos SQLite.", id);

            // Paso 2: Invalidación de la clave del listado en Redis antes de volver a consultarlo
            await _cache.RemoveAsync(CacheKeyListado);
            _logger.LogInformation(">>> [2. REDIS INVALIDATION] Clave de listado '{Clave}' invalidada en Redis para asegurar consistencia.", CacheKeyListado);

            // Paso 3: Publicación del evento en tiempo real hacia PieHost
            await _pieSocketService.PublicarEventoAsync("IncidenciaActualizada", new
            {
                Id = id,
                Estado = "Cerrada"
            });
            _logger.LogInformation(">>> [3. PIEHOST WEBSOCKET] Evento IncidenciaActualizada emitido a PieSocket para incidencia {Id}.", id);

            TempData["Mensaje"] = $"Incidencia #{id} cerrada en base de datos, invalidada de Redis y notificada por WebSocket.";
        }

        return RedirectToAction(nameof(Incidencias));
    }

    // GET: /Operaciones/ObtenerIncidenciasJson (Utilizado para reconexión de WebSocket y consulta de estado vigente)
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
