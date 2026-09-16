using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using SmartHome.Frontend.Services;
using SmartHome.Frontend.Components;
using SmartHome.Shared.Configuration;
using SmartHome.Shared.Persistence;

var builder = WebApplication.CreateBuilder(args);

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

app.MapPost("/login", async (
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

    var validRequest = string.Equals(clientId, options.ClientId, StringComparison.Ordinal)
        && string.Equals(responseType, "code", StringComparison.Ordinal)
        && options.AllowedRedirectUris.Contains(redirectUri, StringComparer.Ordinal);
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
    var parameters = new Dictionary<string, string?> { ["code"] = code };
    if (!string.IsNullOrWhiteSpace(state))
    {
        parameters["state"] = state;
    }

    return Results.Redirect(QueryHelpers.AddQueryString(redirectUri, parameters));
});

app.MapStaticAssets();
app.MapRazorComponents<App>();

app.Run();

public partial class Program;
