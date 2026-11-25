FROM node:24-bookworm-slim AS build
WORKDIR /app

COPY stockflow-web/package.json stockflow-web/package-lock.json ./
RUN npm ci

COPY stockflow-web/ ./
RUN npm run build && test -f /app/dist/index.html

FROM nginx:stable-alpine AS runtime

COPY stockflow-web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/ /usr/share/nginx/html/

EXPOSE 80
