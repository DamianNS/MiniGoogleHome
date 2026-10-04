using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SmartHome.Device;

/// <summary>Comandos de línea de comandos: <c>login</c> y <c>logout</c>.</summary>
public static class AuthCommands
{
    public static async Task<int> LoginAsync(BackendHubOptions options, ITokenStore store)
    {
        Console.Write("Usuario: ");
        var usuario = Console.ReadLine()?.Trim();
        Console.Write("Password: ");
        var password = ReadPassword();

        if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(password))
        {
            Console.Error.WriteLine("Usuario y password son obligatorios.");
            return 1;
        }

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            using var response = await http.PostAsJsonAsync(options.LoginUri, new { usuario, password });

            if (!response.IsSuccessStatusCode)
            {
                Console.Error.WriteLine(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest
                    ? "Credenciales inválidas."
                    : $"El backend respondió {(int)response.StatusCode}.");
                return 1;
            }

            var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (body is null || string.IsNullOrEmpty(body.AccessToken))
            {
                Console.Error.WriteLine("Respuesta de login inválida.");
                return 1;
            }

            store.Save(new StoredToken(body.AccessToken, body.RefreshToken ?? string.Empty));
            Console.WriteLine("Login correcto. Token guardado.");
            return 0;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Console.Error.WriteLine($"No se pudo contactar al backend ({options.BaseUrl}).");
            return 1;
        }
    }

    public static int Logout(ITokenStore store)
    {
        store.Clear();
        Console.WriteLine("Token eliminado.");
        return 0;
    }

    private static string ReadPassword()
    {
        if (Console.IsInputRedirected)
        {
            return Console.ReadLine() ?? string.Empty;
        }

        var sb = new System.Text.StringBuilder();
        ConsoleKeyInfo key;
        while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
        {
            if (key.Key == ConsoleKey.Backspace)
            {
                if (sb.Length > 0) sb.Length--;
            }
            else if (!char.IsControl(key.KeyChar))
            {
                sb.Append(key.KeyChar);
            }
        }

        Console.WriteLine();
        return sb.ToString();
    }

    private sealed record LoginResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken);
}
