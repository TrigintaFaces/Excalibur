#!/bin/bash
# Convert SVG assets to PNG files
# Requires Inkscape to be installed: https://inkscape.org/
#
# Usage: ./convert-to-png.sh

set -e

# Colors
CYAN='\033[0;36m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Check if Inkscape is installed
if ! command -v inkscape &> /dev/null; then
    echo -e "${RED}Inkscape is not installed. Please install from https://inkscape.org/${NC}"
    exit 1
fi

echo -e "${CYAN}Converting SVG files to PNG...${NC}"

# Create png directory
mkdir -p png
echo -e "${GREEN}Created png directory${NC}"

# Main logos
echo -e "\n${YELLOW}Converting main logos...${NC}"
inkscape logo.svg -w 512 -o png/logo-512.png
inkscape logo-light.svg -w 512 -o png/logo-light-512.png
inkscape logo-horizontal.svg -w 800 -o png/logo-horizontal.png

# Icons at multiple sizes
echo -e "\n${YELLOW}Converting icons...${NC}"
inkscape icon-transparent.svg -w 512 -o png/icon-512.png
inkscape icon-transparent.svg -w 256 -o png/icon-256.png
inkscape icon-transparent.svg -w 128 -o png/icon-128.png
inkscape icon-transparent.svg -w 64 -o png/icon-64.png
inkscape icon-transparent.svg -w 32 -o png/icon-32.png
inkscape icon-transparent.svg -w 16 -o png/icon-16.png

# NuGet icons
echo -e "\n${YELLOW}Converting NuGet icons...${NC}"
inkscape nuget-icon-128.svg -w 128 -o png/nuget-icon-128.png
inkscape nuget-icon-64.svg -w 64 -o png/nuget-icon-64.png

# Social media and marketing
echo -e "\n${YELLOW}Converting social media assets...${NC}"
inkscape social-card.svg -w 1200 -o png/social-card.png
inkscape github-banner.svg -w 1280 -o png/github-banner.png
inkscape readme-banner.svg -w 1600 -o png/readme-banner.png

# Favicon (simple PNG export, ICO needs ImageMagick)
echo -e "\n${YELLOW}Converting favicon...${NC}"
inkscape favicon.svg -w 32 -o png/favicon-32.png
inkscape favicon.svg -w 16 -o png/favicon-16.png

echo -e "\n${GREEN}✓ Conversion complete!${NC}"
echo -e "${CYAN}PNG files saved to: png/${NC}"

# Check for ImageMagick to create favicon.ico
if command -v convert &> /dev/null; then
    echo -e "\n${YELLOW}Creating favicon.ico...${NC}"
    convert png/favicon-16.png png/favicon-32.png png/favicon.ico
    echo -e "${GREEN}✓ favicon.ico created${NC}"
else
    echo -e "\n${YELLOW}ImageMagick not found. Skipping favicon.ico creation.${NC}"
    echo -e "${YELLOW}To create favicon.ico, install ImageMagick: https://imagemagick.org/${NC}"
    echo -e "${YELLOW}Then run: convert png/favicon-16.png png/favicon-32.png png/favicon.ico${NC}"
fi

echo -e "\n${GREEN}Done! All assets are ready for use.${NC}"
