using System.Net;
using System.Net.Http.Json;
using ConnectaOficios.Api.DTOs.Users;
using ConnectaOficios.Domain.Data;
using ConnectaOficios.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ConnectaOficios.Tests.Integration;

public class CurrentUserUpdateTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task EachRole_CanUpdateOwnPersonalFields(int role)
    {
        using var factory = new LocalApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", await factory.Token(role));

        var response = await client.PutAsJsonAsync("/api/users/me", new
        {
            nombre = "Nombre actualizado", correo = "nuevo@example.test", telefono = "70001234"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Usuarios.SingleAsync();
        Assert.Equal("Nombre actualizado", user.Nombre);
        Assert.Equal("nuevo@example.test", user.Correo);
        Assert.Equal("70001234", user.Telefono);
        Assert.Equal(role, user.RolId);
        Assert.Equal(EstadoUsuario.Activo, user.Estado);
    }

    [Fact]
    public async Task JwtIdentity_IgnoresOtherUserAndProtectedFields()
    {
        using var factory = new LocalApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", await factory.Token(1));
        await factory.Token(2);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var before = await db.Usuarios.AsNoTracking().OrderBy(u => u.Id).ToArrayAsync();

        var response = await client.PutAsJsonAsync($"/api/users/me?id={before[1].Id}", new
        {
            id = before[1].Id, userId = before[1].Id, nombre = "Solo mi cuenta",
            rolId = 4, rol = "AdministradorPrincipal", estado = 0,
            password = "Ignored-123!", passwordHash = "ignored"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = await db.Usuarios.AsNoTracking().OrderBy(u => u.Id).ToArrayAsync();
        Assert.Equal("Solo mi cuenta", after[0].Nombre);
        Assert.Equal(before[1].Nombre, after[1].Nombre);
        for (var i = 0; i < before.Length; i++)
        {
            Assert.True(before[i].Id == after[i].Id);
            Assert.True(before[i].RolId == after[i].RolId);
            Assert.True(before[i].Estado == after[i].Estado);
            Assert.True(before[i].PasswordHash == after[i].PasswordHash);
            Assert.Equal(before[i].Correo, after[i].Correo);
        }
    }

    [Fact]
    public async Task DuplicateEmail_ReturnsConflictWithoutPersistingChanges()
    {
        using var factory = new LocalApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", await factory.Token(1));
        await factory.Token(2);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var before = await db.Usuarios.AsNoTracking().OrderBy(u => u.Id).ToArrayAsync();

        var response = await client.PutAsJsonAsync("/api/users/me", new
        {
            nombre = "No debe persistir", correo = before[1].Correo
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var user = await db.Usuarios.AsNoTracking().SingleAsync(u => u.Id == before[0].Id);
        Assert.Equal(before[0].Nombre, user.Nombre);
        Assert.Equal(before[0].Correo, user.Correo);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrInactiveAccount_RejectsPreviouslyIssuedJwt(bool delete)
    {
        using var factory = new LocalApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", await factory.Token(1));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Usuarios.SingleAsync();
        if (delete) db.Usuarios.Remove(user);
        else user.Estado = EstadoUsuario.Inactivo;
        await db.SaveChangesAsync();

        var response = await client.PutAsJsonAsync("/api/users/me", new { nombre = "Rechazado" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        db.ChangeTracker.Clear();
        Assert.False(await db.Usuarios.AnyAsync(u => u.Nombre == "Rechazado"));
    }

    [Fact]
    public async Task MissingJwt_ReturnsUnauthorized()
    {
        using var factory = new LocalApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PutAsJsonAsync("/api/users/me", new { nombre = "Rechazado" })).StatusCode);
    }

    [Fact]
    public void RequestDto_ContainsOnlyPersonalFields()
    {
        Assert.Equal(new[] { "Correo", "Nombre", "Telefono" },
            typeof(UserUpdateRequest).GetProperties().Select(p => p.Name).OrderBy(n => n).ToArray());
    }
}
