"use client";

import { useState } from "react";

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
        <p className="text-zinc-500">&ldquo;{question}&rdquo;</p>
        <p className="mt-2 text-zinc-900">{answer}</p>
        <button
          onClick={() => {
            setAnswer(null);
            setQuestion("");
          }}
          className="mt-2 text-xs font-medium text-indigo-600 underline"
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
          className="flex-1 rounded-lg border border-[#dedad4] bg-white px-3 py-2 text-sm text-zinc-900 outline-none transition focus:border-indigo-400 focus:ring-2 focus:ring-indigo-100 disabled:opacity-50"
        />
        <button
          type="submit"
          disabled={isLoading}
          className="shrink-0 rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
        >
          {isLoading ? "Asking…" : "Ask"}
        </button>
      </div>
      {error && <p className="rounded-lg border border-red-100 bg-red-50 px-3 py-2 text-xs text-red-600">{error}</p>}
      <p className="text-xs text-zinc-400">
        Answers are generated from {displayName}&apos;s published entries only.
      </p>
    </form>
  );
}
