import { clerkMiddleware, createRouteMatcher } from "@clerk/nextjs/server";

const isProtectedRoute = createRouteMatcher(["/dashboard(.*)"]);

const apiOrigin = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5000";
const clerkAccountsOrigin = "https://accounts.chuzksy.dev";

export default clerkMiddleware(
  async (auth, req) => {
    if (isProtectedRoute(req)) {
      await auth.protect();
    }
  },
  {
    contentSecurityPolicy: {
      directives: {
        "connect-src": [apiOrigin, clerkAccountsOrigin],
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
