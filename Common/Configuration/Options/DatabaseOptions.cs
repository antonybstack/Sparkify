using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Common.Configuration;

public sealed class DatabaseOptions
{
    public string Name { get; init; }
    public string Http { get; init; }
    public string TcpHostName { get; init; }
    public int TcpPort { get; init; }
}

[OptionsValidator]
public sealed partial class ValidateDatabaseOptions : IValidateOptions<DatabaseOptions>;

public sealed partial class ValidateDatabaseOptions(IConfiguration config)
{
    public DatabaseOptions Config { get; private set; } =
        config.GetSection(nameof(DatabaseOptions)).Get<DatabaseOptions>() ??
        throw new ArgumentNullException(nameof(config));

    public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Name))
        {
            return ValidateOptionsResult.Fail($"{nameof(options.Name)} is required.");
        }

        if (!Uri.IsWellFormedUriString(options.Http, UriKind.RelativeOrAbsolute))
        {
            return ValidateOptionsResult.Fail($"{nameof(options.Http)} must be a valid URI.");
        }

        if (!Uri.IsWellFormedUriString(options.TcpHostName, UriKind.RelativeOrAbsolute))
        {
            return ValidateOptionsResult.Fail($"{nameof(options.TcpHostName)} must be a valid URI.");
        }

        if (options.TcpPort == default)
        {
            return ValidateOptionsResult.Fail($"{nameof(options.TcpPort)} is required.");
        }

        return ValidateOptionsResult.Success;
    }
}
