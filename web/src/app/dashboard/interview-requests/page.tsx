"use client";

import { useEffect, useMemo, useState } from "react";
import { useApi } from "@/lib/use-api";
import { MyInterviewRequest } from "@/lib/types";

const STATUS_STYLE: Record<MyInterviewRequest["status"], string> = {
  Pending: "border-amber-300/18 bg-amber-500/10 text-amber-100",
  Accepted: "border-emerald-300/18 bg-emerald-500/10 text-emerald-100",
  Declined: "border-white/12 bg-white/7 text-white/65",
};

const inputClass =
  "mt-1 w-full rounded-xl border border-white/12 bg-white px-3 py-2 text-sm text-zinc-950 outline-none placeholder:text-zinc-500 focus:border-indigo-300 focus:bg-white";

function formatRequestDate(iso: string) {
return new Date(iso).toLocaleString(undefined, {
    month: "short",
    day: "numeric",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
  });
}

function summarizeRequests(requests: MyInterviewRequest[]) {
  return [
    {
      label: "Total requests",
      value: String(requests.length),
      helper: requests.length > 0 ? "Verified recruiter messages received" : "No verified outreach yet",
    },
    {
      label: "Pending",
      value: String(requests.filter((request) => request.status === "Pending").length),
      helper: "Waiting for your response",
    },
    {
      label: "Accepted",
      value: String(requests.filter((request) => request.status === "Accepted").length),
      helper: "You shared next-step contact details",
    },
  ];
}

export default function InterviewRequestsPage() {
  const { request } = useApi();
  const [requests, setRequests] = useState<MyInterviewRequest[]>([]);
  const [status, setStatus] = useState<"loading" | "ready" | "error">("loading");
  const [busyId, setBusyId] = useState<string | null>(null);
  const [acceptingId, setAcceptingId] = useState<string | null>(null);
  const [contactInstructions, setContactInstructions] = useState("");
  const [error, setError] = useState<string | null>(null);

  function load() {
    request<MyInterviewRequest[]>("/api/interview-requests")
      .then((data) => {
        setRequests(data);
        setStatus("ready");
      })
      .catch(() => setStatus("error"));
  }

  useEffect(load, []); // eslint-disable-line react-hooks/exhaustive-deps

  async function handleAccept(id: string) {
    if (!contactInstructions.trim()) {
      setError("Contact instructions are required to accept — this is what gets revealed to the recruiter.");
      return;
    }
    setBusyId(id);
    setError(null);
    try {
      await request(`/api/interview-requests/${id}/accept`, {
        method: "POST",
        body: JSON.stringify({ contactInstructions: contactInstructions.trim() }),
      });
      setAcceptingId(null);
      setContactInstructions("");
      load();
    } catch {
      setError("Couldn't accept — check that the API is running.");
    } finally {
      setBusyId(null);
    }
  }

  async function handleDecline(id: string) {
    setBusyId(id);
    setError(null);
    try {
      await request(`/api/interview-requests/${id}/decline`, { method: "POST" });
      load();
    } catch {
      setError("Couldn't decline — check that the API is running.");
    } finally {
      setBusyId(null);
    }
  }

  const summary = useMemo(() => summarizeRequests(requests), [requests]);

  return (
    <div className="flex flex-col gap-8">
      <section className="overflow-hidden rounded-[2rem] border border-white/10 bg-white/6 p-8 shadow-2xl shadow-black/20 backdrop-blur-xl">
        <div className="grid gap-8 lg:grid-cols-[1.05fr_0.95fr]">
          <div>
            <div className="inline-flex items-center gap-2 rounded-full border border-indigo-400/20 bg-indigo-500/10 px-4 py-2 text-sm text-indigo-100">
              <span className="h-2 w-2 rounded-full bg-indigo-300" />
              Recruiter inbox
            </div>
            <h1 className="mt-6 text-4xl font-semibold tracking-tight text-white">Review verified interview requests with confidence.</h1>
            <p className="mt-4 max-w-2xl text-base leading-7 text-white/68">
              Only confirmed recruiter emails make it here. Accept to reveal your contact instructions, or decline without exposing your details.
            </p>
          </div>

          <div className="grid gap-3 rounded-[1.5rem] border border-white/10 bg-[#101525] p-5">
            <p className="text-xs font-medium uppercase tracking-[0.22em] text-white/42">Inbox snapshot</p>
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

      {status === "error" && (
        <p className="rounded-2xl border border-rose-400/20 bg-rose-500/10 px-4 py-3 text-sm text-rose-200">
          Couldn&apos;t load requests.
        </p>
      )}

      {status === "ready" && requests.length === 0 && (
        <section className="rounded-[1.75rem] border border-dashed border-white/12 bg-white/4 p-10 text-center">
          <div className="mx-auto max-w-2xl">
            <p className="text-xs font-medium uppercase tracking-[0.22em] text-white/42">Empty inbox</p>
            <h2 className="mt-3 text-2xl font-semibold text-white">No interview requests yet</h2>
            <p className="mt-3 text-sm leading-7 text-white/58">
              When a recruiter verifies their email and requests to interview you, it will appear here for review.
            </p>
          </div>
        </section>
      )}

      <section className="flex flex-col gap-5">
        {status === "loading" && <p className="text-sm text-white/60">Loading requests…</p>}

        {requests.map((r) => (
          <article key={r.id} className="rounded-[1.75rem] border border-white/10 bg-white/6 p-6 transition hover:border-white/15 hover:bg-white/8">
            <div className="flex flex-col gap-5 lg:flex-row lg:items-start lg:justify-between">
              <div className="max-w-3xl">
                <div className="flex flex-wrap items-center gap-3">
                  <span className={`rounded-full border px-3 py-1 text-xs font-medium ${STATUS_STYLE[r.status]}`}>
                    {r.status}
                  </span>
                  <span className="text-sm text-white/45">{formatRequestDate(r.createdAt)}</span>
                </div>
                <h2 className="mt-4 text-2xl font-semibold tracking-tight text-white">
                  {r.recruiterName ? r.recruiterName : "Verified recruiter"}
                </h2>
                <p className="mt-1 text-sm text-white/58">{r.recruiterEmail}</p>
                <p className="mt-4 text-base leading-8 text-white/72">{r.note}</p>
              </div>

              {r.status === "Pending" && acceptingId !== r.id && (
                <div className="flex gap-3 text-sm font-medium">
                  <button
                    onClick={() => setAcceptingId(r.id)}
                    className="rounded-full bg-indigo-500 px-4 py-2 text-white shadow-lg shadow-indigo-500/25 transition hover:bg-indigo-400"
                  >
                    Accept
                  </button>
                  <button
                    onClick={() => handleDecline(r.id)}
                    disabled={busyId === r.id}
                    className="rounded-full border border-white/12 px-4 py-2 text-white/75 transition hover:bg-white/10 hover:text-white disabled:opacity-50"
                  >
                    Decline
                  </button>
                </div>
              )}
            </div>

            {r.status === "Accepted" && r.contactInstructions && (
              <div className="mt-5 rounded-[1.25rem] border border-emerald-300/18 bg-emerald-500/10 p-4">
                <p className="text-xs font-medium uppercase tracking-[0.22em] text-emerald-100/75">Shared contact instructions</p>
                <p className="mt-2 text-sm leading-7 text-white/80">{r.contactInstructions}</p>
              </div>
            )}

            {r.status === "Pending" && acceptingId === r.id && (
              <div className="mt-5 rounded-[1.25rem] border border-white/10 bg-[#111626] p-5">
                <label className="text-sm font-medium text-white/78">
                  Contact instructions
                  <span className="mt-1 block text-xs font-normal text-white/45">
                    This is what will be revealed to the recruiter if you accept.
                  </span>
                  <input
                    type="text"
                    autoFocus
                    placeholder="e.g. Grab time here: https://cal.com/your-handle"
                    value={contactInstructions}
                    onChange={(e) => setContactInstructions(e.target.value)}
                    className={inputClass}
                  />
                </label>
                <div className="mt-4 flex justify-end gap-3">
                  <button
                    onClick={() => {
                      setAcceptingId(null);
                      setContactInstructions("");
                    }}
                    className="rounded-full border border-white/12 px-4 py-2 text-sm font-medium text-white/70 transition hover:bg-white/10 hover:text-white"
                  >
                    Cancel
                  </button>
                  <button
                    onClick={() => handleAccept(r.id)}
                    disabled={busyId === r.id}
                    className="rounded-full bg-indigo-500 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-indigo-500/25 transition hover:bg-indigo-400 disabled:opacity-50"
                  >
                    Confirm accept
                  </button>
                </div>
              </div>
            )}

            {error && acceptingId === r.id && <p className="mt-3 text-sm text-rose-300">{error}</p>}
          </article>
        ))}
      </section>
    </div>
  );
}
