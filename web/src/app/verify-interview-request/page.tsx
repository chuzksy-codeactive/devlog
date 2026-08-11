"use client";

import { Suspense, useEffect, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";

type VerifyState =
  | "loading"
  | "verified"
  | "alreadyVerified"
  | "expired"
  | "notFound"
  | "missingParams"
  | "error";

export default function VerifyInterviewRequestPage() {
  return (
    <Suspense fallback={<StatusShell state="loading" />}>
      <VerifyInterviewRequestContent />
    </Suspense>
  );
}

function VerifyInterviewRequestContent() {
  const searchParams = useSearchParams();
  const [state, setState] = useState<VerifyState>("loading");

  const slug = searchParams.get("slug");
  const requestId = searchParams.get("requestId");
  const token = searchParams.get("token");
  const hasMissingParams = !slug || !requestId || !token;

  useEffect(() => {
    if (hasMissingParams) {
      return;
    }

    let cancelled = false;

    async function verify() {
      try {
        const apiBaseUrl = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5000";
        const response = await fetch(
          `${apiBaseUrl}/api/portfolio/${slug}/interview-requests/${requestId}/verify`,
          {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ token }),
          },
        );

        if (cancelled) return;

        if (response.status === 400) {
          setState("expired");
          return;
        }

        if (response.status === 404) {
          setState("notFound");
          return;
        }

        if (!response.ok) {
          setState("error");
          return;
        }

        const body = (await response.json()) as { verified: boolean; alreadyVerified?: boolean };
        setState(body.alreadyVerified ? "alreadyVerified" : "verified");
      } catch {
        if (!cancelled) setState("error");
      }
    }

    verify();
    return () => {
      cancelled = true;
    };
  }, [hasMissingParams, slug, requestId, token]);

  return <StatusShell state={hasMissingParams ? "missingParams" : state} slug={slug} />;
}

function StatusShell({ state, slug }: { state: VerifyState; slug?: string | null }) {
  const portfolioHref = slug ? `/${slug}` : "/";

  return (
    <div className="mx-auto flex min-h-[60vh] w-full max-w-md flex-col items-center justify-center px-6 py-16 text-center">
      {state === "loading" && (
        <>
          <p className="text-sm text-zinc-600 dark:text-zinc-300">Confirming your request…</p>
        </>
      )}

      {(state === "verified" || state === "alreadyVerified") && (
        <>
          <h1 className="text-xl font-semibold tracking-tight text-zinc-900 dark:text-zinc-100">Request confirmed</h1>
          <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-300">
            {state === "alreadyVerified"
              ? "This link was already confirmed — your interview request has already been sent."
              : "Your interview request has been sent. The creator will reach out if they accept."}
          </p>
          <Link href={portfolioHref} className="mt-6 text-sm font-medium text-indigo-600 underline dark:text-indigo-400">
            Back to portfolio
          </Link>
        </>
      )}

      {state === "expired" && (
        <>
          <h1 className="text-xl font-semibold tracking-tight text-zinc-900 dark:text-zinc-100">Link expired</h1>
          <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-300">
            This confirmation link is invalid or has expired. Confirmation links are valid for 24 hours — head
            back to the portfolio and submit a new interview request.
          </p>
          <Link href={portfolioHref} className="mt-6 text-sm font-medium text-indigo-600 underline dark:text-indigo-400">
            Back to portfolio
          </Link>
        </>
      )}

      {state === "notFound" && (
        <>
          <h1 className="text-xl font-semibold tracking-tight text-zinc-900 dark:text-zinc-100">Request not found</h1>
          <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-300">
            This interview request no longer exists. Please return to the portfolio and submit a new request.
          </p>
          <Link href={portfolioHref} className="mt-6 text-sm font-medium text-indigo-600 underline dark:text-indigo-400">
            Back to portfolio
          </Link>
        </>
      )}

      {state === "missingParams" && (
        <>
          <h1 className="text-xl font-semibold tracking-tight text-zinc-900 dark:text-zinc-100">Invalid confirmation link</h1>
          <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-300">
            This link is missing some information and can&apos;t be confirmed. Try clicking the link directly
            from the email rather than a copy of it.
          </p>
        </>
      )}

      {state === "error" && (
        <>
          <h1 className="text-xl font-semibold tracking-tight text-zinc-900 dark:text-zinc-100">Something went wrong</h1>
          <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-300">
            We couldn&apos;t reach the API to confirm this request. Try again in a moment.
          </p>
          <Link href={portfolioHref} className="mt-6 text-sm font-medium text-indigo-600 underline dark:text-indigo-400">
            Back to portfolio
          </Link>
        </>
      )}
    </div>
  );
}
