using System.Net;
using System.Net.Http.Json;
using ConnectaOficios.Domain.Data;
using ConnectaOficios.Domain.Entities;
using ConnectaOficios.Domain.Enums;
using ConnectaOficios.Api.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConnectaOficios.Tests.Integration;

public sealed class LocalApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        connection.Open();
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Key", "integration-local-only-key-with-more-than-32-bytes");
        builder.UseSetting("Jwt:Issuer", "local-tests");
        builder.UseSetting("Jwt:Audience", "local-clients");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "integration-local-only-key-with-more-than-32-bytes",
            ["Jwt:Issuer"] = "local-tests", ["Jwt:Audience"] = "local-clients",
            ["LocalTesting:SeedEnabled"] = "false", ["LocalTesting:EmailEnabled"] = "false",
            ["BootstrapAdmin:Correo"] = "", ["BootstrapAdmin:Password"] = "", ["Resend:ApiKey"] = "local-unused"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(connection));
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
        });
    }
    public async Task<string> Token(int role, bool active = true)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new Usuario { Nombre = "Local", Correo = $"{Guid.NewGuid():N}@connectaoficios.local", RolId = role,
            PasswordHash = "unused", Estado = active ? EstadoUsuario.Activo : EstadoUsuario.Inactivo, FechaCreacion = DateTime.UtcNow };
        db.Add(user); await db.SaveChangesAsync(); await db.Entry(user).Reference(x => x.Rol).LoadAsync();
        return scope.ServiceProvider.GetRequiredService<IJwtService>().GenerateToken(user);
    }
    protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing) connection.Dispose(); }
}

public class ApiSecurityTests
{
    [Theory]
    [InlineData(1, "/api/admin/users", 403)] [InlineData(2, "/api/admin/users", 403)]
    [InlineData(3, "/api/admin/accounts/", 403)] [InlineData(4, "/api/admin/accounts/", 200)]
    [InlineData(3, "/api/admin/users?page=1&pageSize=10", 200)]
    public async Task Roles_EnforceAdministrativeBoundaries(int role, string path, int status)
    {
        using var factory = new LocalApiFactory(); using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", await factory.Token(role));
        Assert.Equal(status, (int)(await client.GetAsync(path)).StatusCode);
    }
    [Theory]
    [InlineData(null)] [InlineData("invalid.token.signature")]
    public async Task MissingOrInvalidToken_Returns401(string? token)
    {
        using var factory = new LocalApiFactory(); using var client = factory.CreateClient();
        if (token != null) client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/users")).StatusCode);
    }
    [Fact]
    public async Task InactiveUser_WithSignedToken_Returns401()
    {
        using var factory = new LocalApiFactory(); using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", await factory.Token(4, false));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/users")).StatusCode);
    }
    [Fact]
    public async Task ModifiedSignature_Returns401()
    {
        using var factory = new LocalApiFactory(); using var client = factory.CreateClient();
        var parts = (await factory.Token(4)).Split('.');
        parts[2] = (parts[2][0] == 'A' ? "B" : "A") + parts[2][1..];
        client.DefaultRequestHeaders.Authorization = new("Bearer", string.Join('.', parts));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/users")).StatusCode);
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task PreviouslyIssuedToken_RejectsDeactivationOrRoleChange(bool deactivate)
    {
        using var factory = new LocalApiFactory(); using var client = factory.CreateClient();
        var token = await factory.Token(4);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Usuarios.SingleAsync();
            if (deactivate) user.Estado = EstadoUsuario.Inactivo;
            else user.RolId = 1;
            await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/users")).StatusCode);
    }
    [Fact]
    public async Task CurrentAccount_ReturnsIdentityWithoutPhoto()
    {
        using var factory = new LocalApiFactory(); using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", await factory.Token(1));
        var response = await client.GetAsync("/api/users/me"); response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(json.GetProperty("id").GetInt32() > 0); Assert.Equal("Cliente", json.GetProperty("rol").GetString());
        Assert.False(json.TryGetProperty("fotoUrl", out _));
    }
    [Fact]
    public async Task Register_InvalidRequiredFields_Returns400()
    {
        using var factory = new LocalApiFactory(); using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/users/register", new { nombre = "", correo = "invalid", password = "x", rolId = 1 })).StatusCode);
    }
}
