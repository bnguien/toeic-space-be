#!/usr/bin/env bash
set -euo pipefail

# Invoked by the opt-in Compose tools service. Source is read-only; build/tool output
# stays in this disposable container, and credentials are supplied through its environment.
case "${1:-Assessment}" in
  Assessment|assessment) area=Assessment ;;
  Identity|identity) area=Identity ;;
  Classroom|classroom) area=Classroom ;;
  Content|content) area=Content ;;
  *) echo 'Usage: docker compose --profile tools run --rm migrations [Assessment|Identity|Classroom|Content]' >&2; exit 2 ;;
esac
if [[ "$#" -gt 1 ]]; then
  echo 'Only a service name is accepted; this command only updates to the latest migration.' >&2
  exit 2
fi

project="/work/src/Services/$area/ToeicSpace.$area.Infrastructure"
startup="/work/src/Services/$area/ToeicSpace.$area.API"
context="${area}DbContext"
connection_key="ConnectionStrings__${area}Database"
if [[ -z "${!connection_key:-}" ]]; then
  echo "Missing $connection_key configuration." >&2
  exit 2
fi

mkdir -p /work/src /tmp/ef-tools
tar -C /source --exclude='*/bin' --exclude='*/obj' -cf - src | tar -C /work -xf -
dotnet tool install --tool-path /tmp/ef-tools dotnet-ef --version 9.0.11 --verbosity quiet
cd "$project"
exec /tmp/ef-tools/dotnet-ef database update \
  --context "$context" \
  --project "$project" \
  --startup-project "$startup" \
  --configuration Release
