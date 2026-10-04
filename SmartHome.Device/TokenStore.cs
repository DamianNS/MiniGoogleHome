using System.Security.Cryptography;
using System.Text.Json;

namespace SmartHome.Device;

public sealed record StoredToken(string AccessToken, string RefreshToken);

public interface ITokenStore
{
    StoredToken? Load();
    void Save(StoredToken token);
    void Clear();
}

/// <summary>
/// Guarda el token en el perfil del usuario del SO.
/// Windows: cifrado con DPAPI (usuario actual). Linux/macOS: archivo en el directorio de
/// configuración del usuario, con los permisos por defecto del sistema.
/// </summary>
public sealed class FileTokenStore : ITokenStore
{
    private readonly string _path;

    public FileTokenStore()
    {
        var root = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } xdg
                ? xdg
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");

        _path = Path.Combine(root, "SmartHome.Device", OperatingSystem.IsWindows() ? "token.bin" : "token.json");
    }

    public StoredToken? Load()
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        try
        {
            var bytes = File.ReadAllBytes(_path);
            if (OperatingSystem.IsWindows())
            {
                bytes = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            }

            var token = JsonSerializer.Deserialize<StoredToken>(bytes);
            return token is { AccessToken.Length: > 0 } ? token : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or CryptographicException)
        {
            return null;
        }
    }

    public void Save(StoredToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(token);
        if (OperatingSystem.IsWindows())
        {
            bytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        }

        File.WriteAllBytes(_path, bytes);
    }

    public void Clear()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
