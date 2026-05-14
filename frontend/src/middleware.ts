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

// Limit middleware to the routes that actually need auth. Public routes
// (/, /signin, /api/auth/*) skip the check entirely.
export const config = {
  matcher: ["/properties/:path*", "/jobs/:path*"],
};
