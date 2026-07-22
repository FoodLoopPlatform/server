#!/usr/bin/env bash
set -e

dotnet ef migrations remove \
    --project src/FoodLoop.Infrastructure \
    --startup-project src/FoodLoop.API