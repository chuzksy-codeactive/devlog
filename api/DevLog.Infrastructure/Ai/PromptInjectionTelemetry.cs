using System.Diagnostics.Metrics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace DevLog.Infrastructure.Ai;

public class PromptInjectionTelemetry
{
    private static readonly Meter Meter = new("DevLog.Security", "1.0.0");

    private static readonly Counter<long> SuspiciousInputCounter =
        Meter.CreateCounter<long>("ai_prompt_injection_suspicions_total", unit: "events");

    private static readonly Counter<long> ScannedInputCounter =
        Meter.CreateCounter<long>("ai_prompt_inputs_scanned_total", unit: "events");

    private static readonly (string Signal, Regex Pattern)[] DetectionPatterns =
    [
        ("override_instruction", new Regex(@"\b(ignore|disregard|bypass)\b.{0,40}\b(instruction|rule|system|prompt)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline)),
        ("role_escalation", new Regex(@"\b(you are now|act as|pretend to be|system prompt)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("tag_breakout", new Regex(@"</\s*(retrieved_context|recruiter_question|entry_to_sanitize|resume_text|github_repo_data)\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("credential_exfil", new Regex(@"\b(api\s*key|secret|password|token|connection\s*string)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("data_exfil", new Regex(@"\b(exfiltrate|leak|dump|reveal|export\s+all)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled))
    ];

    private readonly ILogger<PromptInjectionTelemetry> _logger;

    public PromptInjectionTelemetry(ILogger<PromptInjectionTelemetry> logger)
    {
        _logger = logger;
    }

    public void Scan(string flow, string inputType, string? content, Guid? tenantId = null, string? slug = null)
    {
        ScannedInputCounter.Add(1,
        [
            new KeyValuePair<string, object?>("flow", flow),
            new KeyValuePair<string, object?>("input_type", inputType)
        ]);

        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        var matchedSignals = DetectionPatterns
            .Where(p => p.Pattern.IsMatch(content))
            .Select(p => p.Signal)
            .ToArray();

        if (matchedSignals.Length == 0)
        {
            return;
        }

        SuspiciousInputCounter.Add(1,
        [
            new KeyValuePair<string, object?>("flow", flow),
            new KeyValuePair<string, object?>("input_type", inputType),
            new KeyValuePair<string, object?>("signals", string.Join(",", matchedSignals))
        ]);

        _logger.LogWarning(
            "AI prompt-injection suspicion detected. Flow={Flow} InputType={InputType} Signals={Signals} TenantId={TenantId} Slug={Slug} InputLength={InputLength}",
            flow,
            inputType,
            matchedSignals,
            tenantId,
            slug,
            content.Length);
    }
}
