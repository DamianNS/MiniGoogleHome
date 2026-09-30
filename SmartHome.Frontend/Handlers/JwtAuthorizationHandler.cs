using SmartHome.Shared.Constantes;
using System.Net.Http.Headers;

namespace SmartHome.Frontend.Handlers;

public class JwtAuthorizationHandler(IHttpContextAccessor _httpContextAccessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // 1. Accedemos al contexto HTTP actual
        var context = _httpContextAccessor.HttpContext;

        // 2. Extraemos el token guardado como Claim en el usuario actual
        // (Asegúrate de que el nombre "BackendToken" coincida con el que usaste en el login)
        var token = context?.User?.FindFirst(Constantes.JwtClaimName)?.Value;

        // 3. Si encontramos el token, lo adjuntamos a la petición saliente
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

