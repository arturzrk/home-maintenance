import Link from "next/link";
import { signIn } from "@/lib/auth";

const enableDevStub = process.env.NEXTAUTH_DEV_STUB === "true";

export const dynamic = "force-dynamic";

export default function SignInPage({
  searchParams,
}: {
  searchParams: Promise<{ callbackUrl?: string }>;
}) {
  return (
    <main className="mx-auto flex min-h-[60vh] max-w-md flex-col justify-center space-y-6 px-4">
      <header className="space-y-1 text-center">
        <h1 className="text-2xl font-semibold">Sign in</h1>
        <p className="text-sm text-gray-500">
          Pick a sign-in method to continue to the app.
        </p>
      </header>

      <form
        action={async () => {
          "use server";
          const params = await searchParams;
          await signIn("google", {
            redirectTo: params?.callbackUrl ?? "/",
          });
        }}
        className="space-y-2"
      >
        <button
          type="submit"
          className="w-full rounded-md border border-gray-300 bg-white px-4 py-2 text-sm font-medium shadow-sm hover:bg-gray-50"
        >
          Sign in with Google
        </button>
      </form>

      {enableDevStub && (
        <form
          action={async (formData: FormData) => {
            "use server";
            const params = await searchParams;
            const sub = String(formData.get("sub") ?? "").trim() || "alice";
            await signIn("dev-stub", {
              sub,
              redirectTo: params?.callbackUrl ?? "/",
            });
          }}
          className="space-y-2 rounded-md border border-dashed border-amber-300 bg-amber-50 p-4"
        >
          <p className="text-xs font-semibold text-amber-700">
            Development stub - no real authentication
          </p>
          <label className="block text-xs text-amber-900">
            OwnerId (any string)
            <input
              type="text"
              name="sub"
              defaultValue="alice"
              className="mt-1 block w-full rounded border border-amber-300 px-2 py-1 text-sm"
            />
          </label>
          <button
            type="submit"
            className="w-full rounded-md bg-amber-600 px-4 py-2 text-sm font-medium text-white hover:bg-amber-700"
          >
            Sign in as dev user
          </button>
        </form>
      )}

      <footer className="flex justify-center gap-4 text-xs text-gray-500">
        <Link href="/privacy" className="hover:text-gray-900 hover:underline">
          Privacy policy
        </Link>
        <Link href="/terms" className="hover:text-gray-900 hover:underline">
          Terms of service
        </Link>
      </footer>
    </main>
  );
}
