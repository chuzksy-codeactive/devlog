"use client";

import { useEffect, useMemo, useState } from "react";
import { ApiError, useApi } from "@/lib/use-api";
import { AdminInviteEntry, AdminWaitlistSubmission } from "@/lib/types";

function formatDate(iso: string | null) {
  if (!iso) {
    return "—";
  }

  return new Date(iso).toLocaleString();
}

export default function BetaAccessAdminPage() {
  const { request } = useApi();
  const [waitlist, setWaitlist] = useState<AdminWaitlistSubmission[]>([]);
  const [invites, setInvites] = useState<AdminInviteEntry[]>([]);
  const [status, setStatus] = useState<"loading" | "ready" | "forbidden" | "error">("loading");
  const [busyId, setBusyId] = useState<string | null>(null);
  const [message, setMessage] = useState<string>("");

  async function load() {
    try {
      const [waitlistRows, inviteRows] = await Promise.all([
        request<AdminWaitlistSubmission[]>("/api/beta-access/admin/waitlist?status=Pending&take=100"),
        request<AdminInviteEntry[]>("/api/beta-access/admin/invites?status=Active&take=100"),
      ]);
      setWaitlist(waitlistRows);
      setInvites(inviteRows);
      setStatus("ready");
    } catch (error) {
      if (error instanceof ApiError && (error.status === 401 || error.status === 403)) {
        setStatus("forbidden");
        return;
      }
      setStatus("error");
    }
  }

  useEffect(() => {
    Promise.all([
      request<AdminWaitlistSubmission[]>("/api/beta-access/admin/waitlist?status=Pending&take=100"),
      request<AdminInviteEntry[]>("/api/beta-access/admin/invites?status=Active&take=100"),
    ])
      .then(([waitlistRows, inviteRows]) => {
        setWaitlist(waitlistRows);
        setInvites(inviteRows);
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
  }, []);

  const pendingCount = useMemo(
    () => waitlist.filter((entry) => entry.status === "Pending").length,
    [waitlist]
  );

  async function reviewWaitlist(id: string, action: "approve" | "reject") {
    setBusyId(id);
    setMessage("");
    try {
      await request(`/api/beta-access/admin/waitlist/${id}/review`, {
        method: "POST",
        body: JSON.stringify({ action }),
      });
      await load();
      setMessage(action === "approve" ? "Submission approved and invite activated." : "Submission rejected.");
    } catch {
      setMessage("Action failed. Please try again.");
    } finally {
      setBusyId(null);
    }
  }

  async function revokeInvite(id: string) {
    setBusyId(id);
    setMessage("");
    try {
      await request(`/api/beta-access/admin/invites/${id}/revoke`, {
        method: "POST",
        body: JSON.stringify({}),
      });
      await load();
      setMessage("Invite revoked.");
    } catch {
      setMessage("Could not revoke invite.");
    } finally {
      setBusyId(null);
    }
  }

  if (status === "loading") {
    return <p className="text-sm text-white/70">Loading beta access queue…</p>;
  }

  if (status === "forbidden") {
    return (
      <section className="rounded-[1.5rem] border border-amber-300/18 bg-amber-500/10 p-6 text-amber-100">
        You don&apos;t have beta admin permissions.
      </section>
    );
  }

  if (status === "error") {
    return (
      <section className="rounded-[1.5rem] border border-rose-300/20 bg-rose-500/10 p-6 text-rose-100">
        Couldn&apos;t load beta access admin data.
      </section>
    );
  }

  return (
    <div className="flex flex-col gap-8">
      <section className="rounded-[1.75rem] border border-white/10 bg-white/6 p-7">
        <p className="text-xs font-medium uppercase tracking-[0.22em] text-white/45">Private beta operations</p>
        <h1 className="mt-3 text-3xl font-semibold text-white">Manage waitlist and invites</h1>
        <p className="mt-3 text-sm text-white/65">
          Pending requests: <span className="font-medium text-white">{pendingCount}</span> · Active invites:{" "}
          <span className="font-medium text-white">{invites.length}</span>
        </p>
        {message && <p className="mt-4 text-sm text-emerald-200">{message}</p>}
      </section>

      <section className="rounded-[1.75rem] border border-white/10 bg-white/6 p-6">
        <div className="mb-4 flex items-center justify-between gap-3">
          <h2 className="text-xl font-semibold text-white">Pending waitlist</h2>
        </div>
        {waitlist.length === 0 ? (
          <p className="text-sm text-white/60">No pending submissions.</p>
        ) : (
          <div className="grid gap-4">
            {waitlist.map((entry) => (
              <article key={entry.id} className="rounded-2xl border border-white/10 bg-[#111626] p-4">
                <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
                  <div className="max-w-3xl">
                    <p className="text-sm font-medium text-white">{entry.email}</p>
                    <p className="text-xs text-white/45">Submitted {formatDate(entry.createdAt)}</p>
                    {entry.name && <p className="mt-2 text-sm text-white/70">Name: {entry.name}</p>}
                    {entry.reason && <p className="mt-2 text-sm leading-6 text-white/78">{entry.reason}</p>}
                    {entry.useCase && <p className="mt-2 text-sm text-white/65">Use case: {entry.useCase}</p>}
                    {entry.referrer && <p className="mt-1 text-sm text-white/55">Referrer: {entry.referrer}</p>}
                  </div>
                  <div className="flex shrink-0 gap-2">
                    <button
                      onClick={() => reviewWaitlist(entry.id, "approve")}
                      disabled={busyId === entry.id}
                      className="rounded-full bg-emerald-500 px-4 py-2 text-sm font-medium text-white transition hover:bg-emerald-400 disabled:opacity-60"
                    >
                      Approve
                    </button>
                    <button
                      onClick={() => reviewWaitlist(entry.id, "reject")}
                      disabled={busyId === entry.id}
                      className="rounded-full border border-white/14 px-4 py-2 text-sm font-medium text-white/80 transition hover:bg-white/10 disabled:opacity-60"
                    >
                      Reject
                    </button>
                  </div>
                </div>
              </article>
            ))}
          </div>
        )}
      </section>

      <section className="rounded-[1.75rem] border border-white/10 bg-white/6 p-6">
        <h2 className="mb-4 text-xl font-semibold text-white">Active invites</h2>
        {invites.length === 0 ? (
          <p className="text-sm text-white/60">No active invites.</p>
        ) : (
          <div className="grid gap-3">
            {invites.map((entry) => (
              <article key={entry.id} className="flex flex-col gap-3 rounded-2xl border border-white/10 bg-[#111626] p-4 md:flex-row md:items-center md:justify-between">
                <div>
                  <p className="text-sm font-medium text-white">{entry.email}</p>
                  <p className="text-xs text-white/45">
                    Invited by {entry.invitedBy ?? "unknown"} · Updated {formatDate(entry.updatedAt)}
                  </p>
                  {entry.expiresAt && <p className="text-xs text-white/50">Expires {formatDate(entry.expiresAt)}</p>}
                </div>
                <button
                  onClick={() => revokeInvite(entry.id)}
                  disabled={busyId === entry.id}
                  className="w-fit rounded-full border border-rose-300/30 px-4 py-2 text-sm font-medium text-rose-100 transition hover:bg-rose-500/20 disabled:opacity-60"
                >
                  Revoke
                </button>
              </article>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
