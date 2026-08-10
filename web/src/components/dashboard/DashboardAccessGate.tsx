"use client";

import { FormEvent, ReactNode, useEffect, useMemo, useState } from "react";
import { ApiError, useApi } from "@/lib/use-api";
import { BetaAccessStatusResponse, WaitlistSubmissionStatus } from "@/lib/types";

type WaitlistFormState = {
  email: string;
  name: string;
  reason: string;
  useCase: string;
  referrer: string;
};

const EMPTY_FORM: WaitlistFormState = {
  email: "",
  name: "",
  reason: "",
  useCase: "",
  referrer: "",
};

const blockedLabel: Record<string, string> = {
  Unauthenticated: "Sign in required",
  MissingEmailClaim: "Email claim missing",
  EmailNotVerified: "Verify your email",
  NotInvited: "Invite required",
  InvitePending: "Invite pending",
  InviteExpired: "Invite expired",
  InviteRevoked: "Invite revoked",
};

export function DashboardAccessGate({ children }: { children: ReactNode }) {
  const { request } = useApi();
  const [status, setStatus] = useState<"loading" | "ready" | "error">("loading");
  const [beta, setBeta] = useState<BetaAccessStatusResponse | null>(null);
  const [form, setForm] = useState<WaitlistFormState>(EMPTY_FORM);
  const [submitStatus, setSubmitStatus] = useState<"idle" | "submitting" | "done" | "error">("idle");
  const [submitMessage, setSubmitMessage] = useState<string>("");

  useEffect(() => {
    request<BetaAccessStatusResponse>("/api/beta-access/status")
      .then((data) => {
        setBeta(data);
        setForm((prev) => ({ ...prev, email: data.resolvedEmail ?? prev.email }));
        setStatus("ready");
      })
      .catch(() => setStatus("error"));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const canAccess = useMemo(() => {
    if (status !== "ready" || !beta) {
      return false;
    }

    return beta.canAccessCreatorFeatures;
  }, [beta, status]);

  async function submitWaitlist(e: FormEvent) {
    e.preventDefault();
    setSubmitStatus("submitting");
    setSubmitMessage("");

    try {
      const result = await request<{ submitted: boolean; alreadySubmitted: boolean; status: WaitlistSubmissionStatus }>(
        "/api/beta-access/waitlist",
        {
          method: "POST",
          body: JSON.stringify({
            email: form.email,
            name: form.name,
            reason: form.reason,
            useCase: form.useCase,
            referrer: form.referrer,
          }),
        }
      );

      setSubmitStatus("done");
      setSubmitMessage(
        result.alreadySubmitted
          ? "You already have a pending waitlist request. We will notify you after review."
          : "Request submitted. We will review and grant access when a beta slot is available."
      );
      setBeta((prev) =>
        prev
          ? {
              ...prev,
              waitlistSubmissionStatus: result.status,
            }
          : prev
      );
    } catch (err) {
      if (err instanceof ApiError) {
        setSubmitMessage(err.message || "Could not submit your request right now.");
      } else {
        setSubmitMessage("Could not submit your request right now.");
      }
      setSubmitStatus("error");
    }
  }

  if (status === "loading") {
    return (
      <div className="rounded-[1.5rem] border border-white/10 bg-white/6 p-6 text-sm text-white/70">
        Checking private beta access…
      </div>
    );
  }

  if (status === "error" || !beta) {
    return (
      <div className="rounded-[1.5rem] border border-rose-400/20 bg-rose-500/10 p-6 text-sm text-rose-100">
        We couldn&apos;t verify your access status. Reload and try again.
      </div>
    );
  }

  if (!canAccess) {
    const statusLabel = blockedLabel[beta.outcome] ?? "Access restricted";
    const pending = beta.waitlistSubmissionStatus === "Pending";
    return (
      <section className="mx-auto w-full max-w-3xl rounded-[1.75rem] border border-indigo-300/20 bg-indigo-500/10 p-8">
        <p className="text-xs font-medium uppercase tracking-[0.22em] text-indigo-100/70">Private beta</p>
        <h1 className="mt-3 text-3xl font-semibold text-white">{statusLabel}</h1>
        <p className="mt-4 text-sm leading-7 text-indigo-50/90">
          This creator workspace is currently invite-only while we roll out access in phases.
        </p>
        {beta.resolvedEmail && (
          <p className="mt-3 text-sm text-indigo-100/80">
            Signed in as <span className="font-medium text-white">{beta.resolvedEmail}</span>
          </p>
        )}

        {pending ? (
          <div className="mt-6 rounded-2xl border border-amber-300/20 bg-amber-500/10 px-4 py-3 text-sm text-amber-100">
            You already have a pending waitlist request. We&apos;ll notify you once it&apos;s reviewed.
          </div>
        ) : (
          <form onSubmit={submitWaitlist} className="mt-6 grid gap-4">
            {!beta.resolvedEmail && (
              <label className="grid gap-1 text-sm text-white/80">
                Email
                <input
                  type="email"
                  inputMode="email"
                  autoComplete="email"
                  value={form.email}
                  onChange={(e) => setForm((prev) => ({ ...prev, email: e.target.value }))}
                  className="rounded-xl border border-white/12 bg-white px-3 py-2 text-zinc-950 outline-none"
                  required
                />
              </label>
            )}
            <label className="grid gap-1 text-sm text-white/80">
              Name (optional)
              <input
                value={form.name}
                onChange={(e) => setForm((prev) => ({ ...prev, name: e.target.value }))}
                className="rounded-xl border border-white/12 bg-white px-3 py-2 text-zinc-950 outline-none"
              />
            </label>
            <label className="grid gap-1 text-sm text-white/80">
              Why do you want access? (optional)
              <textarea
                value={form.reason}
                onChange={(e) => setForm((prev) => ({ ...prev, reason: e.target.value }))}
                className="min-h-24 rounded-xl border border-white/12 bg-white px-3 py-2 text-zinc-950 outline-none"
              />
            </label>
            <label className="grid gap-1 text-sm text-white/80">
              Use case (optional)
              <textarea
                value={form.useCase}
                onChange={(e) => setForm((prev) => ({ ...prev, useCase: e.target.value }))}
                className="min-h-20 rounded-xl border border-white/12 bg-white px-3 py-2 text-zinc-950 outline-none"
              />
            </label>
            <label className="grid gap-1 text-sm text-white/80">
              Referrer (optional)
              <input
                value={form.referrer}
                onChange={(e) => setForm((prev) => ({ ...prev, referrer: e.target.value }))}
                className="rounded-xl border border-white/12 bg-white px-3 py-2 text-zinc-950 outline-none"
              />
            </label>
            <button
              type="submit"
              disabled={submitStatus === "submitting"}
              className="mt-2 w-fit rounded-full bg-indigo-500 px-5 py-2.5 text-sm font-medium text-white transition hover:bg-indigo-400 disabled:opacity-60"
            >
              {submitStatus === "submitting" ? "Submitting…" : "Join waitlist"}
            </button>
            {(submitStatus === "done" || submitStatus === "error") && (
              <p className={`text-sm ${submitStatus === "done" ? "text-emerald-200" : "text-rose-200"}`}>{submitMessage}</p>
            )}
          </form>
        )}
      </section>
    );
  }

  return <>{children}</>;
}
