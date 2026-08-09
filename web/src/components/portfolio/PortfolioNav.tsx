"use client";

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

  return (
    <header className="sticky top-0 z-40 border-b border-[#e8e5e0] bg-[#faf9f7]/90 backdrop-blur">
      <div className="mx-auto flex max-w-4xl items-center justify-between px-6 py-4">
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
        <div className="flex items-center gap-4">
          <nav className="flex items-center gap-1">
            {NAV_ITEMS.map((item) => {
              const href = `${basePath}${item.href}`;
              const isActive = pathname === href;

              if (!item.enabled) {
                return (
                  <span
                    key={item.label}
                    className="cursor-not-allowed rounded-full px-3 py-1.5 text-sm text-zinc-400"
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
                      ? "bg-indigo-50 text-indigo-700"
                      : "text-zinc-500 hover:bg-zinc-100"
                  }`}
                >
                  {item.label}
                </Link>
              );
            })}
          </nav>
          <RequestInterviewButton slug={slug} />
        </div>
      </div>
    </header>
  );
}
