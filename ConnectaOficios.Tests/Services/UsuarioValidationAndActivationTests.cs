using ConnectaOficios.Api.DTOs.Users;
using ConnectaOficios.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ConnectaOficios.Tests.Services;

// Angel Mate: gaps in CON-251 and CON-259; existing tests remain unchanged.
public class UsuarioValidationAndActivationTests
{
    [Theory]
    [Trait("Owner", "Angel Mate")]
    [Trait("Jira", "CON-251")]
    [InlineData("", "registro@example.test", "Synthetic-123!")]
    [InlineData("   ", "registro@example.test", "Synthetic-123!")]
    [InlineData("Registro", "", "Synthetic-123!")]
    [InlineData("Registro", "invalid", "Synthetic-123!")]
    [InlineData("Registro", "registro@example.test", "")]
    [InlineData("Registro", "registro@example.test", "12345")]
    public async Task Register_InvalidRequiredField_DoesNotPersistUser(
        string name, string email, string password)
    {
        using var fixture = new ServiceFixture();

        var result = await fixture.Users.Register(new UserRequest
        {
            Nombre = name, Correo = email, Password = password, RolId = 1
        });

        Assert.Null(result);
        fixture.Db.ChangeTracker.Clear();
        Assert.Empty(await fixture.Db.Usuarios.ToListAsync());
    }

    [Theory]
    [Trait("Owner", "Angel Mate")]
    [Trait("Jira", "CON-259")]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ChangeStatus_InactivePublicAccount_PersistsActivation(int role)
    {
        using var fixture = new ServiceFixture();
        var user = await fixture.Add(role, active: false);
        Assert.Equal(EstadoUsuario.Inactivo, user.Estado);

        var result = await fixture.Users.ChangeStatus(user.Id, (int)EstadoUsuario.Activo);

        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        fixture.Db.ChangeTracker.Clear();
        var stored = await fixture.Db.Usuarios.SingleAsync(u => u.Id == user.Id);
        Assert.Equal(EstadoUsuario.Activo, stored.Estado);
        Assert.Equal(role, stored.RolId);
        Assert.NotNull(stored.FechaActualizacion);
    }
}
