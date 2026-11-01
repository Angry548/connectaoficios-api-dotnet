using ConnectaOficios.Api.DTOs.Users;
using ConnectaOficios.Domain.Enums;
using Moq;

namespace ConnectaOficios.Tests.Services;

public class UsuarioServiceTests
{
    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task Register_ValidPublicRole_ReturnsIdentity(int role)
    {
        using var f = new ServiceFixture();
        var result = await f.Users.Register(new UserRequest { Nombre = "Test", Correo = "nuevo@connectaoficios.local", Password = "LocalOnly-123!", RolId = role });
        Assert.NotNull(result); Assert.True(result.Id > 0); Assert.Equal(role, result.RolId); Assert.NotEmpty(result.Rol);
    }
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public async Task Register_AdminRole_Rejected(int role)
    {
        using var f = new ServiceFixture();
        Assert.Null(await f.Users.Register(new UserRequest { RolId = role }));
    }
    [Fact]
    public async Task Register_DuplicateEmail_Rejected()
    {
        using var f = new ServiceFixture(); var u = await f.Add();
        Assert.Null(await f.Users.Register(new UserRequest { Nombre = "Test", Correo = u.Correo, Password = "LocalOnly-123!", RolId = 1 }));
    }
    [Theory]
    [InlineData(true, "incorrecta", false)] [InlineData(false, "LocalOnly-123!", false)] [InlineData(true, "LocalOnly-123!", true)]
    public async Task Login_ValidatesPasswordAndState(bool active, string password, bool success)
    {
        using var f = new ServiceFixture(); var u = await f.Add(active: active);
        Assert.Equal(success, await f.Users.Login(new LoginRequest { Correo = u.Correo, Password = password }) != null);
    }
    [Fact]
    public async Task GetById_Missing_ReturnsNull()
    {
        using var f = new ServiceFixture(); Assert.Null(await f.Users.GetById(999));
    }
    [Fact]
    public async Task GetById_ContainsPortalIdentity()
    {
        using var f = new ServiceFixture(); var u = await f.Add(); var result = await f.Users.GetById(u.Id);
        Assert.NotNull(result); Assert.Equal(u.Id, result.Id); Assert.Equal(u.Correo, result.Correo); Assert.Equal("Cliente", result.Rol);
    }
    [Fact]
    public async Task List_FiltersAndPaginates()
    {
        using var f = new ServiceFixture();
        for (var i = 0; i < 12; i++) await f.Add();
        await f.Add(3);
        var first = await f.Users.GetAll(nombre: "Usuario", correo: "connectaoficios.local", rolId: 1, estado: 1);
        var second = await f.Users.GetAll(page: 2);
        Assert.Equal(12, first.TotalItems); Assert.Equal(10, first.Items.Count()); Assert.Equal(2, first.TotalPages);
        Assert.Equal(2, second.Items.Count()); Assert.Empty(first.Items.Select(u => u.Id).Intersect(second.Items.Select(u => u.Id)));
        Assert.Empty((await f.Users.GetAll(nombre: "inexistente")).Items);
    }
    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task ChangeStatus_PublicUser(int status)
    {
        using var f = new ServiceFixture(); var u = await f.Add();
        Assert.NotNull(await f.Users.ChangeStatus(u.Id, status)); Assert.Equal((EstadoUsuario)status, u.Estado);
    }
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public async Task ChangeStatus_CannotModifyAdmin(int role)
    {
        using var f = new ServiceFixture(); var u = await f.Add(role);
        Assert.Null(await f.Users.ChangeStatus(u.Id, 2));
    }
    [Fact]
    public async Task Search_ContainsRoleAndIsBounded()
    {
        using var f = new ServiceFixture(); await f.Add();
        var result = await f.Users.Search("Usuario"); Assert.Equal("Cliente", Assert.Single(result).Rol);
    }
}
