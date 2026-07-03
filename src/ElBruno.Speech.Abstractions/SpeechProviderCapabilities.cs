namespace ElBruno.Speech;

/// <summary>Describes provider capabilities used to configure concurrency and fallback behavior.</summary>
/// <param name="SupportsConcurrentRequests">True if the provider can handle simultaneous requests.</param>
/// <param name="SupportsStreaming">True if the provider supports streaming output.</param>
/// <param name="SupportsProgressiveGeneration">True if streaming delivers tokens/chunks incrementally.</param>
/// <param name="SupportsCancellation">True if the provider honors <see cref="CancellationToken"/> promptly.</param>
/// <param name="RecommendedMaxConcurrency">
/// Optional upper bound on concurrent requests. <see langword="null"/> means unlimited.
/// </param>
public sealed record SpeechProviderCapabilities(
    bool SupportsConcurrentRequests,
    bool SupportsStreaming,
    bool SupportsProgressiveGeneration,
    bool SupportsCancellation,
    int? RecommendedMaxConcurrency = null);
