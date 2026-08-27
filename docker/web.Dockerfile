FROM node:22-alpine AS build

WORKDIR /app
COPY src/AiAdmin.Web/package.json ./
RUN npm install --global cnpm \
    && cnpm install --ignore-scripts

COPY src/AiAdmin.Web/ ./
ARG VITE_API_URL=/
ENV VITE_API_URL=${VITE_API_URL}
RUN cnpm exec vite build

FROM nginx:1.27-alpine

COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80