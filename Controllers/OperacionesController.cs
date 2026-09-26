using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CompartiBici.Data;
using CompartiBici.Models;
using CompartiBici.Services;

namespace CompartiBici.Controllers;

public class OperacionesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IAlgoliaSearchService _algoliaService;
    private readonly ILogger<OperacionesController> _logger;

    public OperacionesController(
        ApplicationDbContext context,
        IAlgoliaSearchService algoliaService,
        ILogger<OperacionesController> logger)
    {
        _context = context;
        _algoliaService = algoliaService;
        _logger = logger;
    }

    // GET: /Operaciones/Incidencias?q=...
    public async Task<IActionResult> Incidencias(string? q)
    {
        List<Incidencia> incidencias;

        if (string.IsNullOrWhiteSpace(q))
        {
            // Con búsqueda vacía, presentar la lista habitual
            incidencias = await _context.Incidencias
                .Where(i => i.Estado == "Abierta")
                .OrderByDescending(i => i.FechaRegistro)
                .ToListAsync();
        }
        else
        {
            // El servidor consulta Algolia y muestra solo incidencias abiertas existentes en la base
            var hitIds = await _algoliaService.BuscarIncidenciasAsync(q);

            incidencias = await _context.Incidencias
                .Where(i => hitIds.Contains(i.Id) && i.Estado == "Abierta")
                .OrderByDescending(i => i.FechaRegistro)
                .ToListAsync();

            ViewBag.Busqueda = q;
        }

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
