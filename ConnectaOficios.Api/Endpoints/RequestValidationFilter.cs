using System.ComponentModel.DataAnnotations;

namespace ConnectaOficios.Api.Endpoints;

// Minimal APIs in .NET 8 do not automatically evaluate DataAnnotations.
public sealed class RequestValidationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        foreach (var argument in context.Arguments)
        {
            if (argument?.GetType().Namespace?.StartsWith("ConnectaOficios.Api.DTOs", StringComparison.Ordinal) != true)
                continue;
            var results = new List<ValidationResult>();
            if (!Validator.TryValidateObject(argument, new ValidationContext(argument), results, true))
                return Results.ValidationProblem(results
                    .SelectMany(r => r.MemberNames.DefaultIfEmpty("request").Select(m => (m, r.ErrorMessage!)))
                    .GroupBy(x => x.m).ToDictionary(g => g.Key, g => g.Select(x => x.Item2).ToArray()));
        }
        return await next(context);
    }
}
