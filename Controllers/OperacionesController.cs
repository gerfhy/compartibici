using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CompartiBici.Data;
using CompartiBici.Models;
using CompartiBici.Services;

namespace CompartiBici.Controllers;

public class OperacionesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IPieSocketService _pieSocketService;
    private readonly ILogger<OperacionesController> _logger;

    public OperacionesController(
        ApplicationDbContext context,
        IPieSocketService pieSocketService,
        ILogger<OperacionesController> logger)
    {
        _context = context;
        _pieSocketService = pieSocketService;
        _logger = logger;
    }

    // GET: /Operaciones/Incidencias
    public async Task<IActionResult> Incidencias()
    {
        var incidencias = await _context.Incidencias
            .Where(i => i.Estado == "Abierta")
            .OrderByDescending(i => i.FechaRegistro)
            .ToListAsync();

        ViewBag.PieSocketCluster = _pieSocketService.ClusterId;
        ViewBag.PieSocketApiKey = _pieSocketService.ApiKey;
        ViewBag.PieSocketRoom = _pieSocketService.RoomId;

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
            // 1. Guardar primero el estado en la base de datos
            incidencia.Estado = "Cerrada";
            await _context.SaveChangesAsync();
            _logger.LogInformation("Incidencia {Id} cerrada satisfactoriamente en base de datos", id);

            // 2. Publicar desde el servidor el evento IncidenciaActualizada con Id y Estado en PieHost
            await _pieSocketService.PublicarEventoAsync("IncidenciaActualizada", new
            {
                Id = id,
                Estado = "Cerrada"
            });
            
            TempData["Mensaje"] = $"Incidencia #{id} cerrada y notificada en tiempo real.";
        }

        return RedirectToAction(nameof(Incidencias));
    }

    // GET: /Operaciones/ObtenerIncidenciasJson (Utilizado al reconectar para consultar estado vigente)
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
