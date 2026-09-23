using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using SmartHome.Frontend.Components;
using SmartHome.Frontend.Services;
using SmartHome.Shared.Configuration;
using SmartHome.Shared.Persistence;
using System.Security.Claims;

namespace SmartHome.Frontend;

public static class Program
{
    public static void Main(string[] args)
    {

        var builder = WebApplication.CreateBuilder(args);
        
        builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(@"./data/dataprotection"))
                .SetApplicationName("SmartHomeApp");

        // Add services to the container.
        builder.Services.AddRazorComponents();
        builder.Services.AddSmartHomePersistence(builder.Configuration);
        builder.Services.AddScoped<AdminAuthenticationService>();
        builder.Services.AddScoped<AuthorizationCodeService>();
        builder.Services.AddHttpClient("Backend", client =>
        {
            client.BaseAddress = new Uri(
                builder.Configuration["Backend:BaseUrl"] ?? "http://localhost:5000/");
        });
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.Configure<OAuthOptions>(builder.Configuration.GetSection("OAuth"));
        builder.Services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/login";
                options.AccessDeniedPath = "/login";
                options.Cookie.Name = "SmartHome.Admin";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
            });
        builder.Services.AddAuthorization();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }
        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseHttpsRedirection();

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();

        app.MapPost("/loginPost", async (
            HttpContext httpContext,
            AdminAuthenticationService authenticationService,
            CancellationToken cancellationToken) =>
            {
                var form = await httpContext.Request.ReadFormAsync(cancellationToken);
                var username = form["username"].ToString();
                var password = form["password"].ToString();
                var returnUrl = form["returnUrl"].ToString();
                var user = await authenticationService.AuthenticateAsync(username, password, cancellationToken);

                if (user is null)
                {
                    return Results.Redirect($"/login?returnUrl={Uri.EscapeDataString(returnUrl)}&error=invalid");
                }

                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim("agent_user_id", user.AgentUserId)
                };
                var principal = new ClaimsPrincipal(new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme));
                await httpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal);

                var safeReturnUrl = returnUrl.StartsWith('/') && !returnUrl.StartsWith("//")
                    ? returnUrl
                    : "/dashboard";
                return Results.Redirect(safeReturnUrl);
            });

        app.MapPost("/logout", async (HttpContext httpContext) =>
        {
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/");
        });

        app.MapPost("/oauth/authorize/submit", async (
            HttpContext httpContext,
            AdminAuthenticationService authenticationService,
            AuthorizationCodeService authorizationCodeService,
            IOptions<OAuthOptions> oauthOptions,
            ILogger<AuthorizationCodeService> _logger,
            CancellationToken cancellationToken) =>
        {
            var form = await httpContext.Request.ReadFormAsync(cancellationToken);
            var clientId = form["client_id"].ToString();
            var redirectUri = form["redirect_uri"].ToString();
            var responseType = form["response_type"].ToString();
            var state = form["state"].ToString();
            var username = form["username"].ToString();
            var password = form["password"].ToString();
            var options = oauthOptions.Value;

            _logger.LogInformation("Received OAuth authorization request: client_id={ClientId}, redirect_uri={RedirectUri}, response_type={ResponseType}, state={State}, username={Username}",
                clientId, redirectUri, responseType, state, username);

            var validRequest = string.Equals(clientId, options.ClientId, StringComparison.Ordinal)
                && string.Equals(responseType, "code", StringComparison.Ordinal)
                && options.AllowedRedirectUris.Contains(redirectUri, StringComparer.Ordinal);

            if (!string.Equals(clientId, options.ClientId, StringComparison.Ordinal)) {
                return Results.Redirect("/oauth/authorize?error=invalid_client_id");
            }

            if (!string.Equals(responseType, "code", StringComparison.Ordinal))
            {
                return Results.Redirect("/oauth/authorize?error=invalid_code");
            }

            if(!options.AllowedRedirectUris.Contains(redirectUri, StringComparer.Ordinal))
            {
                return Results.Redirect("/oauth/authorize?error=invalid_redirect_uri");
            }

            if (!validRequest)
            {
                return Results.Redirect("/oauth/authorize?error=invalid_request");
            }

            var user = await authenticationService.AuthenticateAsync(username, password, cancellationToken);
            if (user is null)
            {
                return Results.Redirect("/oauth/authorize?error=invalid_credentials");
            }

            var code = await authorizationCodeService.CreateAsync(user.AgentUserId, cancellationToken);
            _logger.LogInformation("Generated authorization code for user {AgentUserId}: {Code}", user.AgentUserId, code);
            var parameters = new Dictionary<string, string?> { ["code"] = code };
            if (!string.IsNullOrWhiteSpace(state))
            {
                parameters["state"] = state;
            }

            _logger.LogInformation("Redirecting to {RedirectUri} with parameters: {Parameters}", redirectUri, parameters);

            return Results.Redirect(QueryHelpers.AddQueryString(redirectUri, parameters));
        });

        app.MapStaticAssets();
        app.MapRazorComponents<App>();

        app.Run();
    }
}
