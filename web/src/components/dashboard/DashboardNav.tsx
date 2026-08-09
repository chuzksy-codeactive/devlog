"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { UserButton } from "@clerk/nextjs";

const NAV_ITEMS = [
  { href: "/dashboard", label: "Overview" },
  { href: "/dashboard/profile", label: "Profile" },
  { href: "/dashboard/experience", label: "Experience" },
  { href: "/dashboard/journal", label: "Sprint Journal" },
  { href: "/dashboard/interview-requests", label: "Interview Requests" },
  { href: "/dashboard/import", label: "Import" },
  { href: "/dashboard/analytics", label: "Analytics" },
];

export function DashboardNav() {
  const pathname = usePathname();

  return (
    <header className="sticky top-0 z-40 border-b border-white/10 bg-[#0b0d14]/80 backdrop-blur-xl">
      <div className="mx-auto flex max-w-6xl items-center justify-between gap-6 px-4 py-4 sm:px-6">
        <Link href="/dashboard" className="min-w-0">
          <p className="text-sm font-semibold tracking-[0.24em] text-indigo-200 uppercase">Workfolio</p>
          <p className="text-xs text-white/45">Creator workspace</p>
        </Link>
        <div className="flex items-center gap-4">
          <nav className="hidden items-center gap-1 rounded-full border border-white/10 bg-white/5 p-1 lg:flex">
            {NAV_ITEMS.map((item) => {
              const isActive = pathname === item.href;
              return (
                <Link
                  key={item.href}
                  href={item.href}
                  className={`rounded-full px-3 py-1.5 text-sm transition-colors ${
                    isActive
                      ? "bg-indigo-500/18 text-indigo-100"
                      : "text-white/58 hover:bg-white/8 hover:text-white"
                  }`}
                >
                  {item.label}
                </Link>
              );
            })}
          </nav>
          <div className="flex h-11 w-11 items-center justify-center overflow-hidden rounded-full border border-white/10 bg-white/5">
            <UserButton appearance={{ elements: { userButtonAvatarBox: "h-8 w-8" } }} />
          </div>
        </div>
      </div>
      <div className="border-t border-white/10 px-4 py-3 lg:hidden sm:px-6">
        <nav className="flex gap-2 overflow-x-auto pb-1">
          {NAV_ITEMS.map((item) => {
            const isActive = pathname === item.href;
            return (
              <Link
                key={item.href}
                href={item.href}
                className={`shrink-0 rounded-full px-3 py-1.5 text-sm transition-colors ${
                  isActive
                    ? "bg-indigo-500/20 text-indigo-100"
                    : "border border-white/10 text-white/62 hover:bg-white/8 hover:text-white"
                }`}
              >
                {item.label}
              </Link>
            );
          })}
        </nav>
      </div>
    </header>
  );
}
