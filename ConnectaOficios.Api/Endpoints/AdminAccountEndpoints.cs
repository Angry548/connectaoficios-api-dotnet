using ConnectaOficios.Api.DTOs.Admins;
using ConnectaOficios.Api.Services.Admins;

namespace ConnectaOficios.Api.Endpoints;

public static class AdminAccountEndpoints
{
    public static void AddAdminAccountEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/api/admin/accounts")
            .WithTags("Admin Accounts")
            .RequireAuthorization("AdministradorPrincipal");

        // GET: /api/admin/accounts
        group.MapGet("/", async (
            IAdminServices adminServices) =>
        {
            var administradores = await adminServices.GetAll();

            return Results.Ok(administradores);
        })
        .WithName("GetAdminAccounts");

        // GET: /api/admin/accounts/{id}
        group.MapGet("/{id:int}", async (
            int id,
            IAdminServices adminServices) =>
        {
            var administrador = await adminServices.GetById(id);

            if (administrador == null)
            {
                return Results.NotFound(new
                {
                    message = "Cuenta administrativa no encontrada."
                });
            }

            return Results.Ok(administrador);
        })
        .WithName("GetAdminAccountById");

        // POST: /api/admin/accounts
        group.MapPost("/", async (
            AdminAccountRequest request,
            IAdminServices adminServices) =>
        {
            if (request.RolId != 3 &&
                request.RolId != 4)
            {
                return Results.BadRequest(new
                {
                    message =
                        "Solo se pueden crear cuentas con rol Administrador o AdministradorPrincipal."
                });
            }

            var administrador = await adminServices.Create(request);

            if (administrador == null)
            {
                return Results.Conflict(new
                {
                    message =
                        "No fue posible crear la cuenta administrativa. Verifique que el correo no esté registrado."
                });
            }

            return Results.Created(
                $"/api/admin/accounts/{administrador.Id}",
                administrador
            );
        })
        .WithName("CreateAdminAccount");
    }
}