"use client";

import { useEffect, useState } from "react";
import { useApi } from "@/lib/use-api";
import { AnalyticsSummary, DailyCount } from "@/lib/types";

// Milestone 16 — first UI ever built on top of instrumentation the roadmap
// deliberately started early (portfolio_view_events + ai_query_logs since
// Milestone 12, interview_requests since Milestone 5). Read-only: this page
// calls GET /api/analytics/summary once and renders it, no writes anywhere.
// Deliberately no chart library — a plain CSS bar chart (divs sized by
// percentage height) covers "views/queries over the last 30 days" without
// adding a new dependency, matching this project's existing all-explicit-
// utility-classes styling convention.
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

  return (
    <div className="flex flex-col gap-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight text-zinc-900">Analytics</h1>
        <p className="mt-1 text-sm text-zinc-500">
          Portfolio views, Ask AI questions, and interview requests — logged since day one, shown here for the
          first time.
        </p>
      </div>

      {status === "loading" && <p className="text-sm text-zinc-500">Loading…</p>}
      {status === "error" && (
        <p className="text-sm text-red-600">Couldn&apos;t load analytics — check that the API is running.</p>
      )}

      {status === "ready" && summary && (
        <>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
            <StatCard
              label="Portfolio views"
              primary={summary.viewsTotal}
              secondary={`${summary.views7d} in the last 7 days · ${summary.views30d} in 30 days`}
            />
            <StatCard
              label="Ask AI questions"
              primary={summary.queriesTotal}
              secondary={`${summary.queries7d} in the last 7 days · ${summary.queries30d} in 30 days`}
            />
            <StatCard
              label="Interview requests"
              primary={summary.interviewRequestsVerified}
              secondary={`${summary.interviewRequestsPending} pending · ${summary.interviewRequestsAccepted} accepted`}
            />
          </div>

          <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
            <BarChart title="Views, last 30 days" data={summary.viewsByDay} />
            <BarChart title="Ask AI questions, last 30 days" data={summary.queriesByDay} />
          </div>
        </>
      )}
    </div>
  );
}

function StatCard({ label, primary, secondary }: { label: string; primary: number; secondary: string }) {
  return (
    <div className="rounded-xl border border-zinc-200 bg-white p-5 shadow-sm">
      <p className="text-xs font-medium uppercase tracking-wide text-zinc-400">{label}</p>
      <p className="mt-1 text-3xl font-semibold tracking-tight text-zinc-900">{primary}</p>
      <p className="mt-1 text-xs text-zinc-500">{secondary}</p>
    </div>
  );
}

function BarChart({ title, data }: { title: string; data: DailyCount[] }) {
  const max = Math.max(1, ...data.map((d) => d.count));

  return (
    <div className="rounded-xl border border-zinc-200 bg-white p-5 shadow-sm">
      <p className="text-xs font-medium uppercase tracking-wide text-zinc-400">{title}</p>
      <div className="mt-4 flex h-32 items-end gap-0.5">
        {data.map((d) => (
          <div
            key={d.date}
            title={`${d.date}: ${d.count}`}
            className="flex-1 rounded-t bg-indigo-200 transition-colors hover:bg-indigo-400"
            style={{ height: `${Math.max(2, (d.count / max) * 100)}%` }}
          />
        ))}
      </div>
      <div className="mt-2 flex justify-between text-xs text-zinc-400">
        <span>{data[0]?.date}</span>
        <span>{data[data.length - 1]?.date}</span>
      </div>
    </div>
  );
}
