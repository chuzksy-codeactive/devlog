"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useApi } from "@/lib/use-api";
import { LearningLabReminderStatus, MyPortfolio } from "@/lib/types";

const CARDS = [
  { href: "/dashboard/profile", label: "Profile", description: "Your name, headline, tagline, links, and visibility." },
  { href: "/dashboard/experience", label: "Experience", description: "Roles, projects, and education on your timeline." },
  { href: "/dashboard/journal", label: "Sprint Journal", description: "Write, sanitize, and publish entries." },
  { href: "/dashboard/interview-requests", label: "Interview Requests", description: "Accept or decline recruiter requests." },
  { href: "/dashboard/analytics", label: "Analytics", description: "Views, Ask AI questions, and interview request stats." },
];

export default function DashboardOverviewPage() {
  const { request } = useApi();
  const [portfolio, setPortfolio] = useState<MyPortfolio | null>(null);
  const [status, setStatus] = useState<"loading" | "ready" | "error">("loading");
  const [reminder, setReminder] = useState<LearningLabReminderStatus | null>(null);

  useEffect(() => {
    request<MyPortfolio>("/api/portfolio/me")
      .then((data) => {
        setPortfolio(data);
        setStatus("ready");
      })
      .catch(() => setStatus("error"));

    request<LearningLabReminderStatus>("/api/learning-lab/reminder")
      .then(setReminder)
      .catch(() => {});
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div className="flex flex-col gap-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight text-zinc-900">
          {status === "ready" && portfolio ? `Welcome back, ${portfolio.displayName || "there"}` : "Dashboard"}
        </h1>
        {status === "ready" && portfolio && (
          <p className="mt-1 text-sm text-zinc-500">
            Your public portfolio:{" "}
            <Link href={`/${portfolio.slug}`} className="font-medium text-indigo-600 underline">
              devlog.app/{portfolio.slug}
            </Link>{" "}
            <span className="text-zinc-400">
              ({portfolio.visibility === "Public" ? "public" : portfolio.visibility === "Unlisted" ? "unlisted — reachable by direct link only" : "paused — hidden from everyone"})
            </span>
          </p>
        )}
        {status === "error" && (
          <p className="mt-1 text-sm text-red-600">Couldn&apos;t load your portfolio — check that the API is running.</p>
        )}
      </div>

      {reminder?.nudgeActive && (
        <div className="rounded-xl border border-amber-200 bg-amber-50 p-4">
          <p className="text-sm text-amber-800">
            {reminder.daysSinceLastEntry === null
              ? "You haven't written a Learning Lab entry yet — start your growth log."
              : `It's been ${reminder.daysSinceLastEntry} days since your last Learning Lab entry — write one now.`}
          </p>
          <Link href="/dashboard/journal" className="mt-1 inline-block text-sm font-medium text-amber-900 underline">
            Write a Learning Log entry →
          </Link>
        </div>
      )}

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        {CARDS.map((card) => (
          <Link
            key={card.href}
            href={card.href}
            className="rounded-xl border border-zinc-200 bg-white p-5 shadow-sm transition-colors hover:border-indigo-200 hover:bg-indigo-50/40"
          >
            <p className="font-medium text-zinc-900">{card.label}</p>
            <p className="mt-1 text-sm text-zinc-500">{card.description}</p>
          </Link>
        ))}
      </div>
    </div>
  );
}
