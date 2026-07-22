#!/usr/bin/env bash
set -e

if [ $# -eq 0 ]; then
    echo "Usage: migration-add.sh <MigrationName>"
    exit 1
fi

dotnet ef migrations add "$1" \
    --project src/FoodLoop.Infrastructure \
    --startup-project src/FoodLoop.API \
    --output-dir Migrations