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

# Install pre-commit hook
SOURCE_HOOK="eng/hooks/pre-commit"
TARGET_HOOK=".git/hooks/pre-commit"

if [ -f "$SOURCE_HOOK" ]; then
    echo -e "${CYAN}Installing pre-commit hook...${NC}"

    # Check if hook already exists
    if [ -f "$TARGET_HOOK" ]; then
        echo -e "${YELLOW}  ⚠ Existing hook found - creating backup${NC}"
        BACKUP_PATH=".git/hooks/pre-commit.backup.$(date +%Y%m%d-%H%M%S)"
        cp "$TARGET_HOOK" "$BACKUP_PATH"
        echo -e "${GRAY}  Backup saved: $BACKUP_PATH${NC}"
    fi

    # Copy hook and make executable
    cp "$SOURCE_HOOK" "$TARGET_HOOK"
    chmod +x "$TARGET_HOOK"

    echo -e "${GREEN}  ✓ pre-commit hook installed${NC}"
else
    echo -e "${YELLOW}  ⚠ WARNING: $SOURCE_HOOK not found - skipping${NC}"
fi

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
