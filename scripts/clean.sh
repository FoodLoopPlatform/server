#!/usr/bin/env bash
set -e

dotnet clean FoodLoop.sln

find . -type d -name bin -exec rm -rf {} +
find . -type d -name obj -exec rm -rf {} +