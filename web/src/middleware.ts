import { clerkMiddleware, createRouteMatcher } from "@clerk/nextjs/server";

// Milestone 11 — the frontend had zero auth wiring before this; every
// creator action so far was tested via curl with a manually-pasted bearer
// token. This is what actually gates /dashboard behind a real signed-in
// session. Clerk's own recommended matcher (below) — everything except
// static assets and Next internals runs through the middleware, and
// isProtectedRoute decides what actually requires a session.
const isProtectedRoute = createRouteMatcher(["/dashboard(.*)"]);

export default clerkMiddleware(async (auth, req) => {
  if (isProtectedRoute(req)) {
    await auth.protect();
  }
});

export const config = {
  matcher: [
    "/((?!_next|[^?]*\\.(?:html?|css|js(?!on)|jpe?g|webp|png|gif|svg|ttf|woff2?|ico|csv|docx?|xlsx?|zip|webmanifest)).*)",
    "/(api|trpc)(.*)",
  ],
};
