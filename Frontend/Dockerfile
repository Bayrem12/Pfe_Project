# ── Stage 1: Build ────────────────────────────────────────────────────────
# Use Node 20 to compile the Angular application.
FROM node:20-alpine AS build

WORKDIR /app

# Copy package files first so Docker caches the npm install layer separately.
# The layer is only re-run when package.json / package-lock.json change.
COPY package.json package-lock.json ./
RUN npm ci --legacy-peer-deps

# Copy the rest of the source and build for production.
COPY . .
RUN npm run build -- --configuration production

# ── Stage 2: Serve ────────────────────────────────────────────────────────
# Use a tiny nginx image to serve the compiled static files (~20 MB).
FROM nginx:1.27-alpine

# Remove the default nginx page.
RUN rm -rf /usr/share/nginx/html/*

# Copy the Angular build output from stage 1.
# Angular 17+ outputs to dist/<projectName>/browser
COPY --from=build /app/dist/frontend/browser /usr/share/nginx/html

# Copy our custom nginx config.
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
