#!/usr/bin/env bash
set -e

dotnet ef migrations list \
    --project src/FoodLoop.Infrastructure \
    --startup-project src/FoodLoop.API