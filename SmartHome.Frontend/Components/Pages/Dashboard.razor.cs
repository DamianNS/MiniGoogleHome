using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using SmartHome.Shared.Entities;
using SmartHome.Shared.Persistence;

namespace SmartHome.Frontend.Components.Pages
{
    [Authorize]
    public partial class Dashboard
    {
        [Inject]
        IDbContextFactory<SmartHomeDbContext> DbContextFactory { get; set; } = default!;
        [Inject]
        IHttpClientFactory HttpClientFactory { get; set; } = default!;

        private List<TokenRow> ValidTokens { get; } = [];
        private List<HistoryRow> History { get; } = [];
        private string BackendStatus { get; set; } = "No disponible";
        private int? CurrentVolume { get; set; }
        private string? LoadError { get; set; }
        private List<OauthCode> Codes { get; } = [];

        protected override async Task OnInitializedAsync()
        {
            try
            {
                await using var context = await DbContextFactory.CreateDbContextAsync();
                var now = DateTime.UtcNow;
                var tokens = await context.OauthTokens
                    .AsNoTracking()
                    .Where(token => token.AccessExpiresAt > now)
                    .OrderBy(token => token.AccessExpiresAt)
                    .Select(token => new TokenRow(token.AgentUserId, token.AccessExpiresAt))
                    .ToListAsync();
                ValidTokens.AddRange(tokens);

                var history = await context.HistorialReproducciones
                    .AsNoTracking()
                    .OrderByDescending(item => item.Fecha)
                    .Take(50)
                    .Select(item => new HistoryRow(item.QueryTexto, item.Fecha, item.Exitoso))
                    .ToListAsync();
                History.AddRange(history);

                var codes = await context.OauthCodes
                    .AsNoTracking()
                    .ToListAsync();
                Codes.AddRange(codes);
            }
            catch (Exception)
            {
                LoadError = "la base de datos no está disponible";
            }

            await LoadBackendStateAsync();
        }

        private async Task LoadBackendStateAsync()
        {
            try
            {
                var client = HttpClientFactory.CreateClient("Backend");
                using var healthResponse = await client.GetAsync("health");
                BackendStatus = healthResponse.IsSuccessStatusCode ? "Conectado" : "Error";

                using var volumeResponse = await client.GetAsync("api/status/volume");
                if (volumeResponse.IsSuccessStatusCode)
                {
                    var volume = await volumeResponse.Content.ReadFromJsonAsync<VolumeResponse>();
                    CurrentVolume = volume?.CurrentVolume;
                }
            }
            catch (Exception)
            {
                BackendStatus = "No disponible";
            }
        }

        private string BackendBadgeClass => BackendStatus == "Conectado"
            ? "text-bg-success"
            : "text-bg-secondary";

        private sealed record TokenRow(string AgentUserId, DateTime ExpiresAt);

        private sealed record HistoryRow(string Query, DateTime Date, bool Success);

        private sealed class VolumeResponse
        {
            public int? CurrentVolume { get; set; }
        }
    }
}
