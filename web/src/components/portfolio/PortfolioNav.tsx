"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { RequestInterviewButton } from "./RequestInterviewButton";

// The 5-surface IA from the Product Brief: Home, Experience, Sprint
// Journal, Learning Lab, plus the persistent Ask AI + Request Interview
// actions (those two render separately — see AskAiWidget/RequestInterviewButton —
// since they're not "pages," they're persistent across all of them).
// Sprint Journal and Learning Lab routes don't exist yet (later milestones
// per the roadmap); linking to them now with a "soon" badge keeps the nav
// honest about what's actually built.
const NAV_ITEMS = [
  { href: "", label: "Home", enabled: true },
  { href: "/experience", label: "Experience", enabled: true },
  { href: "/journal", label: "Sprint Journal", enabled: false },
  { href: "/learning", label: "Learning Lab", enabled: false },
];

export function PortfolioNav({ slug, displayName }: { slug: string; displayName: string }) {
  const pathname = usePathname();
  const basePath = `/${slug}`;

  return (
    <header className="sticky top-0 z-40 border-b border-black/10 bg-white/80 backdrop-blur dark:border-white/10 dark:bg-black/80">
      <div className="mx-auto flex max-w-4xl items-center justify-between px-6 py-4">
        <Link href={basePath} className="font-semibold tracking-tight">
          {displayName}
        </Link>
        <div className="flex items-center gap-3">
          <nav className="flex items-center gap-1">
            {NAV_ITEMS.map((item) => {
              const href = `${basePath}${item.href}`;
              const isActive = pathname === href;

              if (!item.enabled) {
                return (
                  <span
                    key={item.label}
                    className="cursor-not-allowed rounded-full px-3 py-1.5 text-sm text-zinc-400 dark:text-zinc-600"
                    title="Coming soon"
                  >
                    {item.label}
                  </span>
                );
              }

              return (
                <Link
                  key={item.label}
                  href={href}
                  className={`rounded-full px-3 py-1.5 text-sm transition-colors ${
                    isActive
                      ? "bg-black text-white dark:bg-white dark:text-black"
                      : "text-zinc-600 hover:bg-black/5 dark:text-zinc-400 dark:hover:bg-white/10"
                  }`}
                >
                  {item.label}
                </Link>
              );
            })}
          </nav>
          <RequestInterviewButton />
        </div>
      </div>
    </header>
  );
}
