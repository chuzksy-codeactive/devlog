"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { UserButton } from "@clerk/nextjs";

// Milestone 11 — the creator-facing nav. Mirrors PortfolioNav's shape
// (sticky header, pill-style active link) so the two halves of the product
// feel like the same app, even though this one is auth-gated and the other
// is public.
const NAV_ITEMS = [
  { href: "/dashboard", label: "Overview" },
  { href: "/dashboard/profile", label: "Profile" },
  { href: "/dashboard/experience", label: "Experience" },
  { href: "/dashboard/journal", label: "Sprint Journal" },
  { href: "/dashboard/interview-requests", label: "Interview Requests" },
];

export function DashboardNav() {
  const pathname = usePathname();

  return (
    <header className="sticky top-0 z-40 border-b border-zinc-200 bg-white/80 backdrop-blur">
      <div className="mx-auto flex max-w-5xl items-center justify-between px-6 py-4">
        <Link href="/dashboard" className="font-semibold tracking-tight text-zinc-900">
          DevLog
        </Link>
        <div className="flex items-center gap-4">
          <nav className="flex items-center gap-1">
            {NAV_ITEMS.map((item) => {
              const isActive = pathname === item.href;
              return (
                <Link
                  key={item.href}
                  href={item.href}
                  className={`rounded-full px-3 py-1.5 text-sm transition-colors ${
                    isActive ? "bg-indigo-50 text-indigo-700" : "text-zinc-500 hover:bg-zinc-100"
                  }`}
                >
                  {item.label}
                </Link>
              );
            })}
          </nav>
          <UserButton />
        </div>
      </div>
    </header>
  );
}
