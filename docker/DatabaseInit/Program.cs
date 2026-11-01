using ConnectaOficios.Domain.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

// Herramienta exclusiva de Docker. No arranca el host ni el bootstrap de la API.
if (args is ["--health"])
{
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    using var response = await client.GetAsync("http://127.0.0.1:8080/health");
    response.EnsureSuccessStatusCode();
    return;
}

var checkModelOnly = args is ["--check-model"];
if (args.Length != 0 && !checkModelOnly)
    throw new InvalidOperationException("Argumentos no admitidos.");

var connection = new SqlConnectionStringBuilder(
    Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? throw new InvalidOperationException("Falta la conexion SQL local de Docker."));

// Impide aplicar migraciones accidentalmente a Somee, Azure u otra base.
if (connection.DataSource != "sqlserver,1433"
    || connection.InitialCatalog != "ConnectaOficiosDb"
    || connection.UserID != "sa"
    || connection.IntegratedSecurity)
    throw new InvalidOperationException("Solo se admite sqlserver,1433 / ConnectaOficiosDb / sa.");

var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer(connection.ConnectionString, sql => sql.EnableRetryOnFailure())
    .Options;
await using var db = new ApplicationDbContext(options);

// Tambien permite validar el modelo sin abrir ninguna conexion SQL.
if (db.Database.HasPendingModelChanges())
    throw new InvalidOperationException("El modelo difiere de las migraciones. Revisar antes de inicializar.");

if (checkModelOnly)
{
    Console.WriteLine("Modelo y migraciones coinciden; no se abrio ninguna conexion SQL.");
    return;
}

Console.WriteLine("Aplicando migraciones existentes a SQL Server Docker local...");
await db.Database.MigrateAsync();
Console.WriteLine("Base local inicializada.");
