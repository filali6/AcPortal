# --- Build stage ---
FROM node:20-alpine AS build
WORKDIR /src

COPY package*.json ./
RUN npm ci --legacy-peer-deps --ignore-scripts

COPY . .
RUN npm run build -- --configuration production

# --- Runtime stage ---
FROM nginx:1.27-alpine AS final

COPY --from=build /src/dist/frontend/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Utilise l'utilisateur "nginx" déjà présent dans l'image officielle
RUN chown -R nginx:nginx /usr/share/nginx/html && \
    chown -R nginx:nginx /var/cache/nginx && \
    chown -R nginx:nginx /var/log/nginx && \
    chown -R nginx:nginx /etc/nginx/conf.d && \
    touch /tmp/nginx.pid && \
    chown -R nginx:nginx /tmp/nginx.pid

USER nginx

EXPOSE 80