import { DashboardNav } from "@/components/dashboard/DashboardNav";
import { auth } from "@clerk/nextjs/server";
import { redirect } from "next/navigation";
import type { ReactNode } from "react";

export default async function DashboardLayout({ children }: { children: ReactNode }) {
  const { userId } = await auth();
  if (!userId) {
    redirect("/");
  }

  return (
    <div className="flex min-h-full flex-1 flex-col bg-zinc-50">
      <DashboardNav />
      <main className="mx-auto w-full max-w-5xl flex-1 px-6 py-10">{children}</main>
    </div>
  );
}
