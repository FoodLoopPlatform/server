#!/usr/bin/env bash
set -e

dotnet publish src/FoodLoop.API \
    -c Release \
    -o publish