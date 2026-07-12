using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vinan.Api.Data;
using Vinan.Api.Models;
using Vinan.Api.Security;

namespace Vinan.Api.Tests;

public sealed class OwnerAuthServiceTests
{
    [Fact]
    public async Task SetupCreatesHashedOwnerAndEnrolledDevice()
    {
        await using var fixture = await AuthFixture.CreateAsync();
        var deviceId = Guid.NewGuid();

        var result = await fixture.Service.SetupAsync(
            new SetupOwnerRequest("Rahul", "correct horse battery staple", deviceId, "Test browser"),
            default);

        Assert.True(result.Succeeded);
        var owner = await fixture.Database.OwnerProfiles.SingleAsync();
        Assert.NotEqual("correct horse battery staple", owner.PasswordHash);
        Assert.Equal(deviceId, (await fixture.Database.DeviceEnrollments.SingleAsync()).Id);
        var anonymousStatus = await fixture.Service.GetStatusAsync(new ClaimsPrincipal(), default);
        Assert.True(anonymousStatus.IsConfigured);
        Assert.False(anonymousStatus.IsAuthenticated);
        Assert.Null(anonymousStatus.OwnerName);
        Assert.False((await fixture.Service.SetupAsync(
            new SetupOwnerRequest("Rahul", "another secure passphrase", Guid.NewGuid(), "Other"),
            default)).Succeeded);
    }

    [Fact]
    public async Task LoginRequiresPassphraseAndCanRestoreRevokedDevice()
    {
        await using var fixture = await AuthFixture.CreateAsync();
        var deviceId = Guid.NewGuid();
        await fixture.Service.SetupAsync(
            new SetupOwnerRequest("Rahul", "correct horse battery staple", deviceId, "Test browser"),
            default);
        await fixture.Service.RevokeDeviceAsync(deviceId, default);

        var rejected = await fixture.Service.LoginAsync(
            new LoginRequest("wrong passphrase", deviceId, "Test browser"),
            default);
        var accepted = await fixture.Service.LoginAsync(
            new LoginRequest("correct horse battery staple", deviceId, "Test browser"),
            default);

        Assert.False(rejected.Succeeded);
        Assert.True(accepted.Succeeded);
        Assert.Null((await fixture.Database.DeviceEnrollments.SingleAsync()).RevokedAt);
    }

    [Fact]
    public async Task RevokedDevicePrincipalIsRejected()
    {
        await using var fixture = await AuthFixture.CreateAsync();
        var deviceId = Guid.NewGuid();
        var setup = await fixture.Service.SetupAsync(
            new SetupOwnerRequest("Rahul", "correct horse battery staple", deviceId, "Test browser"),
            default);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, setup.Device!.OwnerId.ToString()),
            new Claim(OwnerAuthService.DeviceIdClaim, deviceId.ToString()),
        }, "test"));

        Assert.True(await fixture.Service.ValidateDeviceAsync(principal, default));
        await fixture.Service.RevokeDeviceAsync(deviceId, default);
        Assert.False(await fixture.Service.ValidateDeviceAsync(principal, default));
    }

    private sealed class AuthFixture : IAsyncDisposable
    {
        private AuthFixture(SqliteConnection connection, VinanDbContext database, OwnerAuthService service)
        {
            Connection = connection;
            Database = database;
            Service = service;
        }

        public SqliteConnection Connection { get; }
        public VinanDbContext Database { get; }
        public OwnerAuthService Service { get; }

        public static async Task<AuthFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<VinanDbContext>().UseSqlite(connection).Options;
            var database = new VinanDbContext(options, new PersonalDataProtector(new EphemeralDataProtectionProvider()));
            await database.Database.EnsureCreatedAsync();
            var service = new OwnerAuthService(database, new PasswordHasher<OwnerProfile>());
            return new AuthFixture(connection, database, service);
        }

        public async ValueTask DisposeAsync()
        {
            await Database.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
