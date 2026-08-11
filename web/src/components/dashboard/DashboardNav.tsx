"use client";

import dynamic from "next/dynamic";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { useApi } from "@/lib/use-api";
import { BetaAccessStatusResponse } from "@/lib/types";

const ClientUserButton = dynamic(
  () => import("@clerk/nextjs").then((module) => module.UserButton),
  { ssr: false }
);

const NAV_ITEMS = [
  { href: "/dashboard", label: "Overview" },
  { href: "/dashboard/profile", label: "Profile" },
  { href: "/dashboard/experience", label: "Experience" },
  { href: "/dashboard/journal", label: "Sprint Journal" },
  { href: "/dashboard/interview-requests", label: "Interview Requests" },
  { href: "/dashboard/import", label: "Import" },
  { href: "/dashboard/analytics", label: "Analytics" },
  { href: "/dashboard/beta-access", label: "Beta Access", requiresBetaAdmin: true },
  { href: "/dashboard/beta-access/analytics", label: "Admin Analytics", requiresBetaAdmin: true },
];

export function DashboardNav() {
  const pathname = usePathname();
  const { request } = useApi();
  const [isBetaAdmin, setIsBetaAdmin] = useState(false);

  useEffect(() => {
    request<BetaAccessStatusResponse>("/api/beta-access/status")
      .then((data) => setIsBetaAdmin(data.isBetaAdmin))
      .catch(() => setIsBetaAdmin(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const navItems = useMemo(
    () => NAV_ITEMS.filter((item) => !item.requiresBetaAdmin || isBetaAdmin),
    [isBetaAdmin]
  );

  return (
    <header className="sticky top-0 z-40 border-b border-white/10 bg-[#0b0d14]/80 backdrop-blur-xl">
      <div className="mx-auto flex max-w-7xl items-center justify-between gap-4 px-4 py-4 sm:px-6">
        <Link href="/dashboard" className="min-w-[10rem] shrink-0">
          <p className="text-sm font-semibold tracking-[0.24em] text-indigo-200 uppercase">Workfolio</p>
          <p className="text-xs text-white/45">Creator workspace</p>
        </Link>
        <div className="flex min-w-0 flex-1 items-center justify-end gap-3">
          <nav className="hidden min-w-0 flex-1 items-center gap-1 overflow-x-auto rounded-full border border-white/10 bg-white/5 p-1 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden xl:flex">
            {navItems.map((item) => {
              const isActive =
                pathname === item.href ||
                (pathname.startsWith(`${item.href}/`) &&
                  !navItems.some(
                    (other) =>
                      other.href.length > item.href.length &&
                      (pathname === other.href || pathname.startsWith(`${other.href}/`))
                  ));
              return (
                <Link
                  key={item.href}
                  href={item.href}
                  className={`whitespace-nowrap rounded-full px-3 py-1.5 text-sm transition-colors ${
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
          <div className="flex h-10 w-10 shrink-0 items-center justify-center overflow-hidden rounded-full border border-white/10 bg-white/5">
            <ClientUserButton appearance={{ elements: { userButtonAvatarBox: "h-8 w-8" } }} />
          </div>
        </div>
      </div>
      <div className="border-t border-white/10 px-4 py-3 xl:hidden sm:px-6">
        <nav className="flex gap-2 overflow-x-auto pb-1">
          {navItems.map((item) => {
            const isActive = pathname === item.href || pathname.startsWith(`${item.href}/`);
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
