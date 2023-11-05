using System;
using Microsoft.Extensions.Options;

namespace Common.Configuration;

public sealed class FeedProcessorAppOptions
{
    public int BlogRetrievalIntervalSeconds { get; init; }
    public int RssArchiveIntervalSeconds { get; init; }
    public bool FetchFromRssArchive { get; init; }
}

[OptionsValidator]
public sealed partial class ValidateFeedProcessorAppOptions : IValidateOptions<FeedProcessorAppOptions>;

public sealed partial class ValidateFeedProcessorAppOptions
{
    public ValidateOptionsResult Validate(string? name, FeedProcessorAppOptions options)
    {
        if (options.BlogRetrievalIntervalSeconds == default)
        {
            return ValidateOptionsResult.Fail($"{nameof(options.BlogRetrievalIntervalSeconds)} is required.");
        }

        if (options.RssArchiveIntervalSeconds == default)
        {
            return ValidateOptionsResult.Fail($"{nameof(options.RssArchiveIntervalSeconds)} is required.");
        }

        return ValidateOptionsResult.Success;
    }
}
