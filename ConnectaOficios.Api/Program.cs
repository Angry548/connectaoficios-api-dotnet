using ConnectaOficios.Api.Endpoints;
using ConnectaOficios.Api.Mappings;
using ConnectaOficios.Api.Services.Users;
using ConnectaOficios.Api.Security;
using ConnectaOficios.Domain.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Controladores
builder.Services.AddControllers();

// AutoMapper
builder.Services.AddAutoMapper(cfg =>
{
    cfg.AddProfile<MappingProfile>();
});

// Servicios
builder.Services.AddScoped<IUserServices, UserServices>();
builder.Services.AddScoped<IJwtService, JwtService>();

// Base de datos SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Autenticación JWT
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "La clave JWT no está configurada."
    );

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException(
        "El emisor JWT no está configurado."
    );

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException(
        "La audiencia JWT no está configurada."
    );

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)
            ),

            ClockSkew = TimeSpan.Zero
        };
    });

// Autorización basada en roles
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Cliente", policy =>
        policy.RequireRole("Cliente"));

    options.AddPolicy("Trabajador", policy =>
        policy.RequireRole("Trabajador"));

    options.AddPolicy("Administrador", policy =>
        policy.RequireRole(
            "Administrador",
            "AdministradorPrincipal"
        ));

    options.AddPolicy("AdministradorPrincipal", policy =>
        policy.RequireRole("AdministradorPrincipal"));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Endpoints de la API
app.AddEndpoints();

app.Run();