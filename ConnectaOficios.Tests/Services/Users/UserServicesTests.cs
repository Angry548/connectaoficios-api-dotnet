using AutoMapper;
using ConnectaOficios.Api.DTOs.Users;
using ConnectaOficios.Api.Mappings;
using ConnectaOficios.Api.Security;
using ConnectaOficios.Api.Services.Email;
using ConnectaOficios.Api.Services.Users;
using ConnectaOficios.Domain.Data;
using ConnectaOficios.Domain.Entities;
using ConnectaOficios.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ConnectaOficios.Tests.Services.Users;

public class UserServicesTests
{
    [Fact]
    public async Task Register_WithRoleOtherThanOneOrTwo_ReturnsNull()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().Options;
        using var dbContext = new ApplicationDbContext(options);
        var mapper = new Mock<IMapper>();
        var jwtService = new Mock<IJwtService>();
        var environment = new Mock<IHostEnvironment>();
        var emailService = new Mock<IEmailService>();
        var service = new UserServices(
            dbContext,
            mapper.Object,
            jwtService.Object,
            environment.Object,
            emailService.Object);
        var user = new UserRequest { RolId = 3 };

        // Act
        var result = await service.Register(user);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Register_WithExistingEmail_ReturnsNull()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"UserServicesTests-{Guid.NewGuid()}")
            .Options;
        await using var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var role = await dbContext.Roles.SingleAsync(r => r.Id == 1);
        dbContext.Usuarios.Add(new Usuario
        {
            Id = 42,
            Nombre = "Usuario existente",
            Correo = "existente@example.com",
            PasswordHash = "hash-ficticio",
            Telefono = "555-0100",
            RolId = role.Id,
            Rol = role,
            Estado = EstadoUsuario.Activo,
            FechaCreacion = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var mapper = new Mock<IMapper>();
        var jwtService = new Mock<IJwtService>();
        var environment = new Mock<IHostEnvironment>();
        var emailService = new Mock<IEmailService>();
        var service = new UserServices(
            dbContext,
            mapper.Object,
            jwtService.Object,
            environment.Object,
            emailService.Object);
        var user = new UserRequest
        {
            Nombre = "Otro usuario",
            Correo = "existente@example.com",
            Password = "contraseña-ficticia",
            Telefono = "555-0101",
            RolId = 1
        };

        // Act
        var result = await service.Register(user);

        // Assert
        Assert.Null(result);
        Assert.Equal(1, await dbContext.Usuarios.CountAsync());
        mapper.Verify(m => m.Map<Usuario>(It.IsAny<UserRequest>()), Times.Never);
    }

    [Fact]
    public async Task Register_WithValidUser_CreatesUserSuccessfully()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"UserServicesTests-{Guid.NewGuid()}")
            .Options;
        await using var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var mapperConfiguration = new MapperConfiguration(
            configuration => configuration.AddProfile<MappingProfile>(),
            NullLoggerFactory.Instance);
        var mapper = mapperConfiguration.CreateMapper();
        var jwtService = new Mock<IJwtService>();
        var environment = new Mock<IHostEnvironment>();
        var emailService = new Mock<IEmailService>();
        var service = new UserServices(
            dbContext,
            mapper,
            jwtService.Object,
            environment.Object,
            emailService.Object);
        const string password = "contraseña-segura-ficticia";
        var user = new UserRequest
        {
            Nombre = "Usuario de prueba",
            Correo = "nuevo@example.com",
            Password = password,
            Telefono = "555-0102",
            RolId = 1
        };
        var beforeRegistration = DateTime.UtcNow;

        // Act
        var result = await service.Register(user);
        var afterRegistration = DateTime.UtcNow;

        // Assert
        Assert.NotNull(result);

        var storedUser = await dbContext.Usuarios.SingleAsync();
        Assert.Equal(1, await dbContext.Usuarios.CountAsync());
        Assert.Equal(user.Correo, storedUser.Correo);
        Assert.Equal(user.Nombre, storedUser.Nombre);
        Assert.Equal(user.Telefono, storedUser.Telefono);
        Assert.Equal(1, storedUser.RolId);
        Assert.Equal(EstadoUsuario.Activo, storedUser.Estado);
        Assert.InRange(storedUser.FechaCreacion, beforeRegistration, afterRegistration);
        Assert.NotEqual(password, storedUser.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(password, storedUser.PasswordHash));

        Assert.Equal(storedUser.Id, result.Id);
        Assert.Equal(user.Nombre, result.Nombre);
        Assert.Equal(user.Correo, result.Correo);
        Assert.Equal(user.Telefono, result.Telefono);
        Assert.Equal(1, result.RolId);
        Assert.Equal("Cliente", result.Rol);
        Assert.Equal(EstadoUsuario.Activo.ToString(), result.Estado);
        Assert.Equal(storedUser.FechaCreacion, result.FechaCreacion);
        jwtService.Verify(
            serviceMock => serviceMock.GenerateToken(It.IsAny<Usuario>()),
            Times.Never);
        emailService.Verify(
            serviceMock => serviceMock.SendPasswordResetEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }
}
