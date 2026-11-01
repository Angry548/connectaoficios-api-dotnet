using AutoMapper;
using ConnectaOficios.Api.Mappings;
using ConnectaOficios.Api.Security;
using ConnectaOficios.Api.Services.Email;
using ConnectaOficios.Api.Services.Users;
using ConnectaOficios.Api.Services.Admins;
using ConnectaOficios.Domain.Data;
using ConnectaOficios.Domain.Entities;
using ConnectaOficios.Domain.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ConnectaOficios.Tests.Services;

public sealed class ServiceFixture : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    public ApplicationDbContext Db { get; }
    public Mock<IEmailService> Email { get; } = new();
    public UserServices Users { get; }
    public AdminServices Admins { get; }
    public ServiceFixture()
    {
        connection.Open();
        Db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        Db.Database.EnsureCreated(); // Isolated test database only; Docker uses SQL Server migrations.
        var mapper = new MapperConfiguration(c => c.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var jwt = new Mock<IJwtService>();
        jwt.Setup(x => x.GenerateToken(It.IsAny<Usuario>())).Returns("local-test-token");
        Users = new UserServices(Db, mapper, jwt.Object, Mock.Of<IHostEnvironment>(), Email.Object);
        Admins = new AdminServices(Db, mapper);
    }
    public async Task<Usuario> Add(int role = 1, bool active = true)
    {
        var user = new Usuario { Nombre = "Usuario local", Correo = $"{Guid.NewGuid():N}@connectaoficios.local", RolId = role,
            Estado = active ? EstadoUsuario.Activo : EstadoUsuario.Inactivo,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("LocalOnly-123!", workFactor: 4), FechaCreacion = DateTime.UtcNow };
        Db.Usuarios.Add(user);
        await Db.SaveChangesAsync();
        return user;
    }
    public void Dispose() { Db.Dispose(); connection.Dispose(); }
}
