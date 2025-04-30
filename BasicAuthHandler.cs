using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Aton_task.Models;
using System.Net.Http.Headers;

public class BasicAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string CACHE_KEY = "UsersCache";
    private readonly IMemoryCache _cache;
    private readonly ILogger<BasicAuthHandler> _logger;

    public BasicAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IMemoryCache cache)
        : base(options, logger, encoder)
    {
        _cache = cache;
        _logger = logger.CreateLogger<BasicAuthHandler>();
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        try
        {
            if (Request.Path.StartsWithSegments("/api/users/authenticate"))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            if (!Request.Headers.ContainsKey("Authorization"))
            {
                return Task.FromResult(AuthenticateResult.Fail("Authorization header missing"));
            }

            var authHeader = Request.Headers.Authorization.ToString();
            if (!AuthenticationHeaderValue.TryParse(authHeader, out var headerValue) ||
                !string.Equals(headerValue.Scheme, "Basic", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid authentication scheme"));
            }

            var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(headerValue.Parameter ?? string.Empty));
            var parts = credentials.Split(':', 2);
            if (parts.Length != 2)
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid credentials format"));
            }

            var login = parts[0];
            var password = parts[1];

            if (!_cache.TryGetValue(CACHE_KEY, out List<User>? users) || users == null)
            {
                _logger.LogWarning("Users cache not found");
                return Task.FromResult(AuthenticateResult.Fail("Authentication system error"));
            }

            var user = users.FirstOrDefault(u =>
                u.Login == login &&
                u.Password == password &&
                u.RevokedOn == null);

            if (user == null)
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid credentials or user deactivated"));
            }

            var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Login),
                    new Claim(ClaimTypes.Role, user.Admin ? "Admin" : "User"),
                    new Claim("UserId", user.Guid.ToString())
                };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication error occurred");
            return Task.FromResult(AuthenticateResult.Fail("Authentication failed due to an error"));
        }
    }
}