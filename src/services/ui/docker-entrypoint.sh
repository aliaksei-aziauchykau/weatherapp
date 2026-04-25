#!/bin/sh
set -e
API="${WEATHER_API_PUBLIC_BASE_URL:-http://localhost}"
printf "window.WEATHER_API_BASE = \"%s\";\n" "$API" > /usr/share/nginx/html/config.js
exec nginx -g "daemon off;"
