using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace SmartHome.Frontend.Components.Pages
{
    [AllowAnonymous]
    public partial class Authorize
    {
        [SupplyParameterFromQuery(Name = "client_id")]
        private string ClientId { get; set; } = string.Empty;

        [SupplyParameterFromQuery(Name = "redirect_uri")]
        private string RedirectUri { get; set; } = string.Empty;

        [SupplyParameterFromQuery(Name = "response_type")]
        private string ResponseType { get; set; } = string.Empty;

        [SupplyParameterFromQuery(Name = "state")]
        private string State { get; set; } = string.Empty;

        [SupplyParameterFromQuery(Name = "scope")]
        private string Scope { get; set; } = string.Empty;

        [SupplyParameterFromQuery(Name = "error")]
        private string? Error { get; set; }

        [Inject]
        private ILogger<Authorize> Logger { get; set; } = default!;

        protected override Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                Logger.LogInformation("OAuth 2.0 authorization page rendered with parameters: ClientId={ClientId}, RedirectUri={RedirectUri}, ResponseType={ResponseType}, State={State}, Scope={Scope}, Error={Error}",
                    ClientId, RedirectUri, ResponseType, State, Scope, Error);
            }
            return base.OnAfterRenderAsync(firstRender);
        }

        protected override Task OnInitializedAsync()
        {
            Logger.LogInformation("OAuth 2.0 authorization page rendered with parameters: ClientId={ClientId}, RedirectUri={RedirectUri}, ResponseType={ResponseType}, State={State}, Scope={Scope}, Error={Error}",
                    ClientId, RedirectUri, ResponseType, State, Scope, Error);

            return base.OnInitializedAsync();
        }
    }
}
