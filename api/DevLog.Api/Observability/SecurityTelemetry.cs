using System.Diagnostics.Metrics;

namespace DevLog.Api.Observability;

internal static class SecurityTelemetry
{
    private static readonly Meter Meter = new("DevLog.Security", "1.0.0");

    private static readonly Counter<long> PrivateBetaDenials =
        Meter.CreateCounter<long>("private_beta_denials_total");

    private static readonly Counter<long> RateLimitRejections =
        Meter.CreateCounter<long>("rate_limit_rejections_total");

    private static readonly Counter<long> AiKillSwitchBlocks =
        Meter.CreateCounter<long>("ai_killswitch_blocks_total");

    private static readonly Counter<long> AiGlobalCapBlocks =
        Meter.CreateCounter<long>("ai_global_cap_blocks_total");

    public static void TrackPrivateBetaDeny(string reasonCode)
    {
        PrivateBetaDenials.Add(
            1,
            new KeyValuePair<string, object?>("reason", reasonCode));
    }

    public static void TrackRateLimitRejection(string scope)
    {
        RateLimitRejections.Add(
            1,
            new KeyValuePair<string, object?>("scope", scope));
    }

    public static void TrackAiKillSwitchBlock(string endpoint)
    {
        AiKillSwitchBlocks.Add(
            1,
            new KeyValuePair<string, object?>("endpoint", endpoint));
    }

    public static void TrackAiGlobalCapBlock(string endpoint, int maxPerMinute)
    {
        AiGlobalCapBlocks.Add(
            1,
            new KeyValuePair<string, object?>("endpoint", endpoint),
            new KeyValuePair<string, object?>("max_per_minute", maxPerMinute));
    }
}
