"use client";

import { useState } from "react";

// Gap-closure feature #1 from the product review: "the actual conversion
// action... the single most load-bearing addition." Backend endpoint
// (interview_requests) is intentionally last in the MVP roadmap's build
// order, so this renders the real persistent CTA placement now (next to
// Ask AI, per the Product Brief) without pretending requests are tracked yet.
export function RequestInterviewButton() {
  const [clicked, setClicked] = useState(false);

  return (
    <div className="relative">
      <button
        onClick={() => setClicked(true)}
        className="rounded-full border border-black px-4 py-1.5 text-sm font-medium transition-colors hover:bg-black hover:text-white dark:border-white dark:hover:bg-white dark:hover:text-black"
      >
        Request Interview
      </button>
      {clicked && (
        <div className="absolute right-0 top-full mt-2 w-64 rounded-xl border border-black/10 bg-white p-3 text-sm text-zinc-500 shadow-lg dark:border-white/10 dark:bg-zinc-900 dark:text-zinc-400">
          Interview requests aren&apos;t wired up yet — this is the real button
          placement, coming online in a later milestone.
          <button
            onClick={() => setClicked(false)}
            className="mt-2 block text-xs font-medium text-black underline dark:text-white"
          >
            Dismiss
          </button>
        </div>
      )}
    </div>
  );
}
