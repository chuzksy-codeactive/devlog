-- Run as the migration/owner role after 005_create_app_role.sql, before enabling AiQuotas.
-- This private system schema deliberately contains no tenant content or identifiers.
BEGIN;

CREATE SCHEMA IF NOT EXISTS devlog_cost_control;
REVOKE ALL ON SCHEMA devlog_cost_control FROM PUBLIC;

CREATE TABLE IF NOT EXISTS devlog_cost_control.ai_quota_counters (
    scope text PRIMARY KEY CHECK (scope IN ('requests', 'provider_calls')),
    minute_start timestamptz NOT NULL DEFAULT '-infinity',
    day_start timestamptz NOT NULL DEFAULT '-infinity',
    month_start timestamptz NOT NULL DEFAULT '-infinity',
    minute_count integer NOT NULL DEFAULT 0 CHECK (minute_count >= 0),
    day_count integer NOT NULL DEFAULT 0 CHECK (day_count >= 0),
    month_count integer NOT NULL DEFAULT 0 CHECK (month_count >= 0)
);

REVOKE ALL ON devlog_cost_control.ai_quota_counters FROM PUBLIC, devlog_app;

CREATE OR REPLACE FUNCTION devlog_cost_control.try_acquire(
    p_scope text,
    p_minute_limit integer,
    p_daily_limit integer,
    p_monthly_limit integer
) RETURNS text
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, devlog_cost_control
AS $function$
DECLARE
    counter devlog_cost_control.ai_quota_counters%ROWTYPE;
    now_utc timestamptz;
    minute_utc timestamptz;
    day_utc timestamptz;
    month_utc timestamptz;
BEGIN
    IF p_scope IS NULL OR p_scope NOT IN ('requests', 'provider_calls')
        OR p_minute_limit IS NULL OR p_minute_limit <= 0
        OR p_daily_limit <= 0 OR p_monthly_limit <= 0 THEN
        RAISE EXCEPTION 'Invalid AI quota scope or limits' USING ERRCODE = '22023';
    END IF;
    IF p_scope = 'provider_calls' AND (p_daily_limit IS NULL OR p_monthly_limit IS NULL) THEN
        RAISE EXCEPTION 'Provider calls require daily and monthly allowances' USING ERRCODE = '22023';
    END IF;

    INSERT INTO devlog_cost_control.ai_quota_counters(scope)
    VALUES (p_scope) ON CONFLICT (scope) DO NOTHING;

    SELECT * INTO STRICT counter
    FROM devlog_cost_control.ai_quota_counters
    WHERE scope = p_scope
    FOR UPDATE;

    -- Read the database clock after acquiring the lock, including at UTC rollover.
    now_utc := clock_timestamp();
    minute_utc := date_trunc('minute', now_utc AT TIME ZONE 'UTC') AT TIME ZONE 'UTC';
    day_utc := date_trunc('day', now_utc AT TIME ZONE 'UTC') AT TIME ZONE 'UTC';
    month_utc := date_trunc('month', now_utc AT TIME ZONE 'UTC') AT TIME ZONE 'UTC';

    IF counter.minute_start <> minute_utc THEN counter.minute_count := 0; END IF;
    IF counter.day_start <> day_utc THEN counter.day_count := 0; END IF;
    IF counter.month_start <> month_utc THEN counter.month_count := 0; END IF;

    IF counter.minute_count >= p_minute_limit THEN RETURN 'minute'; END IF;
    IF p_daily_limit IS NOT NULL AND counter.day_count >= p_daily_limit THEN RETURN 'day'; END IF;
    IF p_monthly_limit IS NOT NULL AND counter.month_count >= p_monthly_limit THEN RETURN 'month'; END IF;

    UPDATE devlog_cost_control.ai_quota_counters
    SET minute_start = minute_utc, day_start = day_utc, month_start = month_utc,
        minute_count = counter.minute_count + 1,
        day_count = CASE WHEN p_daily_limit IS NULL THEN 0 ELSE counter.day_count + 1 END,
        month_count = CASE WHEN p_monthly_limit IS NULL THEN 0 ELSE counter.month_count + 1 END
    WHERE scope = p_scope;

    RETURN 'allowed';
END;
$function$;

REVOKE ALL ON FUNCTION devlog_cost_control.try_acquire(text, integer, integer, integer) FROM PUBLIC;
GRANT USAGE ON SCHEMA devlog_cost_control TO devlog_app;
GRANT EXECUTE ON FUNCTION devlog_cost_control.try_acquire(text, integer, integer, integer) TO devlog_app;

COMMIT;
