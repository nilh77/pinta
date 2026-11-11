import { defineConfig, globalIgnores } from "eslint/config";
import nextVitals from "eslint-config-next/core-web-vitals";
import nextTs from "eslint-config-next/typescript";

export default defineConfig([
  ...nextVitals,
  ...nextTs,
  {
    rules: {
      "no-restricted-imports": ["error", { paths: [{ name: "sonner", importNames: ["toast"], message: "Import toast from @/lib/toast (durations from docs/screens.md)." }] }],
    },
  },
  { files: ["src/lib/toast.ts", "src/lib/toast.test.ts"], rules: { "no-restricted-imports": "off" } },
  globalIgnores([".next/**", "out/**", "next-env.d.ts", "src/lib/api/schema.d.ts"]),
]);
