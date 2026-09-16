namespace ConnectaOficios.Api.Endpoints;

public static class Startup
{
    public static void AddEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.AddUserEndpoints();
    }
}