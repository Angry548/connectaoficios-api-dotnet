using ConnectaOficios.Api.DTOs.Statistics;
using ConnectaOficios.Domain.Data;
using Microsoft.EntityFrameworkCore;

namespace ConnectaOficios.Api.Endpoints;

public static class AdminStatisticsEndpoints
{
    public static void AddAdminStatisticsEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup(
                "/api/admin/statistics"
            )
            .WithTags(
                "Admin Statistics"
            )
            .RequireAuthorization(
                "Administrador"
            );

        group.MapGet(
            "/users",
            async (
                ApplicationDbContext db,
                DateTime? fechaDesde,
                DateTime? fechaHasta) =>
            {
                if (
                    fechaDesde.HasValue &&
                    fechaHasta.HasValue &&
                    fechaDesde.Value >
                    fechaHasta.Value
                )
                {
                    return Results.BadRequest(
                        new
                        {
                            message =
                                "La fecha inicial no puede ser posterior a la fecha final."
                        }
                    );
                }

                var query =
                    db.Usuarios
                        .AsNoTracking()
                        .Where(u =>
                            u.RolId == 1 ||
                            u.RolId == 2
                        );

                if (fechaDesde.HasValue)
                {
                    query =
                        query.Where(u =>
                            u.FechaCreacion >=
                            fechaDesde.Value
                        );
                }

                if (fechaHasta.HasValue)
                {
                    query =
                        query.Where(u =>
                            u.FechaCreacion <=
                            fechaHasta.Value
                        );
                }

                var totalUsuarios =
                    await query.CountAsync();

                var totalClientes =
                    await query.CountAsync(
                        u =>
                            u.RolId == 1
                    );

                var totalTrabajadores =
                    await query.CountAsync(
                        u =>
                            u.RolId == 2
                    );

                var response =
                    new UserStatisticsResponse
                    {
                        TotalUsuarios =
                            totalUsuarios,

                        TotalClientes =
                            totalClientes,

                        TotalTrabajadores =
                            totalTrabajadores
                    };

                return Results.Ok(
                    response
                );
            }
        )
        .WithName(
            "GetAdminUserStatistics"
        );
    }
}