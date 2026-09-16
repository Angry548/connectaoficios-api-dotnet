using ConnectaOficios.Domain.Data;
using ConnectaOficios.Domain.Entities;
using ConnectaOficios.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ConnectaOficios.Api.Bootstrap;

public static class AdminBootstrapService
{
    public static async Task CreateInitialAdminAsync(
        IServiceProvider services,
        IConfiguration configuration)
    {
        using var scope = services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        // Si ya existe un Administrador Principal,
        // no se crea otro automáticamente.
        var principalExists = await db.Usuarios
            .AnyAsync(u => u.RolId == 4);

        if (principalExists)
        {
            return;
        }

        var nombre = configuration["BootstrapAdmin:Nombre"];
        var correo = configuration["BootstrapAdmin:Correo"];
        var password = configuration["BootstrapAdmin:Password"];

        // Si las credenciales iniciales no están configuradas,
        // no se crea ninguna cuenta.
        if (string.IsNullOrWhiteSpace(nombre) ||
            string.IsNullOrWhiteSpace(correo) ||
            string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        // Evitar duplicar el correo.
        var emailExists = await db.Usuarios
            .AnyAsync(u => u.Correo == correo);

        if (emailExists)
        {
            return;
        }

        var principal = new Usuario
        {
            Nombre = nombre.Trim(),
            Correo = correo.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            RolId = 4,
            Estado = EstadoUsuario.Activo,
            FechaCreacion = DateTime.UtcNow
        };

        await db.Usuarios.AddAsync(principal);
        await db.SaveChangesAsync();
    }
}