using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Common.Configuration;

public sealed class OtlpOptions
{
    public string SinkEndpoint { get; init; }
}

[OptionsValidator]
public sealed partial class ValidateOtlpOptions : IValidateOptions<OtlpOptions>;

public sealed partial class ValidateOtlpOptions(IConfiguration config)
{
    public OtlpOptions Config { get; private set; } = config.GetSection(nameof(OtlpOptions)).Get<OtlpOptions>() ??
                                                      throw new ArgumentNullException(nameof(config));

    public ValidateOptionsResult Validate(string? name, OtlpOptions options)
    {
        if (!Uri.IsWellFormedUriString(options.SinkEndpoint, UriKind.RelativeOrAbsolute))
        {
            return ValidateOptionsResult.Fail("SinkEndpoint must be a valid URI.");
        }

        return ValidateOptionsResult.Success;
    }
}
