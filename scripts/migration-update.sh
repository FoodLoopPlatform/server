#!/usr/bin/env bash
set -e

dotnet ef database update \
    --project src/FoodLoop.Infrastructure \
    --startup-project src/FoodLoop.API