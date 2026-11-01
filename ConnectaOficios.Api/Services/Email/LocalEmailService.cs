using System.Text.Json;

namespace ConnectaOficios.Api.Services.Email;

// Private container outbox; never exposed by an HTTP endpoint or written to logs.
public sealed class LocalEmailService : IEmailService
{
    public async Task SendPasswordResetEmailAsync(string email, string nombre, string resetToken)
    {
        const string directory = "/tmp/connectaoficios-mail";
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, $"{Guid.NewGuid():N}.json"),
            JsonSerializer.Serialize(new { email, nombre, resetToken }));
    }
}
