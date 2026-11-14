# Builds the viewer for a DigitalOcean App Platform static site, replacing the Node.js buildpack
# (the old one has no pnpm and an outdated Node).
# App Platform: source directory "wandering-inn-stats-viewer", output directory "/app/dist", catch-all document "index.html".
FROM node:24-slim

# Corepack provides the exact pnpm version from package.json's "packageManager".
ENV COREPACK_ENABLE_DOWNLOAD_PROMPT=0
RUN corepack enable

WORKDIR /app

# Dependencies first, so this layer is reused while only the sources change.
COPY package.json pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile

COPY . .
RUN pnpm run build
