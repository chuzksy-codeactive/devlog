"use client";

import { useEffect, useMemo, useState } from "react";
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

const statusTone: Record<MyEntry["status"], string> = {
  Draft: "border-amber-300/18 bg-amber-500/10 text-amber-100",
  Sanitizing: "border-cyan-300/18 bg-cyan-500/10 text-cyan-100",
  PendingApproval: "border-indigo-300/18 bg-indigo-500/10 text-indigo-100",
  Approved: "border-emerald-300/18 bg-emerald-500/10 text-emerald-100",
  Rejected: "border-rose-300/18 bg-rose-500/10 text-rose-100",
};

const inputClass =
  "mt-1 w-full rounded-xl border border-white/12 bg-white px-3 py-2 text-sm text-zinc-950 outline-none placeholder:text-zinc-500 focus:border-indigo-300 focus:bg-white";

function summarizeEntries(entries: MyEntry[]) {
  return [
    {
      label: "Entries",
      value: String(entries.length),
      helper: entries.length > 0 ? "Drafts and published journal moments" : "Start with your first entry",
    },
    {
      label: "Published",
      value: String(entries.filter((entry) => entry.status === "Approved").length),
      helper: "Visible on your public portfolio",
    },
    {
      label: "Needs review",
      value: String(entries.filter((entry) => entry.status === "PendingApproval").length),
      helper: "Sanitized and ready to approve",
    },
  ];
}

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
  const summary = useMemo(() => summarizeEntries(entries), [entries]);

  return (
    <div className="flex flex-col gap-8">
      <section className="overflow-hidden rounded-[2rem] border border-white/10 bg-white/6 p-8 shadow-2xl shadow-black/20 backdrop-blur-xl">
        <div className="grid gap-8 lg:grid-cols-[1.05fr_0.95fr]">
          <div>
            <div className="inline-flex items-center gap-2 rounded-full border border-indigo-400/20 bg-indigo-500/10 px-4 py-2 text-sm text-indigo-100">
              <span className="h-2 w-2 rounded-full bg-indigo-300" />
              Sprint Journal
            </div>
            <h1 className="mt-6 text-4xl font-semibold tracking-tight text-white">
              Write freely. Sanitize safely. Publish the strongest version.
            </h1>
            <p className="mt-4 max-w-2xl text-base leading-7 text-white/68">
              Capture the real story behind delivery work, then review the sanitized version before it goes public.
            </p>

            <div className="mt-8 flex flex-wrap gap-3">
              <button
                onClick={() => setSelectedId("new")}
                className="rounded-full bg-indigo-500 px-5 py-3 text-sm font-medium text-white shadow-lg shadow-indigo-500/25 transition hover:bg-indigo-400"
              >
                Write new entry
              </button>
              <span className="rounded-full border border-white/10 bg-white/6 px-4 py-3 text-sm text-white/65">
                Select an entry to compare raw content with its sanitized preview.
              </span>
            </div>
          </div>

          <div className="grid gap-3 rounded-[1.5rem] border border-white/10 bg-[#101525] p-5">
            <p className="text-xs font-medium uppercase tracking-[0.22em] text-white/42">Publishing pipeline</p>
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

      <section className="grid gap-6 xl:grid-cols-[340px_1fr]">
        <aside className="rounded-[1.75rem] border border-white/10 bg-white/6 p-5 backdrop-blur">
          <div className="mb-4 flex items-center justify-between">
            <div>
              <p className="text-xs font-medium uppercase tracking-[0.22em] text-white/42">Entries</p>
              <h2 className="mt-2 text-2xl font-semibold text-white">Journal queue</h2>
            </div>
          </div>

          {status === "error" && (
            <p className="rounded-2xl border border-rose-400/20 bg-rose-500/10 px-4 py-3 text-sm text-rose-200">
              Couldn&apos;t load entries.
            </p>
          )}

          <div className="mt-4 flex flex-col gap-3">
            {entries.map((entry) => (
              <button
                key={entry.id}
                onClick={() => setSelectedId(entry.id)}
                className={`rounded-[1.25rem] border p-4 text-left transition ${
                  selectedId === entry.id
                    ? "border-indigo-300/25 bg-indigo-500/12"
                    : "border-white/10 bg-white/5 hover:border-white/16 hover:bg-white/7"
                }`}
              >
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <p className="text-xs font-medium uppercase tracking-[0.18em] text-white/40">
                      {ENTRY_TYPE_LABEL[entry.entryType]}
                    </p>
                    <p className="mt-2 line-clamp-2 text-sm font-medium leading-6 text-white">{entry.title}</p>
                  </div>
                  <span className={`shrink-0 rounded-full border px-2.5 py-1 text-[11px] font-medium ${statusTone[entry.status]}`}>
                    {entry.status === "Approved" ? "Live" : entry.status === "PendingApproval" ? "Review" : entry.status}
                  </span>
                </div>
                <p className="mt-2 text-xs text-white/45">{STATUS_LABEL[entry.status]}</p>
              </button>
            ))}

            {status === "loading" && <p className="text-sm text-white/60">Loading entries…</p>}

            {status === "ready" && entries.length === 0 && (
              <div className="rounded-[1.25rem] border border-dashed border-white/12 bg-white/4 p-5 text-center">
                <p className="text-sm text-white/65">No entries yet. Start your first journal draft.</p>
              </div>
            )}
          </div>
        </aside>

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
              onDeleted={() => {
                load();
                setSelectedId(null);
              }}
            />
          )}
          {!selectedId && (
            <div className="rounded-[1.75rem] border border-white/10 bg-white/6 p-8 backdrop-blur">
              <p className="text-sm text-white/60">Select an entry on the left, or write a new one.</p>
            </div>
          )}
        </div>
      </section>
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
    <div className="rounded-[1.75rem] border border-white/10 bg-white/6 p-6 backdrop-blur">
      <div className="mb-5">
        <p className="text-xs font-medium uppercase tracking-[0.22em] text-white/42">New draft</p>
        <h2 className="mt-2 text-2xl font-semibold text-white">Capture a new journal entry</h2>
        <p className="mt-2 text-sm leading-7 text-white/62">
          Write with real internal detail here — the sanitization pass is what prepares it for public review.
        </p>
      </div>

      <form onSubmit={handleSubmit} className="flex flex-col gap-4">
        <label className="text-sm font-medium text-white/78">
          Type
          <select value={entryType} onChange={(e) => setEntryType(e.target.value as RawEntryType)} className={inputClass}>
            {Object.entries(ENTRY_TYPE_LABEL).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </label>

        <label className="text-sm font-medium text-white/78">
          Title
          <input type="text" value={title} onChange={(e) => setTitle(e.target.value)} className={inputClass} />
        </label>

        <label className="text-sm font-medium text-white/78">
          Content
          <textarea
            rows={12}
            value={rawContent}
            onChange={(e) => setRawContent(e.target.value)}
            className={inputClass}
            placeholder="Describe what you built, the problem, the constraints, the decisions, and the outcome."
          />
        </label>

        {error && <p className="text-sm text-rose-300">{error}</p>}

        <div className="flex justify-end">
          <button
            type="submit"
            disabled={saving}
            className="rounded-full bg-indigo-500 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-indigo-500/25 transition hover:bg-indigo-400 disabled:opacity-50"
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
  onDeleted,
}: {
  entry: MyEntry;
  slug: string | null;
  onChanged: () => void;
  onDeleted: () => void;
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

  async function handleDelete() {
    const isPublished = entry.status === "Approved";
    const confirmed = window.confirm(
      isPublished
        ? "Delete this published entry? This will also remove its recruiter-search embeddings."
        : "Delete this draft entry?"
    );
    if (!confirmed) {
      return;
    }

    setBusy(true);
    setError(null);
    try {
      await request(`/api/entries/${entry.id}`, { method: "DELETE" });
      onDeleted();
    } catch {
      setError("Delete failed — please try again.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="rounded-[1.75rem] border border-white/10 bg-white/6 p-6 backdrop-blur">
      <div className="flex flex-col gap-5 border-b border-white/10 pb-5 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <div className="flex flex-wrap items-center gap-3">
            <span className="text-xs font-medium uppercase tracking-[0.22em] text-white/42">
              {ENTRY_TYPE_LABEL[entry.entryType]}
            </span>
            <span className={`rounded-full border px-3 py-1 text-xs font-medium ${statusTone[entry.status]}`}>
              {STATUS_LABEL[entry.status]}
            </span>
          </div>
          <h2 className="mt-3 text-2xl font-semibold text-white">{entry.title}</h2>
        </div>

        <div className="flex flex-wrap gap-3">
          {entry.status !== "Approved" && (
            <button
              onClick={handleSanitize}
              disabled={busy}
              className="rounded-full border border-white/12 px-4 py-2 text-sm font-medium text-white/80 transition hover:bg-white/10 hover:text-white disabled:opacity-50"
            >
              {entry.sanitizedPreview ? "Re-sanitize" : "Sanitize"}
            </button>
          )}
          {entry.status === "PendingApproval" && (
            <button
              onClick={handleApprove}
              disabled={busy}
              className="rounded-full bg-indigo-500 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-indigo-500/25 transition hover:bg-indigo-400 disabled:opacity-50"
            >
              Approve &amp; publish
            </button>
          )}
          {entry.status === "Approved" && slug && entry.entryType === "SprintEntry" && (
            <Link href={`/${slug}/journal`} className="rounded-full border border-white/12 px-4 py-2 text-sm font-medium text-indigo-100 transition hover:bg-white/10">
              View public journal
            </Link>
          )}
          {entry.status === "Approved" && slug && entry.entryType === "LearningLog" && (
            <Link href={`/${slug}/learning`} className="rounded-full border border-white/12 px-4 py-2 text-sm font-medium text-indigo-100 transition hover:bg-white/10">
              View Learning Lab
            </Link>
          )}
          <button
            onClick={handleDelete}
            disabled={busy}
            className="rounded-full border border-rose-300/30 px-4 py-2 text-sm font-medium text-rose-200 transition hover:bg-rose-500/20 hover:text-rose-100 disabled:opacity-50"
          >
            Delete entry
          </button>
        </div>
      </div>

      <div className="mt-6 grid gap-5 xl:grid-cols-2">
        <section className="rounded-[1.5rem] border border-white/10 bg-[#111626] p-5">
          <p className="text-xs font-medium uppercase tracking-[0.22em] text-white/42">Raw content</p>
          <p className="mt-4 whitespace-pre-wrap text-sm leading-7 text-white/72">{entry.rawContent}</p>
        </section>

        <section className="rounded-[1.5rem] border border-emerald-300/14 bg-emerald-500/8 p-5">
          <p className="text-xs font-medium uppercase tracking-[0.22em] text-emerald-100/75">
            Sanitized preview {entry.sanitizationModelVersion ? `(${entry.sanitizationModelVersion})` : ""}
          </p>
          {entry.sanitizedPreview ? (
            <p className="mt-4 whitespace-pre-wrap text-sm leading-7 text-white/78">{entry.sanitizedPreview}</p>
          ) : (
            <p className="mt-4 text-sm leading-7 text-white/56">
              No sanitized preview yet. Run the sanitization step to generate the recruiter-safe version of this entry.
            </p>
          )}
        </section>
      </div>

      {error && <p className="mt-4 text-sm text-rose-300">{error}</p>}
    </div>
  );
}
