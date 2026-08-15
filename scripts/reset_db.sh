#!/usr/bin/env bash
# ==============================================================================
# FoodLoop Database Reset Script
# Description: Disables foreign key constraints, cleanly deletes all data from
#              all database tables, and re-enables constraints.
# ==============================================================================

set -e

# Change to the server root directory
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$DIR"

# Resolve dotnet executable portably across Git Bash, WSL, Linux, macOS
if command -v dotnet >/dev/null 2>&1; then
    DOTNET_CMD="dotnet"
elif command -v dotnet.exe >/dev/null 2>&1; then
    DOTNET_CMD="dotnet.exe"
elif [ -f "/c/Program Files/dotnet/dotnet.exe" ]; then
    DOTNET_CMD="/c/Program Files/dotnet/dotnet.exe"
elif [ -f "/mnt/c/Program Files/dotnet/dotnet.exe" ]; then
    DOTNET_CMD="/mnt/c/Program Files/dotnet/dotnet.exe"
elif [ -f "$HOME/.dotnet/dotnet" ]; then
    DOTNET_CMD="$HOME/.dotnet/dotnet"
else
    DOTNET_CMD="dotnet"
fi

echo -e "\033[1;33m=======================================================\033[0m"
echo -e "\033[1;33m           FOODLOOP DATABASE RESET SCRIPT              \033[0m"
echo -e "\033[1;33m=======================================================\033[0m"

echo -e "\033[0;36m[i] Target Environment: Server Root at $DIR\033[0m"
echo -e "\033[0;36m[i] Executing FoodLoop.DbTool with --reset...\033[0m"

"$DOTNET_CMD" run --project src/FoodLoop.DbTool -- --reset

echo -e "\033[1;32m[✓] Database reset completed successfully!\033[0m"
