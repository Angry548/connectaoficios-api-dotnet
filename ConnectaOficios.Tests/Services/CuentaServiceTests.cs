using ConnectaOficios.Api.DTOs.Users;
using Moq;

namespace ConnectaOficios.Tests.Services;

public class CuentaServiceTests
{
    [Fact]
    public async Task UpdateCurrentUser_UpdatesPersonalData()
    {
        using var f = new ServiceFixture(); var u = await f.Add();
        var result = await f.Users.UpdateCurrentUser(u.Id, new UserUpdateRequest { Nombre = "Actualizado" });
        Assert.True(result.Success); Assert.Equal("Actualizado", result.User!.Nombre);
    }
    [Theory]
    [InlineData("LocalOnly-123!", true)] [InlineData("incorrecta", false)]
    public async Task ChangePassword_RequiresCurrentPassword(string password, bool success)
    {
        using var f = new ServiceFixture(); var u = await f.Add();
        var result = await f.Users.ChangePassword(u.Id, new ChangePasswordRequest { CurrentPassword = password, NewPassword = "LocalOnly-New123!" });
        Assert.Equal(success, result.Success);
        Assert.Equal(success, BCrypt.Net.BCrypt.Verify("LocalOnly-New123!", u.PasswordHash));
    }
    [Fact]
    public async Task ResetPassword_UsesOneTimeTokenAndStoresOnlyHash()
    {
        using var f = new ServiceFixture(); var u = await f.Add(); string? token = null;
        f.Email.Setup(x => x.SendPasswordResetEmailAsync(u.Correo, u.Nombre, It.IsAny<string>()))
            .Callback<string, string, string>((_, _, t) => token = t).Returns(Task.CompletedTask);
        await f.Users.RequestPasswordReset(new ForgotPasswordRequest { Correo = u.Correo });
        Assert.NotNull(token); Assert.NotEqual(token, u.PasswordResetTokenHash);
        Assert.True((await f.Users.ResetPassword(new ResetPasswordRequest { Token = token!, NewPassword = "LocalOnly-New123!" })).Success);
        Assert.False((await f.Users.ResetPassword(new ResetPasswordRequest { Token = token!, NewPassword = "LocalOnly-Other123!" })).Success);
    }
}
