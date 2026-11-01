#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
mkdir -p reports/zap
# ZAP scans only the API service on the Compose network; no production target argument.
# Active scan can create/update test data. Use only the local test database.
docker run --rm --network connectaoficios_backend \
  -v "$PWD/reports/zap:/zap/wrk:rw" \
  -v "$PWD/tests/zap/auth.py:/zap/auth.py:ro" \
  -e TEST_PASSWORD="${TEST_PASSWORD:-LocalOnly-Connecta-2026!}" \
  ghcr.io/zaproxy/zaproxy:stable zap-api-scan.py \
  -t http://connectaoficios-api:8080/swagger/v1/swagger.json -f openapi \
  --hook /zap/auth.py -r index.html -J results.json -x results.xml
