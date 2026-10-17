using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ConnectaOficios.Api.Security;
using ConnectaOficios.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace ConnectaOficios.Tests.Services.Authentication;

public class JwtServiceTests
{
    [Fact]
    public void GenerateToken_WithValidConfiguration_ReturnsSignedTokenWithUserClaims()
    {
        // Arrange
        const string key = "connectaoficios-unit-tests-secret-key-32-bytes-minimum";
        const string issuer = "connectaoficios-tests";
        const string audience = "connectaoficios-tests-client";
        var configuration = new Mock<IConfiguration>(MockBehavior.Strict);
        configuration.Setup(c => c["Jwt:Key"]).Returns(key);
        configuration.Setup(c => c["Jwt:Issuer"]).Returns(issuer);
        configuration.Setup(c => c["Jwt:Audience"]).Returns(audience);
        var expirationSection = new Mock<IConfigurationSection>();
        expirationSection.Setup(s => s.Value).Returns("30");
        configuration.Setup(c => c.GetSection("Jwt:ExpirationMinutes"))
            .Returns(expirationSection.Object);

        var usuario = new Usuario
        {
            Id = 42,
            Nombre = "Usuario de prueba",
            Correo = "usuario@example.com",
            RolId = 1,
            Rol = new Rol { Id = 1, Nombre = "Cliente" }
        };
        var service = new JwtService(configuration.Object);
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.Zero
        };
        var before = DateTime.UtcNow;

        // Act
        var token = service.GenerateToken(usuario);

        // Assert
        var principal = handler.ValidateToken(token, validationParameters, out var validatedToken);
        var jwt = Assert.IsType<JwtSecurityToken>(validatedToken);
        Assert.Equal(SecurityAlgorithms.HmacSha256, jwt.Header.Alg);
        Assert.Equal("42", principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
        Assert.Equal(usuario.Correo, principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value);
        Assert.Equal("42", principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal(usuario.Nombre, principal.FindFirst(ClaimTypes.Name)?.Value);
        Assert.Equal(usuario.Rol.Nombre, principal.FindFirst(ClaimTypes.Role)?.Value);
        Assert.True(Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value, out _));
        Assert.InRange(jwt.ValidTo, before.AddMinutes(30).AddSeconds(-1), DateTime.UtcNow.AddMinutes(30));
    }
}
