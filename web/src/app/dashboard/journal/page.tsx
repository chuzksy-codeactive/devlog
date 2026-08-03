"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useApi } from "@/lib/use-api";
import { MyEntry, MyPortfolio, RawEntryType } from "@/lib/types";

const ENTRY_TYPE_LABEL: Record<RawEntryType, string> = {
  SprintEntry: "Sprint Journal entry",
  Experience: "Experience draft",
  LearningLog: "Learning Log entry",
};

const STATUS_LABEL: Record<MyEntry["status"], string> = {
  Draft: "Draft — not sanitized yet",
  Sanitizing: "Sanitizing…",
  PendingApproval: "Sanitized — ready to review",
  Approved: "Published",
  Rejected: "Rejected",
};

const inputClass =
  "mt-1 w-full rounded-lg border border-zinc-200 bg-white px-3 py-2 text-sm text-zinc-900 outline-none focus:border-indigo-400";

export default function JournalDashboardPage() {
  const { request } = useApi();
  const [entries, setEntries] = useState<MyEntry[]>([]);
  const [slug, setSlug] = useState<string | null>(null);
  const [status, setStatus] = useState<"loading" | "ready" | "error">("loading");
  const [selectedId, setSelectedId] = useState<string | "new" | null>(null);

  function load() {
    Promise.all([
      request<MyEntry[]>("/api/entries"),
      request<MyPortfolio>("/api/portfolio/me").catch(() => null),
    ])
      .then(([entryData, portfolio]) => {
        setEntries(entryData);
        setSlug(portfolio?.slug ?? null);
        setStatus("ready");
      })
      .catch(() => setStatus("error"));
  }

  useEffect(load, []); // eslint-disable-line react-hooks/exhaustive-deps

  const selected = selectedId && selectedId !== "new" ? entries.find((e) => e.id === selectedId) ?? null : null;

  return (
    <div className="grid grid-cols-1 gap-6 lg:grid-cols-[280px_1fr]">
      <div>
        <div className="flex items-center justify-between">
          <h1 className="text-2xl font-semibold tracking-tight text-zinc-900">Sprint Journal</h1>
        </div>
        <button
          onClick={() => setSelectedId("new")}
          className="mt-4 w-full rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white hover:bg-indigo-700"
        >
          Write new entry
        </button>

        {status === "error" && <p className="mt-4 text-sm text-red-600">Couldn&apos;t load entries.</p>}

        <div className="mt-4 flex flex-col gap-1.5">
          {entries.map((entry) => (
            <button
              key={entry.id}
              onClick={() => setSelectedId(entry.id)}
              className={`rounded-lg border px-3 py-2 text-left text-sm transition-colors ${
                selectedId === entry.id
                  ? "border-indigo-200 bg-indigo-50"
                  : "border-transparent bg-white hover:border-zinc-200"
              }`}
            >
              <p className="truncate font-medium text-zinc-900">{entry.title}</p>
              <p className="text-xs text-zinc-400">{STATUS_LABEL[entry.status]}</p>
            </button>
          ))}
          {status === "ready" && entries.length === 0 && (
            <p className="text-sm text-zinc-500">No entries yet.</p>
          )}
        </div>
      </div>

      <div>
        {selectedId === "new" && (
          <NewEntryForm
            onCreated={(id) => {
              load();
              setSelectedId(id);
            }}
          />
        )}
        {selected && (
          <EntryDetail
            entry={selected}
            slug={slug}
            onChanged={() => load()}
          />
        )}
        {!selectedId && (
          <p className="text-sm text-zinc-500">Select an entry, or write a new one.</p>
        )}
      </div>
    </div>
  );
}

function NewEntryForm({ onCreated }: { onCreated: (id: string) => void }) {
  const { request } = useApi();
  const [title, setTitle] = useState("");
  const [entryType, setEntryType] = useState<RawEntryType>("SprintEntry");
  const [rawContent, setRawContent] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!title.trim() || !rawContent.trim()) {
      setError("Title and content are required.");
      return;
    }
    setSaving(true);
    setError(null);
    try {
      const created = await request<{ id: string }>("/api/entries", {
        method: "POST",
        body: JSON.stringify({ title: title.trim(), entryType, rawContent }),
      });
      onCreated(created.id);
    } catch {
      setError("Couldn't save — check that the API is running.");
      setSaving(false);
    }
  }

  return (
    <div className="rounded-xl border border-zinc-200 bg-white p-5 shadow-sm">
      <h2 className="font-medium text-zinc-900">New entry</h2>
      <p className="mt-1 text-xs text-zinc-400">
        Write freely, including real employer/project names — the sanitization pass strips those before
        anything becomes public. Only a Sprint Journal entry shows up on your public /journal page.
      </p>
      <form onSubmit={handleSubmit} className="mt-4 flex flex-col gap-3">
        <label className="text-xs font-medium text-zinc-500">
          Type
          <select value={entryType} onChange={(e) => setEntryType(e.target.value as RawEntryType)} className={inputClass}>
            {Object.entries(ENTRY_TYPE_LABEL).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </label>
        <label className="text-xs font-medium text-zinc-500">
          Title
          <input type="text" value={title} onChange={(e) => setTitle(e.target.value)} className={inputClass} />
        </label>
        <label className="text-xs font-medium text-zinc-500">
          Content
          <textarea
            rows={10}
            value={rawContent}
            onChange={(e) => setRawContent(e.target.value)}
            className={inputClass}
          />
        </label>
        {error && <p className="text-xs text-red-600">{error}</p>}
        <div className="flex justify-end">
          <button
            type="submit"
            disabled={saving}
            className="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
          >
            {saving ? "Saving…" : "Save draft"}
          </button>
        </div>
      </form>
    </div>
  );
}

function EntryDetail({
  entry,
  slug,
  onChanged,
}: {
  entry: MyEntry;
  slug: string | null;
  onChanged: () => void;
}) {
  const { request } = useApi();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSanitize() {
    setBusy(true);
    setError(null);
    try {
      await request(`/api/entries/${entry.id}/sanitize`, { method: "POST" });
      onChanged();
    } catch {
      setError("Sanitization failed — check the API logs (likely a missing/invalid LLM API key).");
    } finally {
      setBusy(false);
    }
  }

  async function handleApprove() {
    setBusy(true);
    setError(null);
    try {
      await request(`/api/entries/${entry.id}/approve`, { method: "POST" });
      onChanged();
    } catch {
      setError("Approval failed — check that the entry is sanitized first.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="rounded-xl border border-zinc-200 bg-white p-5 shadow-sm">
      <div className="flex items-start justify-between">
        <div>
          <p className="text-xs font-medium uppercase tracking-wide text-indigo-600">
            {ENTRY_TYPE_LABEL[entry.entryType]}
          </p>
          <h2 className="mt-0.5 text-lg font-medium text-zinc-900">{entry.title}</h2>
        </div>
        <span className="rounded-full border border-zinc-200 bg-zinc-50 px-2.5 py-1 text-xs text-zinc-600">
          {STATUS_LABEL[entry.status]}
        </span>
      </div>

      <div className="mt-4">
        <p className="text-xs font-medium uppercase tracking-wide text-zinc-400">Raw content</p>
        <p className="mt-1 whitespace-pre-wrap text-sm text-zinc-600">{entry.rawContent}</p>
      </div>

      {entry.sanitizedPreview && (
        <div className="mt-4 rounded-lg bg-emerald-50 p-3">
          <p className="text-xs font-medium uppercase tracking-wide text-emerald-700">
            Sanitized preview {entry.sanitizationModelVersion ? `(${entry.sanitizationModelVersion})` : ""}
          </p>
          <p className="mt-1 whitespace-pre-wrap text-sm text-zinc-700">{entry.sanitizedPreview}</p>
        </div>
      )}

      {error && <p className="mt-3 text-xs text-red-600">{error}</p>}

      <div className="mt-4 flex items-center gap-3">
        {entry.status !== "Approved" && (
          <button
            onClick={handleSanitize}
            disabled={busy}
            className="rounded-lg border border-zinc-200 bg-white px-3 py-1.5 text-sm font-medium text-zinc-900 hover:bg-zinc-50 disabled:opacity-50"
          >
            {entry.sanitizedPreview ? "Re-sanitize" : "Sanitize"}
          </button>
        )}
        {entry.status === "PendingApproval" && (
          <button
            onClick={handleApprove}
            disabled={busy}
            className="rounded-lg bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
          >
            Approve &amp; publish
          </button>
        )}
        {entry.status === "Approved" && slug && entry.entryType === "SprintEntry" && (
          <Link href={`/${slug}/journal`} className="text-sm font-medium text-indigo-600 underline">
            View on your public journal →
          </Link>
        )}
        {entry.status === "Approved" && slug && entry.entryType === "LearningLog" && (
          <Link href={`/${slug}/learning`} className="text-sm font-medium text-indigo-600 underline">
            View on your public Learning Lab →
          </Link>
        )}
      </div>
    </div>
  );
}
