import assert from "node:assert/strict";
import test from "node:test";
import { getAiQuotaErrorMessage } from "../src/lib/ai-quota-errors.ts";

for (const [status, reason, expected] of [
  [429, "ai_provider_minute_allowance_reached", /in a minute/],
  [429, "ai_daily_allowance_reached", /00:00 UTC/],
  [429, "ai_monthly_allowance_reached", /next month \(UTC\)/],
  [503, "quota_store_unavailable", /allowance could not be checked/],
  [503, "kill_switch_enabled", /temporarily paused/],
]) {
  test(`explains ${reason} without blaming the candidate or API credentials`, () => {
    assert.match(getAiQuotaErrorMessage(status, JSON.stringify({ reason })), expected);
  });
}

test("preserves existing handling for unrelated, malformed and mismatched errors", () => {
  for (const body of ["not JSON", "null", "[]", "{}", '{"reason":"per_user_or_ip_limit_reached"}']) {
    assert.equal(getAiQuotaErrorMessage(429, body), null);
  }
  assert.equal(getAiQuotaErrorMessage(500, '{"reason":"ai_daily_allowance_reached"}'), null);
  assert.equal(getAiQuotaErrorMessage(503, '{"reason":"ai_daily_allowance_reached"}'), null);
  assert.equal(getAiQuotaErrorMessage(429, '{"reason":{"unexpected":"value"}}'), null);
});
