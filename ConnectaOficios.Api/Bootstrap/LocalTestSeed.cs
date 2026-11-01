using ConnectaOficios.Domain.Data;
using ConnectaOficios.Domain.Entities;
using ConnectaOficios.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ConnectaOficios.Api.Bootstrap;

public static class LocalTestSeed
{
    public static async Task RunAsync(IServiceProvider services, IConfiguration configuration, IHostEnvironment environment)
    {
        if (!configuration.GetValue<bool>("LocalTesting:SeedEnabled")) return;
        if (!environment.IsEnvironment("Docker"))
            throw new InvalidOperationException("LocalTesting seed solo se admite en Docker local.");
        var connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(configuration.GetConnectionString("DefaultConnection"));
        if (connection.DataSource != "sqlserver,1433" || connection.InitialCatalog != "ConnectaOficiosDb")
            throw new InvalidOperationException("Seed rechazado: destino distinto de SQL Docker local.");
        var password = configuration["LocalTesting:Password"];
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
            throw new InvalidOperationException("Configurar LocalTesting:Password local de al menos 12 caracteres.");
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var accounts = new[] { (1, "cliente", "Cliente"), (2, "trabajador", "Trabajador"), (3, "admin", "Administrador"), (4, "adminprincipal", "AdministradorPrincipal") };
        foreach (var (roleId, prefix, name) in accounts)
        {
            var email = $"{prefix}.test@connectaoficios.local";
            if (await db.Usuarios.AnyAsync(u => u.Correo == email)) continue;
            db.Usuarios.Add(new Usuario { Nombre = $"{name} local", Correo = email, RolId = roleId,
                Estado = EstadoUsuario.Activo, PasswordHash = BCrypt.Net.BCrypt.HashPassword(password), FechaCreacion = DateTime.UtcNow });
        }
        await db.SaveChangesAsync();
    }
}
