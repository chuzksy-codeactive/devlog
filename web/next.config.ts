import type { NextConfig } from "next";

// Architecture doc Section 9's second half of the stored-XSS fix — the
// first half is SanitizedMarkdown.tsx's restricted renderer with no raw
// HTML passthrough; this is defense in depth on top of it, at the browser
// level, in case something the renderer didn't anticipate ever gets
// through.
//
// script-src/style-src intentionally allow 'unsafe-inline' — Next.js's App
// Router and Tailwind both rely on inline scripts/styles for hydration and
// critical CSS respectively, and a strict nonce-based policy is a bigger
// change than this milestone's scope. This still blocks the actual attack
// this fix targets: an injected <script src="https://evil.example/x.js">
// or an inline handler pulling in a remote payload, since object-src and
// script-src both stay same-origin. Verify in the browser once this is
// running for real (`npm run dev`, check the console for CSP violations) —
// this is config, not something the sandbox can render and check itself.
const apiOrigin = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5000";

const contentSecurityPolicy = [
  "default-src 'self'",
  "script-src 'self' 'unsafe-inline'",
  "style-src 'self' 'unsafe-inline'",
  "img-src 'self' data: https:",
  "font-src 'self' data:",
  `connect-src 'self' ${apiOrigin}`,
  "object-src 'none'",
  "frame-ancestors 'none'",
  "base-uri 'self'",
  "form-action 'self'",
].join("; ");

const nextConfig: NextConfig = {
  async headers() {
    return [
      {
        source: "/:path*",
        headers: [
          {
            key: "Content-Security-Policy",
            value: contentSecurityPolicy,
          },
        ],
      },
    ];
  },
};

export default nextConfig;
