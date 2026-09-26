using Microsoft.AspNetCore.Identity;
using CompartiBici.Models;

namespace CompartiBici.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // 1. Crear rol Supervisor
        var roleName = "Supervisor";
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }

        // 2. Crear o actualizar usuario Supervisor por defecto
        var supervisorEmail = "supervisor@compartibici.com";
        var supervisorUser = await userManager.FindByEmailAsync(supervisorEmail);
        if (supervisorUser == null)
        {
            supervisorUser = new IdentityUser
            {
                UserName = supervisorEmail,
                Email = supervisorEmail,
                EmailConfirmed = true,
                LockoutEnabled = false
            };
            var result = await userManager.CreateAsync(supervisorUser, "Admin123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(supervisorUser, roleName);
            }
        }
        else
        {
            // Forzar reinicio de contraseña y desbloqueo para garantizar acceso
            supervisorUser.EmailConfirmed = true;
            supervisorUser.LockoutEnabled = false;
            supervisorUser.LockoutEnd = null;
            supervisorUser.AccessFailedCount = 0;
            var token = await userManager.GeneratePasswordResetTokenAsync(supervisorUser);
            await userManager.ResetPasswordAsync(supervisorUser, token, "Admin123!");
            await userManager.UpdateAsync(supervisorUser);

            if (!await userManager.IsInRoleAsync(supervisorUser, roleName))
            {
                await userManager.AddToRoleAsync(supervisorUser, roleName);
            }
        }

        // 3. Crear Incidencias de prueba
        if (!context.Incidencias.Any())
        {
            var incidencias = new List<Incidencia>
            {
                new()
                {
                    Id = 1,
                    Estacion = "Estación Miraflores - Parque Kennedy",
                    Descripcion = "Freno delantero trabado en bicicleta #104",
                    Prioridad = "Alta",
                    Estado = "Abierta",
                    FechaRegistro = DateTime.UtcNow.AddHours(-5)
                },
                new()
                {
                    Id = 2,
                    Estacion = "Estación San Isidro - El Olivar",
                    Descripcion = "Cadena suelta y falta de aire en rueda trasera #205",
                    Prioridad = "Media",
                    Estado = "Abierta",
                    FechaRegistro = DateTime.UtcNow.AddHours(-4)
                },
                new()
                {
                    Id = 3,
                    Estacion = "Estación Barranco - Puente de los Suspiros",
                    Descripcion = "Pedal roto y manillar desalineado #042",
                    Prioridad = "Baja",
                    Estado = "Abierta",
                    FechaRegistro = DateTime.UtcNow.AddHours(-3)
                },
                new()
                {
                    Id = 4,
                    Estacion = "Estación Miraflores - Larcomar",
                    Descripcion = "Batería descargada en bicicleta eléctrica #512",
                    Prioridad = "Alta",
                    Estado = "Abierta",
                    FechaRegistro = DateTime.UtcNow.AddHours(-2)
                },
                new()
                {
                    Id = 5,
                    Estacion = "Estación San Borja - Pentagonito",
                    Descripcion = "Sillín suelto y timbre averiado #318",
                    Prioridad = "Baja",
                    Estado = "Abierta",
                    FechaRegistro = DateTime.UtcNow.AddHours(-1)
                },
                new()
                {
                    Id = 6,
                    Estacion = "Estación Surco - Parque de la Amistad",
                    Descripcion = "Cambio de velocidades trabado #119",
                    Prioridad = "Media",
                    Estado = "Cerrada",
                    FechaRegistro = DateTime.UtcNow.AddHours(-6)
                }
            };

            await context.Incidencias.AddRangeAsync(incidencias);
            await context.SaveChangesAsync();
        }
    }
}
