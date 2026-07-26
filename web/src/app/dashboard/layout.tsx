import { DashboardNav } from "@/components/dashboard/DashboardNav";

// Auth is already enforced by src/middleware.ts (isProtectedRoute matches
// /dashboard(.*) and calls auth.protect()) — this layout doesn't re-check
// it, same "one place owns the guarantee" principle as the backend's RLS
// vs. app-layer checks.
export default function DashboardLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-full flex-1 flex-col bg-zinc-50">
      <DashboardNav />
      <main className="mx-auto w-full max-w-5xl flex-1 px-6 py-10">{children}</main>
    </div>
  );
}
