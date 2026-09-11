#!/usr/bin/env bash
set -euo pipefail

dotnet run --configuration Release --no-build -- --memory-test
