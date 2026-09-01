using System.Diagnostics;

namespace RateLimitEngine.Core.Observability;

public static class RateLimitEngineDiagnostics
{
    public const string ActivitySourceName = "RateLimitEngine";

    public static readonly ActivitySource ActivitySource =
        new(ActivitySourceName);
}
