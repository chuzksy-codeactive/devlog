import type { NextConfig } from "next";

// The stored-XSS defense-in-depth CSP header (architecture doc Section 9)
// used to be hand-maintained here. Milestone 11 moved it into
// src/middleware.ts's clerkMiddleware({ contentSecurityPolicy }) call —
// this file's version was written before Clerk existed in the app and had
// no allowance for Clerk's script/connect/frame origins, which broke Clerk
// outright the moment it was added ("Failed to load Clerk JS", missing
// 'unsafe-eval' in dev). Letting Clerk's own middleware own the CSP header
// keeps its required origins correct without this codebase needing to
// track them by hand; see middleware.ts's comment for the full reasoning
// and the merged directives.
const nextConfig: NextConfig = {};

export default nextConfig;
