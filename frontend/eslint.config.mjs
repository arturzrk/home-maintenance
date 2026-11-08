import { FlatCompat } from "@eslint/eslintrc";
import { dirname } from "path";
import { fileURLToPath } from "url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const compat = new FlatCompat({
  baseDirectory: __dirname,
});

const eslintConfig = [
  ...compat.extends("next/core-web-vitals", "next/typescript"),
  {
    ignores: [
      ".next/**",
      "node_modules/**",
      "out/**",
      "next-env.d.ts",
    ],
  },
  {
    // Clean-code gate, not just a convention: fails `npm run lint` (a
    // CI step) if any function's cyclomatic complexity exceeds the
    // threshold. 20 is a ratchet, not the target -- CLAUDE.md's actual
    // goal is below 5, but several pre-existing functions (NextAuth's
    // jwt callback, apiFetch, ...) are above that today. This stops
    // new code from getting any worse than the current worst case
    // while those get refactored down over time, same threshold as
    // the existing CRAP-score "CRAPPY" cutoff for consistency.
    rules: {
      complexity: ["error", 20],
    },
  },
];

export default eslintConfig;
