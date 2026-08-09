"use client";

import { useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { RequestInterviewButton } from "./RequestInterviewButton";

const NAV_ITEMS = [
  { href: "", label: "Home", enabled: true },
  { href: "/experience", label: "Experience", enabled: true },
  { href: "/journal", label: "Sprint Journal", enabled: true },
  { href: "/learning", label: "Learning Lab", enabled: true },
];

export function PortfolioNav({ slug, displayName }: { slug: string; displayName: string }) {
  const pathname = usePathname();
  const basePath = `/${slug}`;
  const [drawerOpen, setDrawerOpen] = useState(false);

  return (
    <>
      <header className="sticky top-0 z-40 border-b border-[#e8e5e0] bg-[#faf9f7]/90 backdrop-blur">
        <div className="mx-auto flex max-w-4xl items-center justify-between px-6 py-4">
          {/* Brand */}
          <div className="flex items-center gap-2.5">
            <Link href={basePath} className="font-semibold tracking-tight text-zinc-900">
              {displayName}
            </Link>
            <span className="hidden h-3.5 w-px bg-zinc-300 sm:block" />
            <span className="hidden items-center gap-1 sm:flex">
              <span className="text-[10px] font-medium text-zinc-400">powered by</span>
              <span className="text-[10px] font-bold tracking-tight text-indigo-500">Workfolio</span>
            </span>
          </div>

          {/* Desktop nav */}
          <div className="hidden items-center gap-4 lg:flex">
            <nav className="flex items-center gap-1">
              {NAV_ITEMS.map((item) => {
                const href = `${basePath}${item.href}`;
                const isActive = pathname === href;
                if (!item.enabled) {
                  return (
                    <span key={item.label} className="cursor-not-allowed rounded-full px-3 py-1.5 text-sm text-zinc-400" title="Coming soon">
                      {item.label}
                    </span>
                  );
                }
                return (
                  <Link
                    key={item.label}
                    href={href}
                    className={`rounded-full px-3 py-1.5 text-sm transition-colors ${
                      isActive ? "bg-indigo-50 text-indigo-700" : "text-zinc-500 hover:bg-zinc-100"
                    }`}
                  >
                    {item.label}
                  </Link>
                );
              })}
            </nav>
            <RequestInterviewButton slug={slug} />
          </div>

          {/* Mobile hamburger */}
          <button
            onClick={() => setDrawerOpen(true)}
            className="flex h-9 w-9 items-center justify-center rounded-lg border border-[#dedad4] bg-white text-zinc-600 shadow-sm transition hover:border-indigo-200 hover:text-indigo-600 lg:hidden"
            aria-label="Open menu"
          >
            <svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round">
              <line x1="2" y1="4" x2="14" y2="4" />
              <line x1="2" y1="8" x2="14" y2="8" />
              <line x1="2" y1="12" x2="14" y2="12" />
            </svg>
          </button>
        </div>
      </header>

      {/* Mobile drawer overlay */}
      {drawerOpen && (
        <div className="fixed inset-0 z-50 lg:hidden">
          {/* Backdrop */}
          <div
            className="absolute inset-0 bg-black/30 backdrop-blur-sm"
            onClick={() => setDrawerOpen(false)}
          />

          {/* Drawer panel */}
          <div className="absolute right-0 top-0 flex h-full w-72 flex-col border-l border-[#e2ded7] bg-[#faf9f7] shadow-2xl">
            {/* Drawer header */}
            <div className="flex items-center justify-between border-b border-[#e8e5e0] px-5 py-4">
              <div>
                <p className="font-semibold text-zinc-900">{displayName}</p>
                <p className="text-[10px] font-medium text-zinc-400">
                  powered by <span className="font-bold text-indigo-500">Workfolio</span>
                </p>
              </div>
              <button
                onClick={() => setDrawerOpen(false)}
                className="flex h-8 w-8 items-center justify-center rounded-lg border border-[#dedad4] bg-white text-zinc-500 hover:text-zinc-900"
                aria-label="Close menu"
              >
                <svg width="12" height="12" viewBox="0 0 12 12" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round">
                  <line x1="1" y1="1" x2="11" y2="11" />
                  <line x1="11" y1="1" x2="1" y2="11" />
                </svg>
              </button>
            </div>

            {/* Nav links */}
            <nav className="flex flex-col gap-1 p-4">
              {NAV_ITEMS.map((item) => {
                const href = `${basePath}${item.href}`;
                const isActive = pathname === href;
                if (!item.enabled) {
                  return (
                    <span key={item.label} className="rounded-xl px-4 py-3 text-sm text-zinc-400 cursor-not-allowed">
                      {item.label}
                    </span>
                  );
                }
                return (
                  <Link
                    key={item.label}
                    href={href}
                    onClick={() => setDrawerOpen(false)}
                    className={`rounded-xl px-4 py-3 text-sm font-medium transition-colors ${
                      isActive
                        ? "bg-indigo-50 text-indigo-700"
                        : "text-zinc-600 hover:bg-white hover:text-zinc-900"
                    }`}
                  >
                    {item.label}
                  </Link>
                );
              })}
            </nav>

            {/* Request Interview CTA */}
            <div className="mt-auto border-t border-[#e8e5e0] p-4">
              <RequestInterviewButton slug={slug} />
            </div>
          </div>
        </div>
      )}
    </>
  );
}
