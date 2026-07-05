"use client";

import { useState } from "react";
import { AskAiForm } from "./AskAiForm";

// Persistent floating AI assistant shell — Product Brief: "Ask AI —
// persistent floating assistant available on every page." Home renders its
// own inline copy (embedded in the hero) via the same AskAiForm; this
// floating version is what's available on Experience/Sprint Journal/
// Learning Lab once those exist.
export function AskAiWidget({ displayName }: { displayName: string }) {
  const [isOpen, setIsOpen] = useState(false);

  return (
    <div className="fixed bottom-6 right-6 z-50">
      {isOpen && (
        <div className="mb-3 w-80 rounded-2xl border border-black/10 bg-white p-4 shadow-xl dark:border-white/10 dark:bg-zinc-900">
          <p className="mb-3 text-sm font-medium">Ask about {displayName}&apos;s experience</p>
          <AskAiForm displayName={displayName} />
        </div>
      )}
      <button
        onClick={() => setIsOpen((v) => !v)}
        className="flex h-14 w-14 items-center justify-center rounded-full bg-black text-white shadow-lg transition-transform hover:scale-105 dark:bg-white dark:text-black"
        aria-label="Ask AI"
      >
        {isOpen ? "×" : "AI"}
      </button>
    </div>
  );
}
