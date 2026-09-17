using ConnectaOficios.Api.Bootstrap;
using ConnectaOficios.Api.Endpoints;
using ConnectaOficios.Api.Mappings;
using ConnectaOficios.Api.Security;
using ConnectaOficios.Api.Services.Users;
using ConnectaOficios.Api.Services.Admins;
using ConnectaOficios.Domain.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;
using Resend;
using ConnectaOficios.Api.Services.Email;

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
builder.Services.AddScoped<IAdminServices, AdminServices>();
builder.Services.AddScoped<IJwtService, JwtService>();

// Base de datos SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

// Swagger
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Ingrese el token JWT."
    });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
});

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

builder.Services.AddOptions();

builder.Services.AddHttpClient<ResendClient>();

builder.Services.Configure<ResendClientOptions>(options =>
{
    options.ApiToken =
        builder.Configuration["Resend:ApiKey"]
        ?? throw new InvalidOperationException(
            "No se ha configurado Resend:ApiKey."
        );
});

builder.Services.AddTransient<IResend, ResendClient>();

builder.Services.AddScoped<IEmailService, EmailService>();

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

// Crear Administrador Principal inicial si todavía no existe.
await AdminBootstrapService.CreateInitialAdminAsync(
    app.Services,
    app.Configuration
);

app.Run();