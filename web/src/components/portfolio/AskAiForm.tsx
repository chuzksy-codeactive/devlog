"use client";

import { useState } from "react";

// Shared core used by both the Home hero's embedded ask-box (Product
// Brief: "the AI ask-box embedded directly in the hero, not buried in a
// separate tab") and AskAiWidget's floating panel on every other page —
// same not-wired-yet behavior, two different placements.
export function AskAiForm({
  displayName,
  placeholder = "e.g. Any Azure or SQL experience?",
}: {
  displayName: string;
  placeholder?: string;
}) {
  const [question, setQuestion] = useState("");
  const [submitted, setSubmitted] = useState(false);

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (question.trim().length === 0) return;
    setSubmitted(true);
  }

  if (submitted) {
    return (
      <p className="text-sm text-zinc-500 dark:text-zinc-400">
        The AI assistant isn&apos;t wired up yet — it needs the sanitized entry
        embedding pipeline (a later build milestone) before it can answer
        real questions about {displayName}&apos;s experience. Your question:
        &ldquo;{question}&rdquo;
      </p>
    );
  }

  return (
    <form onSubmit={handleSubmit} className="flex w-full gap-2">
      <input
        value={question}
        onChange={(e) => setQuestion(e.target.value)}
        placeholder={placeholder}
        className="flex-1 rounded-lg border border-black/10 px-3 py-2 text-sm outline-none focus:border-black/30 dark:border-white/10 dark:bg-zinc-800 dark:focus:border-white/30"
      />
      <button
        type="submit"
        className="shrink-0 rounded-lg bg-black px-4 py-2 text-sm font-medium text-white dark:bg-white dark:text-black"
      >
        Ask
      </button>
    </form>
  );
}
