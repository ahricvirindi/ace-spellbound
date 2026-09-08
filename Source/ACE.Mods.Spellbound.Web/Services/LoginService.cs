using System.Security.Claims;

using ACE.Common.Cryptography;
using ACE.Database.Models.Auth;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ACE.Mods.Spellbound.Web.Services;

public sealed class LoginService(
    AuthDbContext authDb,
    IMemoryCache cache,
    ILogger<LoginService> logger)
{
    private const int MaxAttemptsPerWindow = 5;
    private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(1);

    public async Task<LoginResult> SignInAsync(HttpContext httpContext, string accountName, string password)
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var counterKey = $"login.attempts:{ip}";

        if (cache.TryGetValue<int>(counterKey, out var attempts) && attempts >= MaxAttemptsPerWindow)
            return LoginResult.Fail("Too many failed attempts. Wait a minute and try again.");

        if (string.IsNullOrWhiteSpace(accountName) || string.IsNullOrWhiteSpace(password))
            return RecordFailure(counterKey, "Account name and password are required.");

        Account? account;
        try
        {
            account = await authDb.Account
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AccountName == accountName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Auth DB query failed for account={Account} ip={Ip}", accountName, ip);
            return LoginResult.Fail("Sign-in is temporarily unavailable. Try again shortly.");
        }

        if (account is null || string.IsNullOrEmpty(account.PasswordHash))
            return RecordFailure(counterKey, "Invalid account or password.");

        if (!BCryptProvider.Verify(password, account.PasswordHash))
            return RecordFailure(counterKey, "Invalid account or password.");

        // Mirrors AuthenticationHandler.cs in the game server: a non-null BanExpireTime
        // in the future means actively banned. Past BanExpireTime is treated as expired.
        if (account.BanExpireTime.HasValue && DateTime.UtcNow < account.BanExpireTime.Value)
        {
            logger.LogWarning("Login blocked for banned account: {Account} ip={Ip} until {Until}",
                account.AccountName, ip, account.BanExpireTime.Value);
            var reasonSuffix = string.IsNullOrWhiteSpace(account.BanReason) ? "" : $" ({account.BanReason})";
            return LoginResult.Fail($"Account is banned until {account.BanExpireTime.Value:u}.{reasonSuffix}");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.AccountId.ToString()),
            new(ClaimTypes.Name, account.AccountName),
            new("access_level", account.AccessLevel.ToString())
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        cache.Remove(counterKey);
        logger.LogInformation("Login successful: account={Account} ip={Ip}", account.AccountName, ip);

        return LoginResult.Ok();
    }

    private LoginResult RecordFailure(string counterKey, string error)
    {
        var current = cache.Get<int?>(counterKey) ?? 0;
        cache.Set(counterKey, current + 1, AttemptWindow);
        return LoginResult.Fail(error);
    }
}

public sealed record LoginResult(bool Success, string? Error)
{
    public static LoginResult Ok() => new(true, null);
    public static LoginResult Fail(string error) => new(false, error);
}
