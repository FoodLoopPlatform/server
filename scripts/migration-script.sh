#!/usr/bin/env bash
set -e

dotnet ef migrations script \
    --project src/FoodLoop.Infrastructure \
    --startup-project src/FoodLoop.API \
    -o migration.sql