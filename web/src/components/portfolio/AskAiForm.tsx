"use client";

import { useState } from "react";

// Shared core used by both the Home hero's embedded ask-box (Product
// Brief: "the AI ask-box embedded directly in the hero, not buried in a
// separate tab") and AskAiWidget's floating panel on every other page.
// Calls the real POST /api/portfolio/{slug}/ask endpoint (embedding
// pipeline + RAG milestone) — no longer a placeholder.
export function AskAiForm({
  slug,
  displayName,
  placeholder = "e.g. Any Azure or SQL experience?",
}: {
  slug: string;
  displayName: string;
  placeholder?: string;
}) {
  const [question, setQuestion] = useState("");
  const [answer, setAnswer] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (question.trim().length === 0 || isLoading) return;

    setIsLoading(true);
    setError(null);
    setAnswer(null);

    try {
      const apiBaseUrl = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5000";
      const response = await fetch(`${apiBaseUrl}/api/portfolio/${slug}/ask`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ question }),
      });

      if (response.status === 429) {
        setError("This candidate has hit the hourly question limit — try again later.");
        return;
      }
      if (!response.ok) {
        setError("Something went wrong answering that question. Please try again.");
        return;
      }

      const data: { answer: string } = await response.json();
      setAnswer(data.answer);
    } catch {
      setError("Couldn't reach the AI assistant — check that the API is running.");
    } finally {
      setIsLoading(false);
    }
  }

  if (answer) {
    return (
      <div className="text-sm">
        <p className="text-zinc-500 dark:text-zinc-400">&ldquo;{question}&rdquo;</p>
        <p className="mt-2">{answer}</p>
        <button
          onClick={() => {
            setAnswer(null);
            setQuestion("");
          }}
          className="mt-2 text-xs font-medium underline"
        >
          Ask another question
        </button>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit} className="flex w-full flex-col gap-2">
      <div className="flex w-full gap-2">
        <input
          value={question}
          onChange={(e) => setQuestion(e.target.value)}
          placeholder={placeholder}
          disabled={isLoading}
          className="flex-1 rounded-lg border border-black/10 px-3 py-2 text-sm outline-none focus:border-black/30 disabled:opacity-50 dark:border-white/10 dark:bg-zinc-800 dark:focus:border-white/30"
        />
        <button
          type="submit"
          disabled={isLoading}
          className="shrink-0 rounded-lg bg-black px-4 py-2 text-sm font-medium text-white disabled:opacity-50 dark:bg-white dark:text-black"
        >
          {isLoading ? "Asking…" : "Ask"}
        </button>
      </div>
      {error && <p className="text-xs text-red-600 dark:text-red-400">{error}</p>}
      <p className="text-xs text-zinc-400 dark:text-zinc-600">
        Answers are generated from {displayName}&apos;s published entries only.
      </p>
    </form>
  );
}
