"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { ApiError, useApi } from "@/lib/use-api";
import {
  MyPortfolio,
  OnboardingSuggestions,
  ResumeImportResponse,
  SuggestedExperience,
  experienceTypeLabel,
} from "@/lib/types";

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

  return (
    <div className="max-w-2xl">
      <h1 className="text-2xl font-semibold tracking-tight text-zinc-900">Import from GitHub</h1>
      <p className="mt-1 text-sm text-zinc-500">
        Pull suggestions from your public repositories — nothing is saved until you review and add it.
      </p>

      {gitHubVerified === false && (
        <div className="mt-4 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
          Connect your GitHub account before importing — this makes sure you can only pull suggestions
          from repositories you actually own.{" "}
          <Link href="/dashboard/profile" className="font-medium underline">
            Connect it in your profile
          </Link>
          .
        </div>
      )}

      {gitHubVerified && (
        <div className="mt-4 flex items-center gap-3">
          <button
            type="button"
            onClick={handleImport}
            disabled={status === "loading"}
            className="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
          >
            {status === "loading" ? "Importing…" : "Import from GitHub"}
          </button>
          {gitHubUrl && <span className="text-sm text-zinc-500">{gitHubUrl}</span>}
        </div>
      )}

      {status === "error" && errorMessage && (
        <p className="mt-4 text-sm text-red-600">{errorMessage}</p>
      )}

      {suggestions && (
        <div className="mt-8 flex flex-col gap-6">
          {suggestions.experiences.length > 0 && (
            <div>
              <h2 className="text-sm font-medium uppercase tracking-wide text-zinc-500">
                Suggested experience entries
              </h2>
              <div className="mt-3 flex flex-col gap-3">
                {suggestions.experiences.map((exp, i) => {
                  const added = addedExperiences.has(i);
                  return (
                    <div key={i} className="rounded-xl border border-zinc-200 bg-white p-5 shadow-sm">
                      <div className="flex items-start justify-between gap-4">
                        <div>
                          <p className="text-xs font-medium uppercase tracking-wide text-indigo-600">
                            {experienceTypeLabel[exp.experienceType]}
                          </p>
                          <p className="mt-0.5 font-medium text-zinc-900">
                            {exp.title} — {exp.companyLabel}
                          </p>
                          <p className="text-xs text-zinc-400">
                            {exp.startDate} — {exp.endDate ?? "Present"}
                          </p>
                        </div>
                        <button
                          onClick={() => handleAddExperience(i, exp)}
                          disabled={added}
                          className="shrink-0 rounded-lg bg-indigo-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-indigo-700 disabled:bg-zinc-300"
                        >
                          {added ? "Added" : "Add to Experience"}
                        </button>
                      </div>
                      <p className="mt-2 text-sm text-zinc-600">{exp.keyAchievement}</p>
                      {exp.technologies.length > 0 && (
                        <div className="mt-2 flex flex-wrap gap-1.5">
                          {exp.technologies.map((t) => (
                            <span
                              key={t}
                              className="rounded-full border border-zinc-200 bg-zinc-50 px-2 py-0.5 text-xs text-zinc-600"
                            >
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
                          className="mt-2 inline-block text-xs text-indigo-600 underline"
                        >
                          View repo on GitHub
                        </a>
                      )}
                    </div>
                  );
                })}
              </div>
            </div>
          )}

          {suggestions.draftEntry && (
            <div>
              <h2 className="text-sm font-medium uppercase tracking-wide text-zinc-500">
                Suggested Sprint Journal draft
              </h2>
              <div className="mt-3 rounded-xl border border-zinc-200 bg-white p-5 shadow-sm">
                <p className="font-medium text-zinc-900">{suggestions.draftEntry.title}</p>
                <p className="mt-2 whitespace-pre-wrap text-sm text-zinc-600">
                  {suggestions.draftEntry.rawContent}
                </p>
                {draftSaved ? (
                  <p className="mt-3 text-xs text-emerald-600">
                    Saved as a draft.{" "}
                    <Link href="/dashboard/journal" className="underline">
                      Open it in the journal
                    </Link>{" "}
                    to review, sanitize, and publish — same as any other entry.
                  </p>
                ) : (
                  <button
                    onClick={handleUseDraft}
                    className="mt-3 rounded-lg bg-indigo-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-indigo-700"
                  >
                    Use as Sprint Journal draft
                  </button>
                )}
              </div>
            </div>
          )}
        </div>
      )}

      <hr className="mt-10 border-zinc-200" />

      <h1 className="mt-10 text-2xl font-semibold tracking-tight text-zinc-900">Import from Resume</h1>
      <p className="mt-1 text-sm text-zinc-500">
        Upload a PDF resume — we&rsquo;ll host it as your CV download and draft experience suggestions from it.
        Company names are anonymized during extraction, same as everywhere else in DevLog; nothing is saved
        until you review and add it.
      </p>

      <form onSubmit={handleResumeUpload} className="mt-4 flex gap-2">
        <input
          ref={fileInputRef}
          type="file"
          accept="application/pdf"
          className="flex-1 rounded-lg border border-zinc-200 bg-white px-3 py-2 text-sm text-zinc-900 outline-none file:mr-3 file:rounded-md file:border-0 file:bg-zinc-100 file:px-2 file:py-1 file:text-xs"
        />
        <button
          type="submit"
          disabled={resumeStatus === "loading"}
          className="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
        >
          {resumeStatus === "loading" ? "Uploading…" : "Upload"}
        </button>
      </form>

      {resumeStatus === "error" && resumeErrorMessage && (
        <p className="mt-4 text-sm text-red-600">{resumeErrorMessage}</p>
      )}

      {resumeResult && (
        <div className="mt-6 flex flex-col gap-6">
          <p className="text-sm text-emerald-600">
            Resume saved as your CV download.{" "}
            <a href={resumeResult.cvFileUrl} target="_blank" rel="noreferrer" className="underline">
              View file
            </a>
          </p>

          {resumeResult.message && <p className="text-sm text-zinc-500">{resumeResult.message}</p>}

          {resumeResult.suggestions && resumeResult.suggestions.experiences.length > 0 && (
            <div>
              <h2 className="text-sm font-medium uppercase tracking-wide text-zinc-500">
                Suggested experience entries
              </h2>
              <div className="mt-3 flex flex-col gap-3">
                {resumeResult.suggestions.experiences.map((exp, i) => {
                  const added = addedResumeExperiences.has(i);
                  return (
                    <div key={i} className="rounded-xl border border-zinc-200 bg-white p-5 shadow-sm">
                      <div className="flex items-start justify-between gap-4">
                        <div>
                          <p className="text-xs font-medium uppercase tracking-wide text-indigo-600">
                            {experienceTypeLabel[exp.experienceType]}
                          </p>
                          <p className="mt-0.5 font-medium text-zinc-900">
                            {exp.title} — {exp.companyLabel}
                          </p>
                          <p className="text-xs text-zinc-400">
                            {exp.startDate} — {exp.endDate ?? "Present"}
                          </p>
                        </div>
                        <button
                          onClick={() => handleAddResumeExperience(i, exp)}
                          disabled={added}
                          className="shrink-0 rounded-lg bg-indigo-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-indigo-700 disabled:bg-zinc-300"
                        >
                          {added ? "Added" : "Add to Experience"}
                        </button>
                      </div>
                      <p className="mt-2 text-sm text-zinc-600">{exp.keyAchievement}</p>
                      {exp.technologies.length > 0 && (
                        <div className="mt-2 flex flex-wrap gap-1.5">
                          {exp.technologies.map((t) => (
                            <span
                              key={t}
                              className="rounded-full border border-zinc-200 bg-zinc-50 px-2 py-0.5 text-xs text-zinc-600"
                            >
                              {t}
                            </span>
                          ))}
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
