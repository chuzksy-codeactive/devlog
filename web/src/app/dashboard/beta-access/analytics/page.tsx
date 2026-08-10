"use client";

import { useEffect, useState } from "react";
import { ApiError, useApi } from "@/lib/use-api";
import { AdminUserActivityAnalyticsResponse, AdminUserActivityRow } from "@/lib/types";

function formatDate(iso: string | null) {
  if (!iso) {
    return "No recent activity";
  }

  return new Date(iso).toLocaleString();
}

export default function BetaAccessAdminAnalyticsPage() {
  const { request } = useApi();
  const [windowDays, setWindowDays] = useState(30);
  const [status, setStatus] = useState<"loading" | "ready" | "forbidden" | "error">("loading");
  const [data, setData] = useState<AdminUserActivityAnalyticsResponse | null>(null);

  useEffect(() => {
    request<AdminUserActivityAnalyticsResponse>(`/api/beta-access/admin/analytics/users?days=${windowDays}&take=25`)
      .then((response) => {
        setData(response);
        setStatus("ready");
      })
      .catch((error) => {
        if (error instanceof ApiError && (error.status === 401 || error.status === 403)) {
          setStatus("forbidden");
          return;
        }

        setStatus("error");
      });
      // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [windowDays]);

  const generatedAt = data?.generatedAt ? new Date(data.generatedAt).toLocaleString() : null;

  if (status === "loading") {
    return <p className="text-sm text-white/70">Loading admin analytics…</p>;
  }

  if (status === "forbidden") {
    return (
      <section className="rounded-[1.5rem] border border-amber-300/18 bg-amber-500/10 p-6 text-amber-100">
        You don&apos;t have permission to view admin analytics.
      </section>
    );
  }

  if (status === "error" || !data) {
    return (
      <section className="rounded-[1.5rem] border border-rose-300/20 bg-rose-500/10 p-6 text-rose-100">
        Couldn&apos;t load admin analytics right now.
      </section>
    );
  }

  return (
    <div className="flex flex-col gap-8">
      <section className="rounded-[1.75rem] border border-white/10 bg-white/6 p-7">
        <p className="text-xs font-medium uppercase tracking-[0.22em] text-white/45">Admin analytics</p>
        <h1 className="mt-3 text-3xl font-semibold text-white">Creator activity health</h1>
        <p className="mt-3 text-sm text-white/65">
          Tenants: <span className="font-medium text-white">{data.tenantCount}</span> · Highly active:{" "}
          <span className="font-medium text-white">{data.highlyActiveCount}</span> · Low activity:{" "}
          <span className="font-medium text-white">{data.lowActivityCount}</span> · Possible misuse:{" "}
          <span className="font-medium text-white">{data.possibleAbuseCount}</span>
        </p>

        <div className="mt-5 flex flex-wrap items-center gap-3">
          {[30, 60, 90].map((days) => (
            <button
              key={days}
              onClick={() => {
                setStatus("loading");
                setWindowDays(days);
              }}
              className={`rounded-full px-4 py-2 text-sm transition ${
                windowDays === days
                  ? "bg-indigo-500 text-white"
                  : "border border-white/12 text-white/75 hover:bg-white/10 hover:text-white"
              }`}
            >
              Last {days} days
            </button>
          ))}
          {generatedAt && <span className="text-xs text-white/45">Generated {generatedAt}</span>}
        </div>
      </section>

      <ActivitySection title="Most active users" subtitle="Top users by creator activity score." rows={data.mostActive} />
      <ActivitySection title="Least active users" subtitle="Users needing activation or onboarding support." rows={data.leastActive} />
    </div>
  );
}

function ActivitySection({
  title,
  subtitle,
  rows,
}: {
  title: string;
  subtitle: string;
  rows: AdminUserActivityRow[];
}) {
  return (
    <section className="rounded-[1.75rem] border border-white/10 bg-white/6 p-6">
      <h2 className="text-xl font-semibold text-white">{title}</h2>
      <p className="mt-1 text-sm text-white/60">{subtitle}</p>
      {rows.length === 0 ? (
        <p className="mt-4 text-sm text-white/55">No users in this segment.</p>
      ) : (
        <div className="mt-4 overflow-x-auto">
          <table className="min-w-full text-left text-sm">
            <thead>
              <tr className="border-b border-white/12 text-white/55">
                <th className="px-3 py-2 font-medium">User</th>
                <th className="px-3 py-2 font-medium">Score</th>
                <th className="px-3 py-2 font-medium">Level</th>
                <th className="px-3 py-2 font-medium">Entries</th>
                <th className="px-3 py-2 font-medium">Views</th>
                <th className="px-3 py-2 font-medium">AI</th>
                <th className="px-3 py-2 font-medium">Last activity</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={`${title}-${row.tenantId}`} className="border-b border-white/8 text-white/80">
                  <td className="px-3 py-2">
                    <p className="font-medium text-white">{row.displayName || row.slug}</p>
                    <p className="text-xs text-white/50">/{row.slug}</p>
                    {row.possibleAbuse && (
                      <span className="mt-1 inline-block rounded-full border border-amber-300/30 bg-amber-500/10 px-2 py-0.5 text-[11px] text-amber-100">
                        Possible misuse pattern
                      </span>
                    )}
                  </td>
                  <td className="px-3 py-2">{row.activityScore}</td>
                  <td className="px-3 py-2">{row.activityLevel}</td>
                  <td className="px-3 py-2">
                    <span className="text-white">Raw {row.rawEntries30d}</span>
                    <span className="mx-1 text-white/45">·</span>
                    <span className="text-white">Published {row.publishedEntries30d}</span>
                  </td>
                  <td className="px-3 py-2">{row.portfolioViews30d}</td>
                  <td className="px-3 py-2">{row.aiQueries30d}</td>
                  <td className="px-3 py-2 text-white/65">{formatDate(row.lastActivityAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
