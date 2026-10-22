import type { NextConfig } from "next";
import createNextIntlPlugin from "next-intl/plugin";

// Aspire sets API_URL; the browser only talks to Next.js, which proxies /api and /hangfire (same origin, no CORS).
const apiUrl = process.env.API_URL ?? "http://localhost:5080";

const nextConfig: NextConfig = {
  async rewrites() {
    return {
      beforeFiles: [
        { source: "/api/:path*", destination: `${apiUrl}/api/:path*` },
        { source: "/hangfire", destination: `${apiUrl}/hangfire` },
        { source: "/hangfire/:path*", destination: `${apiUrl}/hangfire/:path*` },
      ],
      afterFiles: [],
      fallback: [],
    };
  },
};

export default createNextIntlPlugin("./src/i18n/request.ts")(nextConfig);
