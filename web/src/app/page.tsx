import Link from "next/link";

// This is a placeholder, not the real marketing/root page — Phase 1 is a
// dogfood build, and the actual public "browse all portfolios" experience
// (Discover) is a Phase 2 item per the roadmap. Each creator's portfolio
// lives at /[slug] (see src/app/[slug]/); this just points at the demo
// fixtures used to build those pages.
export default function RootPage() {
  return (
    <div className="flex flex-1 flex-col items-center justify-center gap-4 px-6 text-center">
      <h1 className="text-2xl font-semibold tracking-tight">DevLog</h1>
      <p className="max-w-md text-zinc-600 dark:text-zinc-400">
        A developer career operating system. Each creator has their own portfolio
        at <code className="rounded bg-zinc-100 px-1.5 py-0.5 text-sm dark:bg-zinc-800">/[slug]</code>.
      </p>
      <div className="flex gap-3 text-sm font-medium">
        <Link href="/chika" className="underline underline-offset-2">
          View demo portfolio
        </Link>
        <Link href="/new-grad-demo" className="underline underline-offset-2">
          View no-job-yet demo
        </Link>
      </div>
    </div>
  );
}
