---
work_package_id: WP04
lane: planned
dependencies: ["WP03"]
subtasks: [T021, T022, T023, T024, T025]
test_status: required
test_file: tests/e2e/WP04-wp04-property-frontend.e2e.js
domain: frontend-craft
---

# WP04 - Property frontend

## Objective

Make the Property API user-visible: NextAuth-driven Google sign-in, a
typed API client, a protected `/properties` page with list + create
form. After this WP a signed-in user can create and view their
Properties in the browser.

## Inputs

- Spec: US1, US2 acceptance scenarios.
- Plan: `plan.md` frontend section.
- Research: `research.md` R1 (NextAuth v5).
- Contracts: `contracts/properties.md`.
- Constitution: TS strict, Server Components default, typed API client.

## Subtasks

### T021 - NextAuth v5 wiring

Install:
```bash
cd frontend
npm install next-auth@^5.0.0-beta.20 @auth/core
```

Create `frontend/src/lib/auth.ts`:

```ts
import NextAuth from "next-auth";
import Google from "next-auth/providers/google";

export const { handlers, auth, signIn, signOut } = NextAuth({
  providers: [
    Google({
      clientId: process.env.GOOGLE_CLIENT_ID,
      clientSecret: process.env.GOOGLE_CLIENT_SECRET,
    }),
  ],
  callbacks: {
    async jwt({ token, account }) {
      if (account?.id_token) {
        token.idToken = account.id_token;
      }
      return token;
    },
    async session({ session, token }) {
      session.idToken = token.idToken as string | undefined;
      return session;
    },
  },
  pages: {
    signIn: "/signin",
  },
});
```

Create `frontend/src/app/api/auth/[...nextauth]/route.ts`:

```ts
import { handlers } from "@/lib/auth";
export const { GET, POST } = handlers;
```

Update `frontend/src/types/next-auth.d.ts` (new) to extend the Session
type with `idToken?: string`.

Add `.env.example` at repo root (or `frontend/.env.example`):
```
NEXTAUTH_SECRET=
NEXTAUTH_URL=http://localhost:3000
GOOGLE_CLIENT_ID=
GOOGLE_CLIENT_SECRET=
API_BASE_URL=http://localhost:5000
```

`.env.local` is the developer's file; `.env.example` is the template
committed to the repo.

### T022 - Typed API client

Create `frontend/src/lib/api-client.ts`:

```ts
const apiBase = process.env.API_BASE_URL ?? "http://localhost:5000";

type FetchOptions = {
  idToken: string;          // required for protected routes
  method?: "GET" | "POST" | "PATCH" | "DELETE" | "PUT";
  body?: unknown;
  signal?: AbortSignal;
};

async function call<T>(path: string, opts: FetchOptions): Promise<T> {
  const res = await fetch(`${apiBase}${path}`, {
    method: opts.method ?? "GET",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${opts.idToken}`,
    },
    body: opts.body ? JSON.stringify(opts.body) : undefined,
    cache: "no-store",
    signal: opts.signal,
  });

  if (res.status === 401) throw new ApiError("unauthorized", 401, "Unauthorized");
  if (res.status === 404) throw new ApiError("not_found", 404, "Not found");
  if (!res.ok) {
    const problem = await res.json().catch(() => ({}));
    throw new ApiError(
      problem.code ?? "error",
      res.status,
      problem.detail ?? res.statusText);
  }
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

export class ApiError extends Error {
  constructor(
    public readonly code: string,
    public readonly status: number,
    message: string) { super(message); }
}

export type Property = { id: string; name: string };

export const properties = {
  list: (idToken: string) =>
    call<{ properties: Property[] }>("/api/properties", { idToken }),
  get: (id: string, idToken: string) =>
    call<Property>(`/api/properties/${id}`, { idToken }),
  create: (name: string, idToken: string) =>
    call<Property>("/api/properties", { idToken, method: "POST", body: { name } }),
  rename: (id: string, name: string, idToken: string) =>
    call<Property>(`/api/properties/${id}`, { idToken, method: "PATCH", body: { name } }),
};
```

Add a Server Component helper `frontend/src/lib/session.ts`:

```ts
import { auth } from "@/lib/auth";

export async function requireSession() {
  const session = await auth();
  if (!session?.idToken) {
    throw new Error("Not authenticated");  // middleware should have redirected
  }
  return session;
}
```

### T023 - Sign-in page + middleware

Create `frontend/src/app/(auth)/signin/page.tsx`:

```tsx
import { signIn } from "@/lib/auth";

export default function SignInPage() {
  return (
    <main className="flex min-h-screen items-center justify-center">
      <form action={async () => { "use server"; await signIn("google", { redirectTo: "/properties" }); }}>
        <button type="submit" className="...">Sign in with Google</button>
      </form>
    </main>
  );
}
```

Create `frontend/src/middleware.ts`:

```ts
import { auth } from "@/lib/auth";
import { NextResponse } from "next/server";

export default auth((req) => {
  const isAuth = !!req.auth?.idToken;
  const isPublic = req.nextUrl.pathname.startsWith("/signin")
                || req.nextUrl.pathname.startsWith("/api/auth")
                || req.nextUrl.pathname === "/";
  if (!isAuth && !isPublic) {
    const url = new URL("/signin", req.url);
    url.searchParams.set("callbackUrl", req.url);
    return NextResponse.redirect(url);
  }
});

export const config = {
  matcher: ["/properties/:path*", "/jobs/:path*"],
};
```

### T024 - /properties page + create form

Create `frontend/src/app/properties/page.tsx`:

```tsx
import { requireSession } from "@/lib/session";
import { properties } from "@/lib/api-client";
import CreatePropertyForm from "@/components/CreatePropertyForm";
import PropertyCard from "@/components/PropertyCard";

export default async function PropertiesPage() {
  const session = await requireSession();
  const { properties: list } = await properties.list(session.idToken!);

  return (
    <main className="mx-auto max-w-3xl p-6 space-y-6">
      <header className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">My properties</h1>
      </header>
      <CreatePropertyForm />
      {list.length === 0
        ? <p className="text-gray-500">No properties yet. Add one to get started.</p>
        : <ul className="space-y-2">{list.map(p => <li key={p.id}><PropertyCard property={p} /></li>)}</ul>}
    </main>
  );
}
```

Create `frontend/src/components/PropertyCard.tsx` (Server Component
since it is read-only):
```tsx
import Link from "next/link";
import type { Property } from "@/lib/api-client";

export default function PropertyCard({ property }: { property: Property }) {
  return (
    <Link href={`/properties/${property.id}`} className="block rounded border p-4 hover:bg-gray-50">
      {property.name}
    </Link>
  );
}
```

Create `frontend/src/components/CreatePropertyForm.tsx`:

```tsx
"use client";
import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";

export default function CreatePropertyForm() {
  const router = useRouter();
  const [name, setName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [pending, startTransition] = useTransition();

  async function submit() {
    setError(null);
    const res = await fetch("/api/local/properties", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ name }),
    });
    if (res.ok) {
      setName("");
      startTransition(() => router.refresh());
    } else {
      const problem = await res.json().catch(() => ({}));
      setError(problem.detail ?? "Could not create");
    }
  }

  return (
    <form action={submit} className="flex gap-2">
      <input value={name} onChange={e => setName(e.target.value)}
             required maxLength={100}
             className="flex-1 rounded border px-3 py-2" />
      <button type="submit" disabled={pending || name.trim() === ""}>
        Create Property
      </button>
      {error && <p className="text-red-600 text-sm">{error}</p>}
    </form>
  );
}
```

Create the BFF route at
`frontend/src/app/api/local/properties/route.ts` that forwards to the
backend with the server-side session's idToken:

```ts
import { NextResponse } from "next/server";
import { auth } from "@/lib/auth";
import { properties } from "@/lib/api-client";

export async function POST(req: Request) {
  const session = await auth();
  if (!session?.idToken) return NextResponse.json({ code: "unauthorized" }, { status: 401 });
  const body = await req.json();
  try {
    const created = await properties.create(body.name, session.idToken);
    return NextResponse.json(created, { status: 201 });
  } catch (err: any) {
    return NextResponse.json({ code: err.code, detail: err.message }, { status: err.status ?? 500 });
  }
}
```

Why a BFF route: Client Components must not see the idToken (it would
leak into the browser bundle). The BFF route runs server-side, attaches
the token, returns the API response.

### T025 [P] - Jest tests

In `frontend/src/__tests__/properties/`:

- `CreatePropertyForm.test.tsx`: render, fill the input, submit, mock
  `fetch` to return 201, assert `router.refresh` was called.
- `CreatePropertyForm.validation.test.tsx`: submit empty/long name -
  HTML validation rejects, no fetch fired.
- `CreatePropertyForm.error.test.tsx`: mock fetch returning a 400
  problem-details body; assert the error message renders.

Use the existing Jest config and Testing Library. Mock fetch via
`global.fetch = jest.fn(...)` per test.

## Test strategy

- Component-level Jest tests for the forms and the data-loading
  flow (`router.refresh` triggered after a successful submit).
- No E2E in this WP; the integration tests in WP03 cover the API.
- Manual smoke via the quickstart walkthrough (Step 4).

## Definition of Done

- [ ] Signing in with Google lands on `/properties` populated from
      the API.
- [ ] Creating a Property persists and appears in the list.
- [ ] Unauthenticated visit to `/properties` redirects to `/signin`.
- [ ] Validation error from the API surfaces as inline UI.
- [ ] `npm run lint`, `npm run build`, `npm test` all green.
- [ ] CI green.

## Risks and non-obvious bits

- The session-token-via-BFF pattern (vs. exposing the token to the
  browser) is the right shape for App Router. The Client Component
  ALWAYS talks to a local API route; the local route is the only place
  that sees the idToken.
- NextAuth v5 is in beta as of 2026-05; pin the patch version and
  surface a follow-up to upgrade once GA lands.
- The `/signin` page intentionally lives under the `(auth)` route
  group so it does not inherit the authenticated layout (when one
  is added later).
- Tailwind v4 classes are used inline; if the project adds a design
  system later this becomes a refactor target, not a redesign.

## Next command

```
polaris implement WP04 --base WP03
```
