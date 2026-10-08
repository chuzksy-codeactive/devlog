using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace DevLog.Infrastructure.Ai.CostControl;

public class AiQuotaHttpHandler(
    IAiQuotaStore quotaStore,
    IOptionsMonitor<AiQuotaOptions> options,
    IConfiguration configuration) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var limits = options.CurrentValue;
        if (!limits.Enabled)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        if (configuration.GetValue<bool>("AiCostControl:KillSwitchEnabled"))
        {
            throw new AiQuotaPausedException();
        }

        var decision = await quotaStore.TryAcquireAsync(
            "provider_calls",
            limits.MaxProviderCallsPerMinute,
            limits.MaxProviderCallsPerDay,
            limits.MaxProviderCallsPerMonth,
            cancellationToken);

        if (decision != AiQuotaDecision.Allowed)
        {
            throw new AiQuotaRejectedException(decision switch
            {
                AiQuotaDecision.MinuteLimit => "ai_provider_minute_allowance_reached",
                AiQuotaDecision.DailyLimit => "ai_daily_allowance_reached",
                AiQuotaDecision.MonthlyLimit => "ai_monthly_allowance_reached",
                _ => throw new AiQuotaUnavailableException("The AI quota store returned an invalid decision.")
            });
        }

        // Do not refund failures: the provider may have processed a request before a timeout.
        return await base.SendAsync(request, cancellationToken);
    }
}
