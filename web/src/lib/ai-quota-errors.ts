export function getAiQuotaErrorMessage(status: number, body: string): string | null {
  if (status !== 429 && status !== 503) return null;

  let payload: unknown;
  try {
    payload = JSON.parse(body);
  } catch (error) {
    if (error instanceof SyntaxError) return null;
    throw error;
  }

  if (!payload || typeof payload !== "object" || !("reason" in payload)) return null;

  if (status === 429) {
    switch (payload.reason) {
      case "ai_provider_minute_allowance_reached":
        return "AI is receiving too many requests. Please try again in a minute.";
      case "ai_daily_allowance_reached":
        return "DevLog's daily AI allowance has been reached. AI requests resume after 00:00 UTC.";
      case "ai_monthly_allowance_reached":
        return "DevLog's monthly AI allowance has been reached. AI requests resume on the first day of the next month (UTC).";
    }
  }

  if (status === 503) {
    switch (payload.reason) {
      case "quota_store_unavailable":
        return "AI is temporarily paused because its usage allowance could not be checked. Please try again later.";
      case "kill_switch_enabled":
        return "AI is temporarily paused. Please try again later.";
    }
  }

  return null;
}
