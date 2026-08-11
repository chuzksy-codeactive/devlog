"use client";

import { useEffect, useRef, useState } from "react";
import { useReverification, useUser } from "@clerk/nextjs";
import { isReverificationCancelledError } from "@clerk/nextjs/errors";
import Link from "next/link";
import { useApi } from "@/lib/use-api";
import { Discipline, MyPortfolio, PortfolioVisibility, ProofLink } from "@/lib/types";
import { disciplineDisplayName, disciplineLabels } from "@/lib/discipline-labels";

type FormState = {
  displayName: string;
  headline: string;
  positioningLine: string;
  photoUrl: string;
  currentRole: string;
  yearsExperience: string;
  coreTechnologies: string; // comma-separated in the form, array on the wire
  linkedInUrl: string;
  gitHubUrl: string;
  gitHubVerified: boolean;
  hasUploadedCv: boolean;
  discipline: Discipline;
  proofLinks: ProofLink[];
  visibility: PortfolioVisibility;
  discoverable: boolean;
  notificationEmail: string;
};

function toFormState(p: MyPortfolio): FormState {
  return {
    displayName: p.displayName,
    headline: p.headline,
    positioningLine: p.positioningLine,
    photoUrl: p.photoUrl ?? "",
    currentRole: p.currentRole ?? "",
    yearsExperience: p.yearsExperience ?? "",
    coreTechnologies: p.coreTechnologies.join(", "),
    linkedInUrl: p.linkedInUrl ?? "",
    gitHubUrl: p.gitHubUrl ?? "",
    gitHubVerified: p.gitHubVerified,
    hasUploadedCv: !!p.cvFileUrl,
    discipline: p.discipline,
    proofLinks: p.proofLinks,
    visibility: p.visibility,
    discoverable: p.discoverable,
    notificationEmail: p.notificationEmail ?? "",
  };
}

export default function ProfilePage() {
  const { request } = useApi();
  const { user } = useUser();
  const [slug, setSlug] = useState<string | null>(null);
  const [form, setForm] = useState<FormState | null>(null);
  const [status, setStatus] = useState<"loading" | "ready" | "saving" | "saved" | "error">("loading");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [gitHubStatus, setGitHubStatus] = useState<"idle" | "connecting" | "syncing">("idle");
  const [gitHubErrorMessage, setGitHubErrorMessage] = useState<string | null>(null);
  const autoSyncedRef = useRef(false);

  const createExternalAccountWithReverification = useReverification(
    (params: { strategy: "oauth_github"; redirectUrl: string }) => user!.createExternalAccount(params)
  );

  useEffect(() => {
    request<MyPortfolio>("/api/portfolio/me")
      .then((data) => {
        setSlug(data.slug);
        setForm(toFormState(data));
        setStatus("ready");
      })
      .catch(() => setStatus("error"));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (autoSyncedRef.current || !form || form.gitHubVerified || !user) {
      return;
    }
    const hasGitHubExternalAccount = user.externalAccounts?.some((a) => a.provider === "github");
    if (!hasGitHubExternalAccount) {
      return;
    }
    autoSyncedRef.current = true;
    void (async () => {
      try {
        const updated = await request<MyPortfolio>("/api/portfolio/me/github/sync", { method: "POST" });
        setForm((prev) =>
          prev ? { ...prev, gitHubUrl: updated.gitHubUrl ?? "", gitHubVerified: updated.gitHubVerified } : prev,
        );
      } catch {
      }
    })();
  }, [form, request, user]);

  async function handleConnectGitHub() {
    if (!user) return;
    setGitHubStatus("connecting");
    setGitHubErrorMessage(null);
    try {
      const externalAccount = await createExternalAccountWithReverification({
        strategy: "oauth_github",
        redirectUrl: window.location.href,
      });
      const redirectUrl = externalAccount?.verification?.externalVerificationRedirectURL;
      if (redirectUrl) {
        window.location.href = redirectUrl.toString();
      } else {
        setGitHubStatus("idle");
      }
    } catch (err) {
      if (isReverificationCancelledError(err)) {
        setGitHubErrorMessage("Verification was cancelled — try again to connect GitHub.");
      } else {
        setGitHubErrorMessage("Couldn't start the GitHub connection. Try again.");
      }
      setGitHubStatus("idle");
    }
  }

  function update<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm((prev) => (prev ? { ...prev, [key]: value } : prev));
  }

  function updateProofLink(index: number, field: keyof ProofLink, value: string) {
    setForm((prev) => {
      if (!prev) return prev;
      const proofLinks = prev.proofLinks.map((link, i) =>
        i === index ? { ...link, [field]: value } : link
      );
      return { ...prev, proofLinks };
    });
  }

  function addProofLink() {
    setForm((prev) =>
      prev ? { ...prev, proofLinks: [...prev.proofLinks, { label: "", url: "" }] } : prev
    );
  }

  function removeProofLink(index: number) {
    setForm((prev) =>
      prev ? { ...prev, proofLinks: prev.proofLinks.filter((_, i) => i !== index) } : prev
    );
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!form) return;

    setStatus("saving");
    setErrorMessage(null);

    try {
      await request("/api/portfolio/me", {
        method: "PUT",
        body: JSON.stringify({
          headline: form.headline,
          displayName: form.displayName,
          positioningLine: form.positioningLine,
          photoUrl: form.photoUrl || null,
          currentRole: form.currentRole || null,
          yearsExperience: form.yearsExperience || null,
          coreTechnologies: form.coreTechnologies
            .split(",")
            .map((t) => t.trim())
            .filter(Boolean),
          linkedInUrl: form.linkedInUrl || null,
          gitHubUrl: form.gitHubUrl || null,
          discipline: form.discipline,
          proofLinks: form.proofLinks
            .map((l) => ({ label: l.label.trim(), url: l.url.trim() }))
            .filter((l) => l.label || l.url),
          visibility: form.visibility,
          discoverable: form.discoverable,
          notificationEmail: form.notificationEmail || null,
        }),
      });
      setStatus("saved");
    } catch {
      setErrorMessage("Couldn't save — check that the API is running.");
      setStatus("error");
    }
  }

  if (status === "loading") {
    return <p className="text-sm text-white/60">Loading your profile…</p>;
  }

  if (!form) {
    return <p className="text-sm text-rose-300">Couldn&apos;t load your profile — check that the API is running.</p>;
  }

  return (
    <div className="max-w-2xl">
      <h1 className="text-2xl font-semibold tracking-tight text-white">Profile</h1>
      {slug && <p className="mt-1 text-sm text-white/60">Public URL: workfolio.app/{slug}</p>}

      <form onSubmit={handleSubmit} className="mt-6 flex flex-col gap-5">
        <Field label="Display name" hint="Shown as the page title on your Home page.">
          <input
            type="text"
            required
            value={form.displayName}
            onChange={(e) => update("displayName", e.target.value)}
            className={inputClass}
          />
        </Field>

        <Field label="Positioning line" hint="One-line hero statement — not a generic 'About me.'">
          <textarea
            rows={2}
            value={form.positioningLine}
            onChange={(e) => update("positioningLine", e.target.value)}
            className={inputClass}
          />
        </Field>

        <Field label="Current role" hint="Leave blank to show 'Actively building — open to opportunities.'">
          <input
            type="text"
            value={form.currentRole}
            onChange={(e) => update("currentRole", e.target.value)}
            className={inputClass}
          />
        </Field>

        <Field label="Headline" hint="Internal label — not currently shown on the public page.">
          <input
            type="text"
            value={form.headline}
            onChange={(e) => update("headline", e.target.value)}
            className={inputClass}
          />
        </Field>

        <Field label="Discipline" hint="Picks the right copy for a few fields below — no other effect.">
          <select
            value={form.discipline}
            onChange={(e) => update("discipline", e.target.value as Discipline)}
            className={inputClass}
          >
            {Object.entries(disciplineDisplayName).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </Field>

        <Field
          label={disciplineLabels[form.discipline].coreTechnologiesLabel}
          hint={`${disciplineLabels[form.discipline].coreTechnologiesHint} Comma-separated.`}
        >
          <input
            type="text"
            value={form.coreTechnologies}
            onChange={(e) => update("coreTechnologies", e.target.value)}
            className={inputClass}
          />
        </Field>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-white/78">GitHub</label>
            {form.gitHubVerified ? (
              <div className="mt-1 flex items-center gap-2 rounded-xl border border-white/12 bg-white/8 px-3 py-2 text-sm text-white">
                <span className="truncate">{form.gitHubUrl}</span>
                <span className="shrink-0 rounded-full bg-emerald-400/15 px-2 py-0.5 text-xs font-medium text-emerald-200">
                  Verified
                </span>
              </div>
            ) : (
              <button
                type="button"
                onClick={handleConnectGitHub}
                disabled={gitHubStatus !== "idle"}
                className="mt-1 w-full rounded-xl border border-white/12 bg-white/8 px-3 py-2 text-sm font-medium text-white transition hover:bg-white/12 disabled:opacity-50"
              >
                {gitHubStatus === "connecting" ? "Redirecting to GitHub…" : gitHubStatus === "syncing" ? "Confirming…" : "Connect GitHub"}
              </button>
            )}
            <span className="mt-1 block text-xs font-normal text-white/45">
              {form.gitHubVerified
                ? "Sourced from your connected GitHub account — disconnect it in Clerk to change this."
                : "Connects your real GitHub account so the link on your public page is verified, not just typed."}
            </span>
            {gitHubErrorMessage && <p className="mt-1 text-xs text-rose-300">{gitHubErrorMessage}</p>}
          </div>
          <Field label="LinkedIn URL">
            <input
              type="url"
              value={form.linkedInUrl}
              onChange={(e) => update("linkedInUrl", e.target.value)}
              className={inputClass}
            />
          </Field>
        </div>

        <Field label="CV uploaded" hint="Upload or replace your CV from the import page.">
          <div className="mt-1 flex items-center justify-between rounded-xl border border-white/12 bg-white/8 px-3 py-2 text-sm text-white">
            <span>{form.hasUploadedCv ? "Yes" : "No"}</span>
            <Link href="/dashboard/import" className="text-indigo-200 underline hover:text-indigo-100">
              Go to import
            </Link>
          </div>
        </Field>

        <Field label="Photo URL" hint="Use a direct image URL (for example .jpg/.png or a direct CDN file URL). Google Photos share links don't expose raw image files here.">
          <input
            type="url"
            value={form.photoUrl}
            onChange={(e) => update("photoUrl", e.target.value)}
            className={inputClass}
          />
        </Field>

        <div>
          <label className="block text-sm font-medium text-white/78">
            {disciplineLabels[form.discipline].proofLinksLabel}
            <span className="mt-1 block text-xs font-normal text-white/45">
              Optional extra links — a portfolio site, published articles, certifications.
            </span>
          </label>
          <div className="mt-2 flex flex-col gap-2">
            {form.proofLinks.map((link, i) => (
              <div key={i} className="flex items-center gap-2">
                <input
                  type="text"
                  placeholder="Label"
                  value={link.label}
                  onChange={(e) => updateProofLink(i, "label", e.target.value)}
                  className={`${inputClass} mt-0 w-32`}
                />
                <input
                  type="url"
                  placeholder="https://…"
                  value={link.url}
                  onChange={(e) => updateProofLink(i, "url", e.target.value)}
                  className={`${inputClass} mt-0 flex-1`}
                />
                <button
                  type="button"
                  onClick={() => removeProofLink(i)}
                  className="shrink-0 rounded-xl border border-white/12 px-2.5 py-2 text-xs font-medium text-white/70 transition hover:bg-white/10"
                >
                  Remove
                </button>
              </div>
            ))}
            <button
              type="button"
              onClick={addProofLink}
              className="self-start rounded-xl border border-white/12 px-3 py-1.5 text-xs font-medium text-white/80 transition hover:bg-white/10"
            >
              + Add link
            </button>
          </div>
        </div>

        <Field
          label="Notification email"
          hint="Where you're emailed when a verified interview request comes in."
        >
          <input
            type="email"
            value={form.notificationEmail}
            onChange={(e) => update("notificationEmail", e.target.value)}
            className={inputClass}
          />
        </Field>

        <Field label="Visibility">
          <select
            value={form.visibility}
            onChange={(e) => update("visibility", e.target.value as PortfolioVisibility)}
            className={inputClass}
          >
            <option value="Unlisted">Unlisted — reachable by direct link only</option>
            <option value="Public">Public</option>
            <option value="Paused">Paused — hidden from everyone, including your own link</option>
          </select>
        </Field>

        {errorMessage && <p className="text-sm text-rose-300">{errorMessage}</p>}

        <div className="flex items-center gap-3">
          <button
            type="submit"
            disabled={status === "saving"}
            className="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
          >
            {status === "saving" ? "Saving…" : "Save changes"}
          </button>
          {status === "saved" && <span className="text-sm text-emerald-300">Saved.</span>}
        </div>
      </form>
    </div>
  );
}

const inputClass =
  "mt-1 w-full rounded-xl border border-white/12 bg-white px-3 py-2 text-sm text-zinc-950 outline-none placeholder:text-zinc-500 focus:border-indigo-300 focus:bg-white";

function Field({
  label,
  hint,
  children,
}: {
  label: string;
  hint?: string;
  children: React.ReactNode;
}) {
  return (
    <label className="block text-sm font-medium text-white/78">
      {label}
      {children}
      {hint && <span className="mt-1 block text-xs font-normal text-white/45">{hint}</span>}
    </label>
  );
}
