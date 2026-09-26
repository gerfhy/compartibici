using System.ComponentModel.DataAnnotations;

namespace CompartiBici.Models;

public class Incidencia
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Estación")]
    public string Estacion { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Descripción")]
    public string Descripcion { get; set; } = string.Empty;

    [Required]
    public string Prioridad { get; set; } = "Media"; // Alta, Media, Baja

    [Required]
    public string Estado { get; set; } = "Abierta"; // Abierta, Cerrada

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
}
