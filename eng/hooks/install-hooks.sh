#!/bin/bash
# Git Hooks Installation Script (Bash)
# Installs canonical Git hooks from eng/hooks/ to .git/hooks/

set -e

# Colors
RED='\033[0;31m'
YELLOW='\033[1;33m'
GREEN='\033[0;32m'
CYAN='\033[0;36m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${CYAN}Git Hooks Installation - Excalibur.Dispatch${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""

# Verify we're in the repository root
if [ ! -d ".git" ]; then
    echo -e "${RED}✗ ERROR: Not in repository root (no .git directory found)${NC}"
    echo -e "${YELLOW}  Please run this script from the repository root directory${NC}"
    exit 1
fi

# Verify eng/hooks directory exists
if [ ! -d "eng/hooks" ]; then
    echo -e "${RED}✗ ERROR: eng/hooks directory not found${NC}"
    echo -e "${YELLOW}  Canonical hooks should be in eng/hooks/${NC}"
    exit 1
fi

# Create .git/hooks directory if it doesn't exist
if [ ! -d ".git/hooks" ]; then
    echo -e "${CYAN}Creating .git/hooks directory...${NC}"
    mkdir -p ".git/hooks"
fi

install_hook() {
    local hook_name="$1"
    local source_hook="eng/hooks/$hook_name"
    local target_hook=".git/hooks/$hook_name"

    if [ -f "$source_hook" ]; then
        echo -e "${CYAN}Installing $hook_name hook...${NC}"

        if [ -f "$target_hook" ]; then
            echo -e "${YELLOW}  ⚠ Existing hook found - creating backup${NC}"
            local backup_path=".git/hooks/${hook_name}.backup.$(date +%Y%m%d-%H%M%S)"
            cp "$target_hook" "$backup_path"
            echo -e "${GRAY}  Backup saved: $backup_path${NC}"
        fi

        cp "$source_hook" "$target_hook"
        chmod +x "$target_hook"

        echo -e "${GREEN}  ✓ $hook_name hook installed${NC}"
    else
        echo -e "${YELLOW}  ⚠ WARNING: $source_hook not found - skipping${NC}"
    fi
}

install_hook pre-commit
install_hook pre-push

echo ""
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${GREEN}Installation Complete${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""
echo -e "${CYAN}Installed Hooks:${NC}"
find ".git/hooks" -type f ! -name "*.sample" -exec basename {} \; | while read -r hook; do
    echo -e "  • $hook"
done
echo ""
echo -e "${CYAN}Test the pre-commit hook:${NC}"
echo -e "${GRAY}  bash .git/hooks/pre-commit${NC}"
echo ""
echo -e "${CYAN}Documentation:${NC}"
echo -e "${GRAY}  eng/hooks/README.md - Installation and usage guide${NC}"
echo -e "${GRAY}  .git/hooks/README.md - Detailed hook behavior (after installation)${NC}"
echo ""
echo -e "${GREEN}✓ You're all set! The pre-commit hook will now validate namespace depth.${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
