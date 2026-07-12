using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vinan.Api.Data;
using Vinan.Api.Models;

namespace Vinan.Api.Security;

public sealed class OwnerAuthService
{
    public const string DeviceIdClaim = "vinan_device_id";

    private readonly VinanDbContext _database;
    private readonly IPasswordHasher<OwnerProfile> _passwordHasher;

    public OwnerAuthService(VinanDbContext database, IPasswordHasher<OwnerProfile> passwordHasher)
    {
        _database = database;
        _passwordHasher = passwordHasher;
    }

    public async Task<AuthStatusResponse> GetStatusAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var owner = await _database.OwnerProfiles.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var deviceId = ParseClaim(principal, DeviceIdClaim);
        var ownerId = ParseClaim(principal, ClaimTypes.NameIdentifier);
        var authenticated = owner is not null
            && principal.Identity?.IsAuthenticated == true
            && ownerId == owner.Id
            && deviceId is not null
            && await _database.DeviceEnrollments.AsNoTracking()
                .AnyAsync(device => device.Id == deviceId && device.OwnerId == owner.Id && device.RevokedAt == null, cancellationToken);

        return new AuthStatusResponse(
            owner is not null,
            authenticated,
            authenticated ? owner?.DisplayName : null,
            authenticated ? deviceId : null);
    }

    public async Task<AuthResult> SetupAsync(SetupOwnerRequest request, CancellationToken cancellationToken)
    {
        if (await _database.OwnerProfiles.AnyAsync(cancellationToken))
        {
            return AuthResult.Fail("VINAN already has an owner.");
        }

        var validation = Validate(request.DisplayName, request.Passphrase, request.DeviceId, request.DeviceName);
        if (validation is not null)
        {
            return AuthResult.Fail(validation);
        }

        var now = DateTimeOffset.UtcNow;
        var owner = new OwnerProfile
        {
            Id = Guid.NewGuid(),
            DisplayName = request.DisplayName.Trim(),
            CreatedAt = now,
        };
        owner.PasswordHash = _passwordHasher.HashPassword(owner, request.Passphrase);
        var device = new DeviceEnrollment
        {
            Id = request.DeviceId,
            OwnerId = owner.Id,
            Name = request.DeviceName.Trim(),
            EnrolledAt = now,
            LastSeenAt = now,
        };
        _database.OwnerProfiles.Add(owner);
        _database.DeviceEnrollments.Add(device);
        try
        {
            await _database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _database.ChangeTracker.Clear();
            return AuthResult.Fail("VINAN setup was already completed or this device is already enrolled.");
        }

        return AuthResult.Success(new AuthenticatedDevice(owner.Id, owner.DisplayName, device.Id));
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var owner = await _database.OwnerProfiles.SingleOrDefaultAsync(cancellationToken);
        if (owner is null)
        {
            return AuthResult.Fail("VINAN owner setup is required.");
        }

        var verification = _passwordHasher.VerifyHashedPassword(owner, owner.PasswordHash, request.Passphrase);
        if (verification == PasswordVerificationResult.Failed)
        {
            return AuthResult.Fail("The passphrase is incorrect.");
        }

        var validation = Validate(owner.DisplayName, request.Passphrase, request.DeviceId, request.DeviceName);
        if (validation is not null)
        {
            return AuthResult.Fail(validation);
        }

        var now = DateTimeOffset.UtcNow;
        var device = await _database.DeviceEnrollments.FindAsync(new object[] { request.DeviceId }, cancellationToken);
        if (device is null)
        {
            device = new DeviceEnrollment
            {
                Id = request.DeviceId,
                OwnerId = owner.Id,
                Name = request.DeviceName.Trim(),
                EnrolledAt = now,
                LastSeenAt = now,
            };
            _database.DeviceEnrollments.Add(device);
        }
        else
        {
            device.OwnerId = owner.Id;
            device.Name = request.DeviceName.Trim();
            device.LastSeenAt = now;
            device.RevokedAt = null;
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            owner.PasswordHash = _passwordHasher.HashPassword(owner, request.Passphrase);
        }

        await _database.SaveChangesAsync(cancellationToken);
        return AuthResult.Success(new AuthenticatedDevice(owner.Id, owner.DisplayName, device.Id));
    }

    public async Task SignInAsync(HttpContext context, AuthenticatedDevice device, bool persistent)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, device.OwnerId.ToString()),
            new Claim(ClaimTypes.Name, device.OwnerName),
            new Claim(DeviceIdClaim, device.DeviceId.ToString()),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
        {
            IsPersistent = persistent,
            AllowRefresh = true,
            ExpiresUtc = persistent ? DateTimeOffset.UtcNow.AddDays(30) : null,
        });
    }

    public async Task<bool> ValidateDeviceAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var ownerId = ParseClaim(principal, ClaimTypes.NameIdentifier);
        var deviceId = ParseClaim(principal, DeviceIdClaim);
        if (ownerId is null || deviceId is null)
        {
            return false;
        }

        var device = await _database.DeviceEnrollments.SingleOrDefaultAsync(
            item => item.Id == deviceId && item.OwnerId == ownerId,
            cancellationToken);
        if (device is null || device.RevokedAt is not null)
        {
            return false;
        }

        if (device.LastSeenAt < DateTimeOffset.UtcNow.AddMinutes(-5))
        {
            device.LastSeenAt = DateTimeOffset.UtcNow;
            await _database.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<List<DeviceSummary>> GetDevicesAsync(Guid currentDeviceId, CancellationToken cancellationToken)
    {
        return await _database.DeviceEnrollments.AsNoTracking()
            .Where(device => device.RevokedAt == null)
            .OrderByDescending(device => device.LastSeenAt)
            .Select(device => new DeviceSummary(
                device.Id,
                device.Name,
                device.EnrolledAt,
                device.LastSeenAt,
                device.Id == currentDeviceId))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> RevokeDeviceAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        var device = await _database.DeviceEnrollments.FindAsync(new object[] { deviceId }, cancellationToken);
        if (device is null || device.RevokedAt is not null)
        {
            return false;
        }

        device.RevokedAt = DateTimeOffset.UtcNow;
        await _database.SaveChangesAsync(cancellationToken);
        return true;
    }

    public static Guid? CurrentDeviceId(ClaimsPrincipal principal) => ParseClaim(principal, DeviceIdClaim);

    private static Guid? ParseClaim(ClaimsPrincipal principal, string claimType)
    {
        return Guid.TryParse(principal.FindFirstValue(claimType), out var value) ? value : null;
    }

    private static string? Validate(string displayName, string passphrase, Guid deviceId, string deviceName)
    {
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 80)
        {
            return "A valid owner name is required.";
        }

        if (string.IsNullOrWhiteSpace(passphrase) || passphrase.Length < 12)
        {
            return "Use a passphrase with at least 12 characters.";
        }

        if (deviceId == Guid.Empty || string.IsNullOrWhiteSpace(deviceName) || deviceName.Trim().Length > 120)
        {
            return "A valid device identity is required.";
        }

        return null;
    }
}

public sealed record AuthResult(bool Succeeded, AuthenticatedDevice? Device, string? Error)
{
    public static AuthResult Success(AuthenticatedDevice device) => new(true, device, null);
    public static AuthResult Fail(string error) => new(false, null, error);
}
