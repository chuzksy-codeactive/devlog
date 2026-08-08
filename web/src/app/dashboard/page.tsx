"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useApi } from "@/lib/use-api";
import { LearningLabReminderStatus, MyPortfolio } from "@/lib/types";

const workspaceCards = [
  {
    href: "/dashboard/profile",
    label: "Profile",
    description: "Shape your public narrative, headline, proof links, and visibility.",
    eyebrow: "Public identity",
    helper: "Name, headline, links",
  },
  {
    href: "/dashboard/experience",
    label: "Experience",
    description: "Organize your roles, projects, and timeline so recruiters get the full story.",
    eyebrow: "Career proof",
    helper: "Timeline, projects, outcomes",
  },
  {
    href: "/dashboard/journal",
    label: "Sprint Journal",
    description: "Turn delivery work into sanitizable, publishable portfolio proof.",
    eyebrow: "Ongoing work",
    helper: "Draft, sanitize, publish",
  },
  {
    href: "/dashboard/interview-requests",
    label: "Interview Requests",
    description: "Manage recruiter outreach and stay responsive to the strongest opportunities.",
    eyebrow: "Recruiter inbox",
    helper: "Requests and follow-ups",
  },
  {
    href: "/dashboard/analytics",
    label: "Analytics",
    description: "See views, AI questions, and signals that show what is resonating.",
    eyebrow: "Performance",
    helper: "Views, AI questions, interest",
  },
];

function visibilityLabel(visibility: MyPortfolio["visibility"]): string {
  switch (visibility) {
    case "Public":
      return "Public";
    case "Unlisted":
      return "Unlisted";
    case "Paused":
      return "Paused";
    default:
      return "Unknown";
  }
}

function visibilityHint(visibility: MyPortfolio["visibility"]): string {
  switch (visibility) {
    case "Public":
      return "Visible to recruiters and shareable by link.";
    case "Unlisted":
      return "Hidden from discovery but accessible with a direct link.";
    case "Paused":
      return "Hidden from everyone until you publish again.";
    default:
      return "";
  }
}

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

  const profileUrl = useMemo(() => {
    if (!portfolio) {
      return null;
    }

    return `workfolio.app/${portfolio.slug}`;
  }, [portfolio]);

  const summaryStats = useMemo(() => {
    if (!portfolio) {
      return [];
    }

    return [
      {
        label: "Visibility",
        value: visibilityLabel(portfolio.visibility),
        helper: visibilityHint(portfolio.visibility),
      },
      {
        label: "Proof links",
        value: String(portfolio.proofLinks.length),
        helper: portfolio.proofLinks.length > 0 ? "External evidence attached" : "Add links recruiters can verify",
      },
      {
        label: "Core technologies",
        value: String(portfolio.coreTechnologies.length),
        helper:
          portfolio.coreTechnologies.length > 0
            ? portfolio.coreTechnologies.slice(0, 3).join(", ")
            : "Add skills to make your portfolio easier to scan",
      },
      {
        label: "GitHub",
        value: portfolio.gitHubVerified ? "Verified" : portfolio.gitHubUrl ? "Connected" : "Missing",
        helper: portfolio.gitHubVerified
          ? "Recruiters can trust the profile link"
          : portfolio.gitHubUrl
            ? "Connected but not yet verified"
            : "Add a source of proof",
      },
    ];
  }, [portfolio]);

  return (
    <div className="flex flex-col gap-8">
      <section className="overflow-hidden rounded-[2rem] border border-white/10 bg-white/6 p-8 shadow-2xl shadow-black/20 backdrop-blur-xl">
        <div className="grid gap-8 lg:grid-cols-[1.2fr_0.8fr]">
          <div>
            <div className="inline-flex items-center gap-2 rounded-full border border-indigo-400/20 bg-indigo-500/10 px-4 py-2 text-sm text-indigo-100">
              <span className="h-2 w-2 rounded-full bg-indigo-300" />
              Creator dashboard
            </div>

            <h1 className="mt-6 text-4xl font-semibold tracking-tight text-white">
              {status === "ready" && portfolio ? `Welcome back, ${portfolio.displayName || "there"}` : "Your creator workspace"}
            </h1>
            <p className="mt-4 max-w-2xl text-base leading-7 text-white/68">
              Build a polished portfolio recruiters can explore, keep private drafts safe, and make your real work easier to understand.
            </p>

            {status === "ready" && portfolio && profileUrl && (
              <div className="mt-6 flex flex-wrap items-center gap-3">
                <div className="rounded-full border border-white/10 bg-white/6 px-4 py-2 text-sm text-white/72">
                  Portfolio URL: <span className="font-medium text-white">{profileUrl}</span>
                </div>
                <div className="rounded-full border border-emerald-400/20 bg-emerald-500/10 px-4 py-2 text-sm text-emerald-100">
                  {visibilityLabel(portfolio.visibility)}
                </div>
              </div>
            )}

            <div className="mt-8 flex flex-wrap gap-3">
              <Link
                href="/dashboard/profile"
                className="rounded-full bg-indigo-500 px-5 py-3 text-sm font-medium text-white shadow-lg shadow-indigo-500/25 transition hover:bg-indigo-400"
              >
                Edit profile
              </Link>
              <Link
                href="/dashboard/journal"
                className="rounded-full border border-white/12 px-5 py-3 text-sm font-medium text-white transition hover:bg-white/10"
              >
                New journal entry
              </Link>
              {status === "ready" && portfolio && (
                <Link
                  href={`/${portfolio.slug}`}
                  className="rounded-full border border-white/12 px-5 py-3 text-sm font-medium text-white transition hover:bg-white/10"
                >
                  View public portfolio
                </Link>
              )}
            </div>
          </div>

          <div className="rounded-[1.5rem] border border-white/10 bg-[#101525] p-5">
            <p className="text-xs font-medium uppercase tracking-[0.22em] text-white/42">Recruiter readiness</p>
            <div className="mt-4 grid gap-3">
              {summaryStats.map((stat) => (
                <div key={stat.label} className="rounded-2xl border border-white/10 bg-white/5 p-4">
                  <div className="flex items-start justify-between gap-4">
                    <div>
                      <p className="text-sm text-white/50">{stat.label}</p>
                      <p className="mt-1 text-2xl font-semibold tracking-tight text-white">{stat.value}</p>
                    </div>
                  </div>
                  <p className="mt-2 text-sm leading-6 text-white/62">{stat.helper}</p>
                </div>
              ))}
              {status === "loading" && <p className="text-sm text-white/55">Loading your workspace…</p>}
              {status === "error" && (
                <p className="rounded-2xl border border-rose-400/20 bg-rose-500/10 px-4 py-3 text-sm text-rose-100">
                  Couldn&apos;t load your portfolio. Check that the API is running and try again.
                </p>
              )}
            </div>
          </div>
        </div>
      </section>

      {reminder?.nudgeActive && (
        <section className="rounded-[1.5rem] border border-amber-300/18 bg-amber-500/10 p-5 backdrop-blur">
          <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <div>
              <p className="text-sm font-medium text-amber-100">Growth reminder</p>
              <p className="mt-1 text-sm leading-6 text-amber-50/88">
                {reminder.daysSinceLastEntry === null
                  ? "You haven&apos;t written a Learning Lab entry yet. Start building your proof-of-growth trail."
                  : `It&apos;s been ${reminder.daysSinceLastEntry} days since your last Learning Lab entry. Capture the next thing you learned.`}
              </p>
            </div>
            <Link
              href="/dashboard/journal"
              className="inline-flex rounded-full border border-amber-100/20 px-4 py-2 text-sm font-medium text-amber-50 transition hover:bg-amber-50/10"
            >
              Write learning log
            </Link>
          </div>
        </section>
      )}

      <section>
        <div className="mb-5">
          <h2 className="text-2xl font-semibold tracking-tight text-white">Workspace</h2>
          <p className="mt-2 text-sm text-white/58">
            Move through the core parts of your portfolio with clearer context and faster actions.
          </p>
        </div>

        <div className="grid grid-cols-1 gap-5 lg:grid-cols-2">
          {workspaceCards.map((card) => (
            <Link
              key={card.href}
              href={card.href}
              className="group overflow-hidden rounded-[1.5rem] border border-white/10 bg-white/6 p-6 transition hover:-translate-y-0.5 hover:border-white/18 hover:bg-white/8"
            >
              <div className="mb-6 flex items-start justify-between gap-4">
                <div>
                  <p className="text-xs font-medium uppercase tracking-[0.22em] text-white/42">{card.eyebrow}</p>
                  <h3 className="text-xl font-semibold text-white">{card.label}</h3>
                  <p className="mt-2 text-sm text-indigo-100/80">{card.helper}</p>
                </div>
                <span className="rounded-full border border-white/10 px-3 py-1 text-xs font-medium text-white/60 transition group-hover:border-indigo-300/30 group-hover:text-indigo-100">
                  Open
                </span>
              </div>
              <p className="text-sm leading-7 text-white/62">{card.description}</p>
            </Link>
          ))}
        </div>
      </section>
    </div>
  );
}
