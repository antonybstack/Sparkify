using System;
using Microsoft.Extensions.Options;

namespace Common.Configuration;

public sealed class ApiOptions
{
    public int Port { get; init; }
    public string[] AllowedOrigins { get; init; }
}

[OptionsValidator]
public sealed partial class ValidateApiOptions : IValidateOptions<ApiOptions>;

public sealed partial class ValidateApiOptions
{
    public ValidateOptionsResult Validate(string? name, ApiOptions options)
    {
        if (options.Port == default)
        {
            return ValidateOptionsResult.Fail("Port is required.");
        }

        foreach (var origin in options.AllowedOrigins)
        {
            if (!Uri.IsWellFormedUriString(origin, UriKind.RelativeOrAbsolute))
            {
                return ValidateOptionsResult.Fail("AllowedOrigins must be a valid URI.");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
