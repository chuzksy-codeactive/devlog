"use client";

import { useState } from "react";
import { AskAiForm } from "./AskAiForm";

export function AskAiWidget({ slug, displayName }: { slug: string; displayName: string }) {
  const [isOpen, setIsOpen] = useState(false);

  return (
    <div className="fixed bottom-6 right-6 z-50">
      {isOpen && (
        <div className="mb-3 w-80 rounded-2xl border border-[#e2ded7] bg-[#faf9f7] p-5 shadow-xl ring-1 ring-black/5">
          <p className="mb-0.5 text-xs font-semibold uppercase tracking-widest text-indigo-500">AI Assistant</p>
          <p className="mb-3 text-sm font-semibold text-zinc-900">Ask about {displayName}&apos;s experience</p>
          <AskAiForm slug={slug} displayName={displayName} />
        </div>
      )}
      <button
        onClick={() => setIsOpen((v) => !v)}
        className="flex h-14 w-14 items-center justify-center rounded-full bg-indigo-600 text-white shadow-lg transition-transform hover:scale-105 hover:bg-indigo-700"
        aria-label="Ask AI"
      >
        {isOpen ? "×" : "AI"}
      </button>
    </div>
  );
}
