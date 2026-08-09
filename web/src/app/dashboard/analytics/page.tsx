"use client";

import { useEffect, useMemo, useState } from "react";
import { useApi } from "@/lib/use-api";
import { AnalyticsSummary, DailyCount } from "@/lib/types";

export default function AnalyticsDashboardPage() {
  const { request } = useApi();
  const [summary, setSummary] = useState<AnalyticsSummary | null>(null);
  const [status, setStatus] = useState<"loading" | "ready" | "error">("loading");

  useEffect(() => {
    request<AnalyticsSummary>("/api/analytics/summary")
      .then((data) => {
        setSummary(data);
        setStatus("ready");
      })
      .catch(() => setStatus("error"));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const headlineStats = useMemo(() => {
    if (!summary) {
      return [];
    }

    return [
      {
        label: "Portfolio views",
        primary: summary.viewsTotal,
        secondary: `${summary.views7d} in the last 7 days`,
        tertiary: `${summary.views30d} in the last 30 days`,
      },
      {
        label: "Ask AI questions",
        primary: summary.queriesTotal,
        secondary: `${summary.queries7d} in the last 7 days`,
        tertiary: `${summary.queries30d} in the last 30 days`,
      },
      {
        label: "Interview requests",
        primary: summary.interviewRequestsVerified,
        secondary: `${summary.interviewRequestsPending} pending`,
        tertiary: `${summary.interviewRequestsAccepted} accepted`,
      },
    ];
  }, [summary]);

  return (
    <div className="flex flex-col gap-8">
      <section className="overflow-hidden rounded-[2rem] border border-white/10 bg-white/6 p-8 shadow-2xl shadow-black/20 backdrop-blur-xl">
        <div className="grid gap-8 lg:grid-cols-[1.05fr_0.95fr]">
          <div>
            <div className="inline-flex items-center gap-2 rounded-full border border-indigo-400/20 bg-indigo-500/10 px-4 py-2 text-sm text-indigo-100">
              <span className="h-2 w-2 rounded-full bg-indigo-300" />
              Portfolio analytics
            </div>
            <h1 className="mt-6 text-4xl font-semibold tracking-tight text-white">
              See what recruiters are noticing and where interest is building.
            </h1>
            <p className="mt-4 max-w-2xl text-base leading-7 text-white/68">
              Track portfolio views, AI recruiter questions, and interview request activity in one place.
            </p>
          </div>

          <div className="grid gap-3 rounded-[1.5rem] border border-white/10 bg-[#101525] p-5">
            <p className="text-xs font-medium uppercase tracking-[0.22em] text-white/42">Signals at a glance</p>
            {headlineStats.map((stat) => (
              <div key={stat.label} className="rounded-2xl border border-white/10 bg-white/5 p-4">
                <p className="text-sm text-white/50">{stat.label}</p>
                <p className="mt-1 text-2xl font-semibold tracking-tight text-white">{stat.primary}</p>
                <p className="mt-2 text-sm leading-6 text-white/62">
                  {stat.secondary} · {stat.tertiary}
                </p>
              </div>
            ))}
            {status === "loading" && <p className="text-sm text-white/55">Loading analytics…</p>}
          </div>
        </div>
      </section>

      {status === "error" && (
        <p className="rounded-2xl border border-rose-400/20 bg-rose-500/10 px-4 py-3 text-sm text-rose-200">
          Couldn&apos;t load analytics — check that the API is running.
        </p>
      )}

      {status === "loading" && (
        <div className="grid gap-5 lg:grid-cols-3">
          {[1, 2, 3].map((item) => (
            <div key={item} className="h-36 rounded-[1.5rem] border border-white/10 bg-white/6" />
          ))}
        </div>
      )}

      {status === "ready" && summary && (
        <>
          <section className="grid gap-5 lg:grid-cols-3">
            <StatCard
              label="Portfolio views"
              primary={summary.viewsTotal}
              secondary={`${summary.views7d} in the last 7 days`}
              tertiary={`${summary.views30d} in the last 30 days`}
            />
            <StatCard
              label="Ask AI questions"
              primary={summary.queriesTotal}
              secondary={`${summary.queries7d} in the last 7 days`}
              tertiary={`${summary.queries30d} in the last 30 days`}
            />
            <StatCard
              label="Interview requests"
              primary={summary.interviewRequestsVerified}
              secondary={`${summary.interviewRequestsPending} pending`}
              tertiary={`${summary.interviewRequestsAccepted} accepted`}
            />
          </section>

          <section className="grid gap-6 xl:grid-cols-2">
            <BarChart
              title="Views, last 30 days"
              subtitle="How often recruiters opened your portfolio."
              data={summary.viewsByDay}
            />
            <BarChart
              title="Ask AI questions, last 30 days"
              subtitle="How often recruiters queried your portfolio with AI."
              data={summary.queriesByDay}
            />
          </section>
        </>
      )}
    </div>
  );
}

function StatCard({
  label,
  primary,
  secondary,
  tertiary,
}: {
  label: string;
  primary: number;
  secondary: string;
  tertiary: string;
}) {
  return (
    <div className="rounded-[1.5rem] border border-white/10 bg-white/6 p-6 backdrop-blur">
      <p className="text-xs font-medium uppercase tracking-[0.22em] text-white/42">{label}</p>
      <p className="mt-3 text-4xl font-semibold tracking-tight text-white">{primary}</p>
      <div className="mt-4 space-y-1 text-sm text-white/62">
        <p>{secondary}</p>
        <p>{tertiary}</p>
      </div>
    </div>
  );
}

function BarChart({
  title,
  subtitle,
  data,
}: {
  title: string;
  subtitle: string;
  data: DailyCount[];
}) {
  const max = Math.max(1, ...data.map((d) => d.count));
  const total = data.reduce((sum, item) => sum + item.count, 0);
  const peak = data.reduce((best, item) => (item.count > best.count ? item : best), data[0] ?? { date: "", count: 0 });

  return (
    <div className="rounded-[1.75rem] border border-white/10 bg-white/6 p-6 backdrop-blur">
      <div className="flex items-start justify-between gap-4">
        <div>
          <p className="text-xs font-medium uppercase tracking-[0.22em] text-white/42">{title}</p>
          <p className="mt-2 text-sm leading-7 text-white/62">{subtitle}</p>
        </div>
        <div className="text-right">
          <p className="text-2xl font-semibold text-white">{total}</p>
          <p className="text-xs text-white/45">total in range</p>
        </div>
      </div>

      <div className="mt-6 flex h-44 items-end gap-1 rounded-[1.25rem] border border-white/8 bg-[#111626] px-4 pb-4 pt-8">
        {data.map((d) => (
          <div
            key={d.date}
            title={`${d.date}: ${d.count}`}
            className="flex-1 rounded-t-full bg-gradient-to-t from-indigo-500/75 to-indigo-200 transition-opacity hover:opacity-85"
            style={{ height: `${Math.max(3, (d.count / max) * 100)}%` }}
          />
        ))}
      </div>

      <div className="mt-4 flex items-center justify-between text-xs text-white/45">
        <span>{data[0]?.date ?? "—"}</span>
        <span>
          Peak: {peak.date || "—"} · {peak.count}
        </span>
        <span>{data[data.length - 1]?.date ?? "—"}</span>
      </div>
    </div>
  );
}
