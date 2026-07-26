import { clerkMiddleware, createRouteMatcher } from "@clerk/nextjs/server";

// Milestone 11 — the frontend had zero auth wiring before this; every
// creator action so far was tested via curl with a manually-pasted bearer
// token. This is what actually gates /dashboard behind a real signed-in
// session. Clerk's own recommended matcher (below) — everything except
// static assets and Next internals runs through the middleware, and
// isProtectedRoute decides what actually requires a session.
const isProtectedRoute = createRouteMatcher(["/dashboard(.*)"]);

// contentSecurityPolicy here (post-Milestone-11 fix) replaces the
// hand-maintained CSP header that used to live in next.config.ts. That one
// was written in Milestone 9, before Clerk existed in this app at all, and
// broke Clerk outright once it was added: no 'unsafe-eval' for Next.js dev
// mode, and no allowance for Clerk's own script/connect/frame origins
// (its FAPI host, Cloudflare's bot-protection host, its abuse/fraud
// protection hosts). Rather than hand-maintain that list — which is exactly
// how it went stale the first time — this uses Clerk's own automatic CSP
// injection (@clerk/nextjs >=6.14, available since this project is on
// 7.6.1), which keeps those Clerk-specific origins correct without this
// codebase needing to track them. `directives` merges additions on top of
// Clerk's own default set rather than replacing it:
//   - connect-src: the .NET API origin (Ask AI, dashboard calls, etc.)
//   - img-src: https:/data: so a creator's arbitrary PhotoUrl/CV image
//     links still render, not just Clerk's own avatar CDN
const apiOrigin = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5000";

export default clerkMiddleware(
  async (auth, req) => {
    if (isProtectedRoute(req)) {
      await auth.protect();
    }
  },
  {
    contentSecurityPolicy: {
      directives: {
        "connect-src": [apiOrigin],
        "img-src": ["https:", "data:"],
      },
    },
  }
);

export const config = {
  matcher: [
    "/((?!_next|[^?]*\\.(?:html?|css|js(?!on)|jpe?g|webp|png|gif|svg|ttf|woff2?|ico|csv|docx?|xlsx?|zip|webmanifest)).*)",
    "/(api|trpc)(.*)",
  ],
};
