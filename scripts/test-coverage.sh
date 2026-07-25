#!/usr/bin/env bash
set -e

dotnet test \
    --collect:"XPlat Code Coverage"