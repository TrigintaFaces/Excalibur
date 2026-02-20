#!/usr/bin/env node
/**
 * SVG to PNG converter using sharp
 * Generates PNG files needed for NuGet package icons and README banners
 */
const sharp = require('sharp');
const fs = require('fs');
const path = require('path');

const conversions = [
  // Dispatch NuGet icons
  { input: 'Dispatch/nuget-icon-128.svg', output: 'Dispatch/png/icon.png', width: 128 },
  { input: 'Dispatch/nuget-icon-64.svg', output: 'Dispatch/png/icon-64.png', width: 64 },
  { input: 'Dispatch/icon-transparent.svg', output: 'Dispatch/png/icon-256.png', width: 256 },
  { input: 'Dispatch/readme-banner.svg', output: 'Dispatch/png/readme-banner.png', width: 1600 },
  { input: 'Dispatch/github-banner.svg', output: 'Dispatch/png/github-banner.png', width: 1280 },

  // Excalibur NuGet icons
  { input: 'Excalibur/nuget-icon-128.svg', output: 'Excalibur/png/icon.png', width: 128 },
  { input: 'Excalibur/nuget-icon-64.svg', output: 'Excalibur/png/icon-64.png', width: 64 },
  { input: 'Excalibur/icon-transparent.svg', output: 'Excalibur/png/icon-256.png', width: 256 },
  { input: 'Excalibur/readme-banner.svg', output: 'Excalibur/png/readme-banner.png', width: 1600 },
  { input: 'Excalibur/github-banner.svg', output: 'Excalibur/png/github-banner.png', width: 1280 },
];

async function convertSvgToPng() {
  const baseDir = __dirname;

  for (const conv of conversions) {
    const inputPath = path.join(baseDir, conv.input);
    const outputPath = path.join(baseDir, conv.output);
    const outputDir = path.dirname(outputPath);

    // Ensure output directory exists
    if (!fs.existsSync(outputDir)) {
      fs.mkdirSync(outputDir, { recursive: true });
      console.log(`Created directory: ${outputDir}`);
    }

    // Check if input exists
    if (!fs.existsSync(inputPath)) {
      console.warn(`Warning: Input file not found: ${inputPath}`);
      continue;
    }

    try {
      await sharp(inputPath)
        .resize(conv.width)
        .png()
        .toFile(outputPath);
      console.log(`Converted: ${conv.input} -> ${conv.output} (${conv.width}px)`);
    } catch (err) {
      console.error(`Error converting ${conv.input}: ${err.message}`);
    }
  }

  console.log('\nConversion complete!');
}

convertSvgToPng().catch(console.error);
