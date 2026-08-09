"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import Link from "next/link";
import { ApiError, useApi } from "@/lib/use-api";
import {
  MyPortfolio,
  OnboardingSuggestions,
  ResumeImportResponse,
  SuggestedExperience,
  experienceTypeLabel,
} from "@/lib/types";

const cardTone = "rounded-[1.5rem] border border-white/10 bg-white/6 p-5 backdrop-blur";
const inputClass =
  "w-full rounded-xl border border-white/12 bg-white px-3 py-2 text-sm text-zinc-950 outline-none file:mr-3 file:rounded-md file:border-0 file:bg-zinc-100 file:px-2 file:py-1 file:text-xs placeholder:text-zinc-500 focus:border-indigo-300 focus:bg-white";

export default function ImportPage() {
  const { request } = useApi();
  const [gitHubVerified, setGitHubVerified] = useState<boolean | null>(null);
  const [gitHubUrl, setGitHubUrl] = useState<string | null>(null);
  const [status, setStatus] = useState<"idle" | "loading" | "ready" | "error">("idle");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [suggestions, setSuggestions] = useState<OnboardingSuggestions | null>(null);
  const [addedExperiences, setAddedExperiences] = useState<Set<number>>(new Set());
  const [draftSaved, setDraftSaved] = useState(false);

  useEffect(() => {
    request<MyPortfolio>("/api/portfolio/me")
      .then((data) => {
        setGitHubVerified(data.gitHubVerified);
        setGitHubUrl(data.gitHubUrl);
      })
      .catch(() => setGitHubVerified(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const fileInputRef = useRef<HTMLInputElement>(null);
  const [resumeStatus, setResumeStatus] = useState<"idle" | "loading" | "ready" | "error">("idle");
  const [resumeErrorMessage, setResumeErrorMessage] = useState<string | null>(null);
  const [resumeResult, setResumeResult] = useState<ResumeImportResponse | null>(null);
  const [addedResumeExperiences, setAddedResumeExperiences] = useState<Set<number>>(new Set());

  async function handleImport() {
    setStatus("loading");
    setErrorMessage(null);
    setSuggestions(null);
    setAddedExperiences(new Set());
    setDraftSaved(false);

    try {
      const result = await request<OnboardingSuggestions>("/api/onboarding/github-import", {
        method: "POST",
      });
      setSuggestions(result);
      setStatus("ready");
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.status === 429) {
          setErrorMessage("GitHub's rate limit was reached. Try again in a few minutes.");
        } else if (err.status === 400) {
          setErrorMessage(err.message || "Connect your GitHub account in your profile before importing.");
        } else if (err.status === 404) {
          setErrorMessage(err.message || "No public repositories found for your connected account.");
        } else {
          setErrorMessage("Something went wrong generating suggestions. Try again.");
        }
      } else {
        setErrorMessage("Couldn't reach the API — check that it's running.");
      }
      setStatus("error");
    }
  }

  async function handleAddExperience(index: number, exp: SuggestedExperience) {
    await request("/api/experiences", {
      method: "POST",
      body: JSON.stringify({
        experienceType: exp.experienceType,
        companyLabel: exp.companyLabel,
        title: exp.title,
        startDate: exp.startDate,
        endDate: exp.endDate,
        technologies: exp.technologies,
        keyAchievement: exp.keyAchievement,
      }),
    });
    setAddedExperiences((prev) => new Set(prev).add(index));
  }

  async function handleResumeUpload(e: React.FormEvent) {
    e.preventDefault();
    const file = fileInputRef.current?.files?.[0];
    if (!file) return;

    setResumeStatus("loading");
    setResumeErrorMessage(null);
    setResumeResult(null);
    setAddedResumeExperiences(new Set());

    const formData = new FormData();
    formData.append("file", file);

    try {
      const result = await request<ResumeImportResponse>("/api/onboarding/resume-import", {
        method: "POST",
        body: formData,
      });
      setResumeResult(result);
      setResumeStatus("ready");
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.status === 429) {
          setResumeErrorMessage("Resume import limit reached. Try again in an hour.");
        } else if (err.status === 400) {
          setResumeErrorMessage(err.message || "That file couldn't be processed. Make sure it's a PDF.");
        } else {
          setResumeErrorMessage("Something went wrong generating suggestions. Try again.");
        }
      } else {
        setResumeErrorMessage("Couldn't reach the API — check that it's running.");
      }
      setResumeStatus("error");
    }
  }

  async function handleAddResumeExperience(index: number, exp: SuggestedExperience) {
    await request("/api/experiences", {
      method: "POST",
      body: JSON.stringify({
        experienceType: exp.experienceType,
        companyLabel: exp.companyLabel,
        title: exp.title,
        startDate: exp.startDate,
        endDate: exp.endDate,
        technologies: exp.technologies,
        keyAchievement: exp.keyAchievement,
      }),
    });
    setAddedResumeExperiences((prev) => new Set(prev).add(index));
  }

  async function handleUseDraft() {
    if (!suggestions?.draftEntry) return;
    await request("/api/entries", {
      method: "POST",
      body: JSON.stringify({
        title: suggestions.draftEntry.title,
        entryType: "SprintEntry",
        rawContent: suggestions.draftEntry.rawContent,
      }),
    });
    setDraftSaved(true);
  }

  const summary = useMemo(
    () => [
{
  label: "GitHub",
  value: gitHubVerified === null ? "Checking…" : gitHubVerified ? "Connected" : "Needs setup",
  helper:
    gitHubVerified === null
      ? "Verifying your profile connection"
      : gitHubVerified
        ? gitHubUrl ?? "Verified source ready"
        : "Connect GitHub in your profile before importing",
},
      {
        label: "GitHub suggestions",
        value: suggestions ? String(suggestions.experiences.length) : "0",
        helper: "Potential experience entries from repositories",
      },
      {
        label: "Resume suggestions",
        value: resumeResult?.suggestions ? String(resumeResult.suggestions.experiences.length) : "0",
        helper: "Potential experience entries from your CV",
      },
    ],
    [gitHubUrl, gitHubVerified, resumeResult?.suggestions, suggestions]
  );

  return (
    <div className="flex flex-col gap-8">
      <section className="overflow-hidden rounded-[2rem] border border-white/10 bg-white/6 p-8 shadow-2xl shadow-black/20 backdrop-blur-xl">
        <div className="grid gap-8 lg:grid-cols-[1.05fr_0.95fr]">
          <div>
            <div className="inline-flex items-center gap-2 rounded-full border border-indigo-400/20 bg-indigo-500/10 px-4 py-2 text-sm text-indigo-100">
              <span className="h-2 w-2 rounded-full bg-indigo-300" />
              Guided import
            </div>
            <h1 className="mt-6 text-4xl font-semibold tracking-tight text-white">Bring your work in, then curate what deserves to stay.</h1>
            <p className="mt-4 max-w-2xl text-base leading-7 text-white/68">
              Import suggestions from GitHub or your resume, review them carefully, and only add the pieces that strengthen your portfolio.
            </p>
          </div>

          <div className="grid gap-3 rounded-[1.5rem] border border-white/10 bg-[#101525] p-5">
            <p className="text-xs font-medium uppercase tracking-[0.22em] text-white/42">Import snapshot</p>
            {summary.map((item) => (
              <div key={item.label} className="rounded-2xl border border-white/10 bg-white/5 p-4">
                <p className="text-sm text-white/50">{item.label}</p>
                <p className="mt-1 text-2xl font-semibold tracking-tight text-white">{item.value}</p>
                <p className="mt-2 text-sm leading-6 text-white/62">{item.helper}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="grid gap-6 xl:grid-cols-2">
        <div className={cardTone}>
          <p className="text-xs font-medium uppercase tracking-[0.22em] text-white/42">Source one</p>
          <h2 className="mt-2 text-2xl font-semibold text-white">Import from GitHub</h2>
          <p className="mt-3 text-sm leading-7 text-white/62">
            Pull suggestions from your public repositories. Real project names are preserved. Nothing is saved until you review and explicitly add it.
          </p>

          {gitHubVerified === false && (
            <div className="mt-5 rounded-[1.25rem] border border-amber-300/18 bg-amber-500/10 px-4 py-3 text-sm text-amber-100">
              Connect your GitHub account before importing.{" "}
              <Link href="/dashboard/profile" className="font-medium underline">
                Open profile setup
              </Link>
              .
            </div>
          )}

          {gitHubVerified && (
            <div className="mt-5 flex flex-wrap items-center gap-3">
              <button
                type="button"
                onClick={handleImport}
                disabled={status === "loading"}
                className="rounded-full bg-indigo-500 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-indigo-500/25 transition hover:bg-indigo-400 disabled:opacity-50"
              >
                {status === "loading" ? "Importing…" : "Import from GitHub"}
              </button>
              {gitHubUrl && <span className="text-sm text-white/55">{gitHubUrl}</span>}
            </div>
          )}

          {status === "error" && errorMessage && <p className="mt-4 text-sm text-rose-300">{errorMessage}</p>}
        </div>

        <div className={cardTone}>
          <p className="text-xs font-medium uppercase tracking-[0.22em] text-white/42">Source two</p>
          <h2 className="mt-2 text-2xl font-semibold text-white">Import from resume</h2>
          <p className="mt-3 text-sm leading-7 text-white/62">
            Upload a PDF resume to host it as your CV download and generate experience suggestions from it. Real company names are preserved.
          </p>

          <form onSubmit={handleResumeUpload} className="mt-5 flex flex-col gap-3 sm:flex-row">
            <input
              ref={fileInputRef}
              type="file"
              accept="application/pdf"
              className={`${inputClass} flex-1`}
            />
            <button
              type="submit"
              disabled={resumeStatus === "loading"}
              className="rounded-full bg-indigo-500 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-indigo-500/25 transition hover:bg-indigo-400 disabled:opacity-50"
            >
              {resumeStatus === "loading" ? "Uploading…" : "Upload PDF"}
            </button>
          </form>

          {resumeStatus === "error" && resumeErrorMessage && <p className="mt-4 text-sm text-rose-300">{resumeErrorMessage}</p>}

          {resumeResult && (
            <div className="mt-5 rounded-[1.25rem] border border-emerald-300/18 bg-emerald-500/10 p-4">
              <p className="text-sm text-emerald-100">
                Resume saved as your CV download.{" "}
                <a href={resumeResult.cvFileUrl} target="_blank" rel="noreferrer" className="font-medium underline">
                  View file
                </a>
              </p>
              {resumeResult.message && <p className="mt-2 text-sm text-white/65">{resumeResult.message}</p>}
            </div>
          )}
        </div>
      </section>

      {suggestions && (
        <section className="flex flex-col gap-6">
          <div className="rounded-[1.25rem] border border-indigo-300/18 bg-indigo-500/10 p-4">
            <p className="text-sm text-indigo-100">
              <span className="font-semibold">👁️ These will be public:</span> The company names and project details from your GitHub and resume are preserved and will be visible to recruiters on your public portfolio. Edit any entry before saving if you'd like to change details.
            </p>
          </div>
          {suggestions.experiences.length > 0 && (
            <div>
              <div className="mb-4">
                <p className="text-xs font-medium uppercase tracking-[0.22em] text-white/42">GitHub suggestions</p>
                <h2 className="mt-2 text-2xl font-semibold text-white">Suggested experience entries</h2>
              </div>
              <div className="flex flex-col gap-4">
                {suggestions.experiences.map((exp, i) => {
                  const added = addedExperiences.has(i);
                  return (
                    <SuggestionCard
                      key={`github-${i}`}
                      exp={exp}
                      added={added}
                      actionLabel={added ? "Added" : "Add to Experience"}
                      onAdd={() => handleAddExperience(i, exp)}
                    />
                  );
                })}
              </div>
            </div>
          )}

          {suggestions.draftEntry && (
            <div className={cardTone}>
              <p className="text-xs font-medium uppercase tracking-[0.22em] text-white/42">Journal suggestion</p>
              <h2 className="mt-2 text-2xl font-semibold text-white">{suggestions.draftEntry.title}</h2>
              <p className="mt-4 whitespace-pre-wrap text-sm leading-7 text-white/72">{suggestions.draftEntry.rawContent}</p>
              {draftSaved ? (
                <p className="mt-4 text-sm text-emerald-200">
                  Saved as a draft.{" "}
                  <Link href="/dashboard/journal" className="underline">
                    Open it in the journal
                  </Link>{" "}
                  to review, sanitize, and publish.
                </p>
              ) : (
                <button
                  onClick={handleUseDraft}
                  className="mt-5 rounded-full bg-indigo-500 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-indigo-500/25 transition hover:bg-indigo-400"
                >
                  Use as Sprint Journal draft
                </button>
              )}
            </div>
          )}
        </section>
      )}

      {resumeResult?.suggestions && resumeResult.suggestions.experiences.length > 0 && (
        <section>
          <div className="mb-4 rounded-[1.25rem] border border-indigo-300/18 bg-indigo-500/10 p-4">
            <p className="text-sm text-indigo-100">
              <span className="font-semibold">👁️ These will be public:</span> The company names and job details extracted from your resume will be visible to recruiters on your public portfolio. Edit any entry before saving if needed.
            </p>
          </div>
          <div className="mb-4">
            <p className="text-xs font-medium uppercase tracking-[0.22em] text-white/42">Resume suggestions</p>
            <h2 className="mt-2 text-2xl font-semibold text-white">Suggested experience entries</h2>
          </div>
          <div className="flex flex-col gap-4">
            {resumeResult.suggestions.experiences.map((exp, i) => {
              const added = addedResumeExperiences.has(i);
              return (
                <SuggestionCard
                  key={`resume-${i}`}
                  exp={exp}
                  added={added}
                  actionLabel={added ? "Added" : "Add to Experience"}
                  onAdd={() => handleAddResumeExperience(i, exp)}
                />
              );
            })}
          </div>
        </section>
      )}
    </div>
  );
}

function SuggestionCard({
  exp,
  added,
  actionLabel,
  onAdd,
}: {
  exp: SuggestedExperience;
  added: boolean;
  actionLabel: string;
  onAdd: () => void;
}) {
  return (
    <article className="rounded-[1.75rem] border border-white/10 bg-white/6 p-6 transition hover:border-white/15 hover:bg-white/8">
      <div className="flex flex-col gap-5 lg:flex-row lg:items-start lg:justify-between">
        <div className="max-w-3xl">
          <p className="text-xs font-medium uppercase tracking-[0.18em] text-indigo-100/72">
            {experienceTypeLabel[exp.experienceType]}
          </p>
          <h3 className="mt-3 text-2xl font-semibold tracking-tight text-white">
            {exp.title} <span className="text-white/50">— {exp.companyLabel}</span>
          </h3>
          <p className="mt-2 text-sm text-white/45">
            {exp.startDate} — {exp.endDate ?? "Present"}
          </p>
          <p className="mt-4 text-base leading-8 text-white/72">{exp.keyAchievement}</p>
        </div>

<button
  type="button"
  onClick={onAdd}
          disabled={added}
          className={`shrink-0 rounded-full px-4 py-2 text-sm font-medium transition ${
            added
              ? "bg-white/12 text-white/55"
              : "bg-indigo-500 text-white shadow-lg shadow-indigo-500/25 hover:bg-indigo-400"
          }`}
        >
          {actionLabel}
        </button>
      </div>

      {exp.technologies.length > 0 && (
        <div className="mt-5 flex flex-wrap gap-2">
          {exp.technologies.map((t) => (
            <span key={t} className="rounded-full border border-white/10 bg-white/5 px-3 py-1 text-xs font-medium text-white/72">
              {t}
            </span>
          ))}
        </div>
      )}

      {exp.repoUrl && (
        <a
          href={exp.repoUrl}
          target="_blank"
          rel="noreferrer"
          className="mt-4 inline-flex text-sm font-medium text-indigo-100 underline underline-offset-2"
        >
          View repo on GitHub
        </a>
      )}
    </article>
  );
}
