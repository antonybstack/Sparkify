namespace Sparkify.Features.BlogFeatures;

public readonly record struct FaviconPacket(string Name, long Size, Stream Stream);