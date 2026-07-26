"use client";

import { useEffect, useState } from "react";
import { useApi } from "@/lib/use-api";
import { MyInterviewRequest } from "@/lib/types";

const STATUS_STYLE: Record<MyInterviewRequest["status"], string> = {
  Pending: "border-amber-200 bg-amber-50 text-amber-700",
  Accepted: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Declined: "border-zinc-200 bg-zinc-50 text-zinc-500",
};

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

  return (
    <div className="max-w-2xl">
      <h1 className="text-2xl font-semibold tracking-tight text-zinc-900">Interview Requests</h1>
      <p className="mt-1 text-sm text-zinc-500">
        Only email-verified requests appear here — an unconfirmed recruiter email never reaches this inbox.
      </p>

      {status === "error" && <p className="mt-4 text-sm text-red-600">Couldn&apos;t load requests.</p>}
      {status === "ready" && requests.length === 0 && (
        <p className="mt-4 text-sm text-zinc-500">No interview requests yet.</p>
      )}

      <div className="mt-6 flex flex-col gap-4">
        {requests.map((r) => (
          <div key={r.id} className="rounded-xl border border-zinc-200 bg-white p-5 shadow-sm">
            <div className="flex items-start justify-between">
              <div>
                <p className="font-medium text-zinc-900">
                  {r.recruiterName ? `${r.recruiterName} · ` : ""}
                  {r.recruiterEmail}
                </p>
                <p className="text-xs text-zinc-400">{new Date(r.createdAt).toLocaleString()}</p>
              </div>
              <span className={`rounded-full border px-2.5 py-1 text-xs font-medium ${STATUS_STYLE[r.status]}`}>
                {r.status}
              </span>
            </div>
            <p className="mt-3 text-sm text-zinc-700">{r.note}</p>

            {r.status === "Accepted" && r.contactInstructions && (
              <p className="mt-3 rounded-lg bg-emerald-50 p-2.5 text-xs text-emerald-700">
                Revealed: {r.contactInstructions}
              </p>
            )}

            {r.status === "Pending" && (
              <div className="mt-4">
                {acceptingId === r.id ? (
                  <div className="flex flex-col gap-2">
                    <label className="text-xs font-medium text-zinc-500">
                      Contact instructions (revealed to the recruiter)
                      <input
                        type="text"
                        autoFocus
                        placeholder="e.g. Grab time here: https://cal.com/your-handle"
                        value={contactInstructions}
                        onChange={(e) => setContactInstructions(e.target.value)}
                        className="mt-1 w-full rounded-lg border border-zinc-200 bg-white px-3 py-2 text-sm text-zinc-900 outline-none focus:border-indigo-400"
                      />
                    </label>
                    <div className="flex justify-end gap-2">
                      <button
                        onClick={() => {
                          setAcceptingId(null);
                          setContactInstructions("");
                        }}
                        className="text-xs font-medium text-zinc-500 underline"
                      >
                        Cancel
                      </button>
                      <button
                        onClick={() => handleAccept(r.id)}
                        disabled={busyId === r.id}
                        className="rounded-lg bg-indigo-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
                      >
                        Confirm accept
                      </button>
                    </div>
                  </div>
                ) : (
                  <div className="flex gap-3">
                    <button
                      onClick={() => setAcceptingId(r.id)}
                      className="rounded-lg bg-indigo-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-indigo-700"
                    >
                      Accept
                    </button>
                    <button
                      onClick={() => handleDecline(r.id)}
                      disabled={busyId === r.id}
                      className="rounded-lg border border-zinc-200 px-3 py-1.5 text-xs font-medium text-zinc-700 hover:bg-zinc-50 disabled:opacity-50"
                    >
                      Decline
                    </button>
                  </div>
                )}
              </div>
            )}

            {error && acceptingId === r.id && <p className="mt-2 text-xs text-red-600">{error}</p>}
          </div>
        ))}
      </div>
    </div>
  );
}
