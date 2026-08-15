#!/usr/bin/env bash
# ==============================================================================
# FoodLoop Database Large Dataset Seeding Script
# Description: Resets all database tables and populates them with a comprehensive,
#              realistic, large-scale dataset across all entities (Users, Stores,
#              Charities, Products, Images, Prices, AI Scans, Orders, Reviews,
#              Addresses, Notifications, Tickets, Disputes, Donations, Audit Logs).
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

echo -e "\033[1;36m=======================================================\033[0m"
echo -e "\033[1;36m       FOODLOOP DATABASE LARGE-SCALE SEEDING           \033[0m"
echo -e "\033[1;36m=======================================================\033[0m"

echo -e "\033[0;36m[i] Target Environment: Server Root at $DIR\033[0m"
echo -e "\033[0;36m[i] Executing FoodLoop.DbTool with --seed...\033[0m"

"$DOTNET_CMD" run --project src/FoodLoop.DbTool -- --seed

echo -e "\033[1;32m[✓] Large dataset seeding completed successfully!\033[0m"
