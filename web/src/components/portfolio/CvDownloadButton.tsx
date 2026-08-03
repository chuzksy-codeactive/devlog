"use client";

import { useState } from "react";

const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5000";

export function CvDownloadButton({ slug }: { slug: string }) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleClick() {
    setLoading(true);
    setError(null);

    try {
      const response = await fetch(`${API_BASE_URL}/api/portfolio/${encodeURIComponent(slug)}/cv-access-url`);

      if (!response.ok) {
        setError(response.status === 404 ? "CV is not available right now." : "Could not open CV right now.");
        return;
      }

      const data = (await response.json()) as { cvAccessUrl?: string };
      if (!data.cvAccessUrl) {
        setError("Could not open CV right now.");
        return;
      }

      window.location.assign(data.cvAccessUrl);
    } catch {
      setError("Could not open CV right now.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="flex flex-col">
      <button
        type="button"
        onClick={handleClick}
        disabled={loading}
        className="rounded-lg border border-zinc-200 bg-white px-4 py-1.5 text-sm font-medium text-zinc-900 disabled:opacity-60"
      >
        {loading ? "Opening…" : "Download CV"}
      </button>
      {error ? <span className="mt-1 text-xs text-red-600">{error}</span> : null}
    </div>
  );
}
