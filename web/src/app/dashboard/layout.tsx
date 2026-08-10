import { DashboardNav } from "@/components/dashboard/DashboardNav";
import { DashboardAccessGate } from "@/components/dashboard/DashboardAccessGate";
import { auth } from "@clerk/nextjs/server";
import { redirect } from "next/navigation";
import type { ReactNode } from "react";

export default async function DashboardLayout({ children }: { children: ReactNode }) {
  const { userId } = await auth();
  if (!userId) {
    redirect("/");
  }

  return (
    <div className="flex min-h-full flex-1 flex-col bg-[radial-gradient(circle_at_top,_rgba(99,102,241,0.16),_transparent_28%),linear-gradient(180deg,#08090f_0%,#0f1220_42%,#0b0d14_100%)] text-white">
      <DashboardNav />
      <main className="mx-auto w-full max-w-6xl flex-1 px-6 py-10">
        <DashboardAccessGate>{children}</DashboardAccessGate>
      </main>
    </div>
  );
}
