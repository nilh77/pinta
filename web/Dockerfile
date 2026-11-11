# The web app for the Demo environment on Railway (docs/deploy-railway.md). Build context: web/.
FROM node:24-slim AS base
ENV NEXT_TELEMETRY_DISABLED=1
RUN corepack enable
WORKDIR /app

FROM base AS build
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile
COPY . .
# Next.js fixes the /api and /hangfire rewrites at build time, so the API's private address is a build argument.
ARG API_URL
ENV API_URL=$API_URL
RUN test -n "$API_URL" && pnpm build

FROM base
ENV NODE_ENV=production
COPY --from=build /app ./
USER node
EXPOSE 3000
CMD ["sh", "-c", "pnpm start -p ${PORT:-3000}"]
