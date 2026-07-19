import { auth } from "@/lib/auth";

// Protect authenticated routes. NextAuth's `auth` export wraps the
// middleware handler so `req.auth` carries the resolved session.
export default auth((req) => {
  if (!req.auth?.idToken) {
    const url = new URL("/signin", req.nextUrl.origin);
    url.searchParams.set("callbackUrl", req.nextUrl.pathname + req.nextUrl.search);
    return Response.redirect(url, 307);
  }
});

// Limit middleware to the routes that actually need auth. "/" is public
// (split render: landing for anonymous, dashboard for a session);
// /signin, /privacy, /terms, /api/auth/* and /user-manual/* never enter
// the matcher.
export const config = {
  matcher: [
    "/properties/:path*",
    "/jobs/:path*",
    "/job-definitions/:path*",
    "/assets/:path*",
  ],
};
