import Link from "next/link";
import { Show, SignInButton, UserButton } from "@clerk/nextjs";

export default function RootPage() {
  return (
    <div className="flex flex-1 flex-col items-center justify-center gap-4 px-6 text-center">
      <h1 className="text-2xl font-semibold tracking-tight">DevLog</h1>
      <p className="max-w-md text-zinc-600 dark:text-zinc-400">
        A developer career operating system. Each creator has their own portfolio
        at <code className="rounded bg-zinc-100 px-1.5 py-0.5 text-sm dark:bg-zinc-800">/[slug]</code>.
      </p>
      <div className="flex items-center gap-3 text-sm font-medium">
        <Show when="signed-out">
          <SignInButton mode="modal">
            <button className="rounded-lg bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700">
              Sign in
            </button>
          </SignInButton>
        </Show>
        <Show when="signed-in">
          <Link
            href="/dashboard"
            className="rounded-lg bg-indigo-600 px-4 py-2 text-white hover:bg-indigo-700"
          >
            Go to dashboard
          </Link>
          <UserButton />
        </Show>
      </div>
    </div>
  );
}
