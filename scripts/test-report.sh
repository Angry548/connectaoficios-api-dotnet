#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
# PowerShell 7 also runs on Linux; reuse the existing TRX HTML generator.
pwsh -NoProfile -File scripts/test-report.ps1 "$@"
