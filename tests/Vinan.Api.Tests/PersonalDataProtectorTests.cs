using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vinan.Api.Data;
using Vinan.Api.Models;
using Vinan.Api.Security;

namespace Vinan.Api.Tests;

public sealed class PersonalDataProtectorTests
{
    [Fact]
    public void ProtectsAndRestoresPersonalText()
    {
        var protector = CreateProtector();

        var encrypted = protector.Protect("private preference");

        Assert.StartsWith(PersonalDataProtector.Prefix, encrypted);
        Assert.DoesNotContain("private preference", encrypted);
        Assert.Equal("private preference", protector.Unprotect(encrypted));
        Assert.Equal("legacy plaintext", protector.Unprotect("legacy plaintext"));
    }

    [Fact]
    public async Task DatabaseStoresEncryptedMemoryAndReturnsPlaintext()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var protector = CreateProtector();
        var options = new DbContextOptionsBuilder<VinanDbContext>().UseSqlite(connection).Options;
        await using var database = new VinanDbContext(options, protector);
        await database.Database.EnsureCreatedAsync();
        database.Memories.Add(new MemoryItem
        {
            Id = Guid.NewGuid(),
            Text = "Rahul prefers concise answers",
            Category = "Preference",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await database.SaveChangesAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Text FROM Memories LIMIT 1;";
        var stored = Convert.ToString(await command.ExecuteScalarAsync());

        Assert.StartsWith(PersonalDataProtector.Prefix, stored);
        Assert.DoesNotContain("concise answers", stored);
        Assert.Equal("Rahul prefers concise answers", (await database.Memories.AsNoTracking().SingleAsync()).Text);
    }

    private static PersonalDataProtector CreateProtector()
    {
        return new PersonalDataProtector(new EphemeralDataProtectionProvider());
    }
}
