# syntax=docker/dockerfile:1.7
FROM node:22.23.2-alpine3.24 AS build
WORKDIR /app

ARG VITE_API_BASE_URL
ARG VITE_API_ORIGIN
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL} \
    VITE_API_ORIGIN=${VITE_API_ORIGIN}

COPY presentation/Frontend/ludora-client/package.json presentation/Frontend/ludora-client/package-lock.json ./
RUN npm ci --include=optional

COPY presentation/Frontend/ludora-client/ ./
RUN test -n "$VITE_API_BASE_URL" \
    && test -n "$VITE_API_ORIGIN" \
    && npm run build

FROM nginx:1.30.4-alpine3.24 AS final
COPY presentation/Frontend/ludora-client/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD wget -q -O - http://127.0.0.1/healthz || exit 1
