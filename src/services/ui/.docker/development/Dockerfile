# syntax=docker/dockerfile:1
FROM nginx:1.25-alpine
COPY www /usr/share/nginx/html
COPY .docker/local/nginx.conf /etc/nginx/conf.d/default.conf
COPY docker-entrypoint.sh /docker-entrypoint.sh
RUN chmod +x /docker-entrypoint.sh
EXPOSE 80
ENV WEATHER_API_PUBLIC_BASE_URL=http://localhost
ENTRYPOINT ["/docker-entrypoint.sh"]
