using ConnectaOficios.Api.DTOs.Admins;

namespace ConnectaOficios.Tests.Services;

public class AdministradorServiceTests
{
    [Fact]
    public async Task CreateAndUpdateAdministrator()
    {
        using var f = new ServiceFixture();
        var admin = await f.Admins.Create(new AdminAccountRequest { Nombre = "Admin", Correo = "admin@connectaoficios.local", Password = "LocalOnly-123!", RolId = 3 });
        Assert.NotNull(admin); Assert.Equal("Administrador", admin.Rol);
        var result = await f.Admins.Update(admin.Id, new AdminAccountUpdateRequest { Nombre = "Actualizado" });
        Assert.True(result.Success); Assert.Equal("Actualizado", result.Admin!.Nombre);
    }
    [Fact]
    public async Task DisableAndDeleteAdministrator()
    {
        using var f = new ServiceFixture(); var admin = await f.Add(3);
        Assert.True((await f.Admins.ChangeStatus(admin.Id, 2, 999)).Success);
        Assert.True((await f.Admins.Delete(admin.Id, 999)).Success);
        Assert.Null(await f.Admins.GetById(admin.Id));
    }
    [Fact]
    public async Task LastActivePrincipal_CannotBeDisabledOrDeleted()
    {
        using var f = new ServiceFixture(); var admin = await f.Add(4);
        Assert.Equal("LAST_ACTIVE_PRINCIPAL", (await f.Admins.ChangeStatus(admin.Id, 2, 999)).Error);
        Assert.Equal("LAST_ACTIVE_PRINCIPAL", (await f.Admins.Delete(admin.Id, 999)).Error);
    }
    [Fact]
    public async Task Principal_CannotDisableOrDeleteSelf()
    {
        using var f = new ServiceFixture(); var admin = await f.Add(4);
        Assert.Equal("CANNOT_DISABLE_SELF", (await f.Admins.ChangeStatus(admin.Id, 2, admin.Id)).Error);
        Assert.Equal("CANNOT_DELETE_SELF", (await f.Admins.Delete(admin.Id, admin.Id)).Error);
    }
}
