"use client";

import { useState } from "react";

export function RequestInterviewButton({ slug }: { slug: string }) {
  const [open, setOpen] = useState(false);
  const [recruiterEmail, setRecruiterEmail] = useState("");
  const [recruiterName, setRecruiterName] = useState("");
  const [note, setNote] = useState("");
  const [status, setStatus] = useState<"idle" | "loading" | "sent" | "error">("idle");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (status === "loading") return;

    setStatus("loading");
    setErrorMessage(null);

    try {
      const apiBaseUrl = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5000";
      const response = await fetch(`${apiBaseUrl}/api/portfolio/${slug}/interview-requests`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          recruiterEmail,
          recruiterName: recruiterName.trim() || null,
          note,
        }),
      });

      if (!response.ok) {
        const text = await response.text();
        setErrorMessage(text || "Something went wrong sending that request.");
        setStatus("error");
        return;
      }

      setStatus("sent");
    } catch {
      setErrorMessage("Couldn't reach the API — check that it's running.");
      setStatus("error");
    }
  }

  function reset() {
    setOpen(false);
    setStatus("idle");
    setRecruiterEmail("");
    setRecruiterName("");
    setNote("");
    setErrorMessage(null);
  }

  return (
    <div className="relative">
      <button
        onClick={() => setOpen((v) => !v)}
        className="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-indigo-700"
      >
        Request Interview
      </button>
      {open && (
        <div className="absolute right-0 top-full z-50 mt-2 w-72 rounded-xl border border-zinc-200 bg-white p-4 text-sm shadow-lg">
          {status === "sent" ? (
            <div>
              <p className="text-zinc-900">Almost there — check your inbox.</p>
              <p className="mt-1 text-xs text-zinc-500">
                We sent a confirmation link to {recruiterEmail}. Click it to send your request — we only notify
                the creator once you&apos;ve confirmed it&apos;s really you.
              </p>
              <button onClick={reset} className="mt-3 text-xs font-medium text-indigo-600 underline">
                Close
              </button>
            </div>
          ) : (
            <form onSubmit={handleSubmit} className="flex flex-col gap-2">
              <label className="text-xs font-medium text-zinc-500">
                Your email
                <input
                  type="email"
                  required
                  value={recruiterEmail}
                  onChange={(e) => setRecruiterEmail(e.target.value)}
                  disabled={status === "loading"}
                  className="mt-1 w-full rounded-lg border border-zinc-200 bg-zinc-50 px-2 py-1.5 text-sm text-zinc-900 outline-none focus:border-indigo-400 focus:bg-white disabled:opacity-50"
                />
              </label>
              <label className="text-xs font-medium text-zinc-500">
                Your name (optional)
                <input
                  type="text"
                  value={recruiterName}
                  onChange={(e) => setRecruiterName(e.target.value)}
                  disabled={status === "loading"}
                  className="mt-1 w-full rounded-lg border border-zinc-200 bg-zinc-50 px-2 py-1.5 text-sm text-zinc-900 outline-none focus:border-indigo-400 focus:bg-white disabled:opacity-50"
                />
              </label>
              <label className="text-xs font-medium text-zinc-500">
                Note
                <textarea
                  required
                  rows={3}
                  value={note}
                  onChange={(e) => setNote(e.target.value)}
                  disabled={status === "loading"}
                  placeholder="What made you want to reach out?"
                  className="mt-1 w-full resize-none rounded-lg border border-zinc-200 bg-zinc-50 px-2 py-1.5 text-sm text-zinc-900 outline-none focus:border-indigo-400 focus:bg-white disabled:opacity-50"
                />
              </label>
              {status === "error" && errorMessage && <p className="text-xs text-red-600">{errorMessage}</p>}
              <div className="mt-1 flex justify-end gap-2">
                <button type="button" onClick={reset} className="text-xs font-medium text-zinc-500 underline">
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={status === "loading"}
                  className="rounded-lg bg-indigo-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
                >
                  {status === "loading" ? "Sending…" : "Send request"}
                </button>
              </div>
            </form>
          )}
        </div>
      )}
    </div>
  );
}
