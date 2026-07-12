using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Vinan.Api.Security;

public sealed class PersonalDataProtector
{
    public const string Prefix = "vinan:v1:";

    private readonly IDataProtector _protector;

    public PersonalDataProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("VINAN.PersonalData.v1");
    }

    public string Protect(string value)
    {
        if (string.IsNullOrEmpty(value) || value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return value;
        }

        return Prefix + _protector.Protect(value);
    }

    public string Unprotect(string value)
    {
        if (string.IsNullOrEmpty(value) || !value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return value;
        }

        try
        {
            return _protector.Unprotect(value[Prefix.Length..]);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException("VINAN could not decrypt personal data with the current key ring.", exception);
        }
    }
}
