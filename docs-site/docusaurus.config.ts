import type * as Preset from '@docusaurus/preset-classic';
import type { Config } from '@docusaurus/types';
import { themes as prismThemes } from 'prism-react-renderer';
import { existsSync, readFileSync } from 'node:fs';

// This runs in Node.js - Don't use client-side code here (browser APIs, JSX...)

// Use strict link checking in CI environments
// Set DOCUSAURUS_STRICT_LINKS=true in CI to fail on broken links
const isStrictMode = process.env.DOCUSAURUS_STRICT_LINKS === 'true' || process.env.CI === 'true';

const detectedStableVersions = (() => {
  if (!existsSync('./versions.json')) {
    return [] as string[];
  }

  try {
    const parsed = JSON.parse(readFileSync('./versions.json', 'utf8'));
    if (!Array.isArray(parsed)) {
      return [] as string[];
    }

    return parsed.filter((value): value is string => typeof value === 'string' && value.length > 0);
  } catch {
    return [] as string[];
  }
})();

const hasStableDocsRelease =
  process.env.DOCS_HAS_STABLE_RELEASE === 'true' ||
  detectedStableVersions.length > 0;
const latestStableVersion = process.env.DOCS_LAST_STABLE_VERSION?.trim() || detectedStableVersions[0];
const currentDocsPath = hasStableDocsRelease ? 'next' : '';
const announcementText = process.env.DOCS_ANNOUNCEMENT_TEXT?.trim();
const announcementId = process.env.DOCS_ANNOUNCEMENT_ID?.trim() || 'docs-site-announcement';

const config: Config = {
  title: 'Excalibur + Dispatch',
  tagline: 'Zero-allocation messaging, domain modeling, and event sourcing for .NET. Start minimal, scale up.',
  favicon: 'Dispatch/favicon.svg',

  // Static directories - include canonical images folder from repo root
  // The ../images folder content is served at root, so images/Dispatch/logo.svg becomes /Dispatch/logo.svg
  staticDirectories: ['static', '../images'],

  // Future flags for Docusaurus v4 compatibility
  future: {
    v4: true,
  },

  // Production URL for custom domain
  url: 'https://docs.excalibur-dispatch.dev',
  // Set the /<baseUrl>/ pathname under which your site is served
  baseUrl: '/',

  // GitHub pages deployment config
  organizationName: 'TrigintaFaces', // GitHub organization
  projectName: 'Excalibur', // Repo name
  deploymentBranch: 'gh-pages',
  trailingSlash: false,

  // Sprint 461 T5.4: Strict mode for CI environments
  // In CI (isStrictMode=true), broken links will fail the build
  // Locally, they will only warn to avoid blocking development
  onBrokenLinks: isStrictMode ? 'throw' : 'warn',
  markdown: {
    hooks: {
      onBrokenMarkdownLinks: isStrictMode ? 'throw' : 'warn',
    },
  },

  // Internationalization
  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  // SEO and Analytics configuration
  headTags: [
    // Open Graph meta tags for social sharing
    {
      tagName: 'meta',
      attributes: {
        property: 'og:type',
        content: 'website',
      },
    },
    {
      tagName: 'meta',
      attributes: {
        property: 'og:site_name',
        content: 'Excalibur Documentation',
      },
    },
    // Twitter Card meta tags
    {
      tagName: 'meta',
      attributes: {
        name: 'twitter:card',
        content: 'summary_large_image',
      },
    },
    // Additional SEO meta tags
    {
      tagName: 'meta',
      attributes: {
        name: 'keywords',
        content: 'dotnet, csharp, messaging, event-sourcing, mediator, cqrs, outbox-pattern, domain-driven-design, nuget',
      },
    },
    {
      tagName: 'meta',
      attributes: {
        name: 'author',
        content: 'The Excalibur Project',
      },
    },
    // JSON-LD structured data for software framework
    {
      tagName: 'script',
      attributes: {
        type: 'application/ld+json',
      },
      innerHTML: JSON.stringify({
        '@context': 'https://schema.org',
        '@type': 'SoftwareSourceCode',
        name: 'Excalibur',
        description: 'Zero-allocation .NET messaging with domain modeling and event sourcing.',
        programmingLanguage: 'C#',
        runtimePlatform: '.NET',
        codeRepository: 'https://github.com/TrigintaFaces/Excalibur',
        license: 'https://opensource.org/licenses/MIT',
      }),
    },
  ],

  // LLM-friendly documentation generation (llms.txt + llms-full.txt)
  plugins: [
    [
      'docusaurus-plugin-llms',
      {
        generateLLMsTxt: true,
        generateLLMsFullTxt: true,
        docsDir: 'docs',
        includeBlog: false,
      },
    ],
  ],

  // Themes - including local search
  themes: [
    [
      '@easyops-cn/docusaurus-search-local',
      {
        hashed: true,
        language: ['en'],
        highlightSearchTermsOnTargetPage: true,
        explicitSearchResultPath: true,
        indexDocs: true,
        indexBlog: false,
        indexPages: true,
        // Unified documentation
        docsRouteBasePath: '/docs',
        docsDir: 'docs',
      },
    ],
  ],

  presets: [
    [
      'classic',
      {
        docs: {
          // Excalibur documentation
          path: 'docs',
          routeBasePath: 'docs',
          sidebarPath: './sidebars.ts',
          editUrl: 'https://github.com/TrigintaFaces/Excalibur/tree/main/docs-site/',
          showLastUpdateAuthor: true,
          showLastUpdateTime: true,

          // Version configuration:
          // - Pre-release: only current exists, routed under /docs/next
          // - After first release: latest stable becomes default under /docs/*
          lastVersion: latestStableVersion || 'current',
          versions: {
            current: {
              label: hasStableDocsRelease ? 'Next (unreleased)' : 'Current',
              path: currentDocsPath,
              banner: hasStableDocsRelease ? 'unreleased' : 'none',
            },
            // Add versions as they are released:
            // '1.0.0': {
            //   label: '1.0.0',
            //   path: '',  // Default path (no prefix for latest)
            //   banner: 'none',
            // },
          },
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      } satisfies Preset.Options,
    ],
  ],

  themeConfig: {
    // Social card for link previews
    image: 'Dispatch/social-card.svg',

    // Color mode configuration
    colorMode: {
      defaultMode: 'light',
      disableSwitch: false,
      respectPrefersColorScheme: true,
    },

    // Navbar configuration
    navbar: {
      title: 'Excalibur + Dispatch',
      logo: {
        alt: 'Dispatch Logo',
        src: 'Dispatch/logo.svg',
        srcDark: 'Dispatch/logo-light.svg',
      },
      items: [
        // Unified documentation dropdown
        {
          type: 'dropdown',
          label: 'Documentation',
          position: 'left',
          items: [
            {
              to: '/docs/intro',
              label: 'Introduction',
            },
            {
              to: '/docs/getting-started/',
              label: 'Getting Started',
            },
            {
              to: '/docs/handlers',
              label: 'Build',
            },
            {
              to: '/docs/data-providers/',
              label: 'Data Providers',
            },
          ],
        },
        {
          type: 'docsVersionDropdown',
          position: 'left',
        },
        // NuGet packages
        {
          type: 'dropdown',
          label: 'Packages',
          position: 'left',
          items: [
            {
              href: 'https://www.nuget.org/packages?q=owner:SystemDeveloper',
              label: 'Excalibur.*',
            },
          ],
        },
        {
          href: 'https://github.com/TrigintaFaces/Excalibur',
          label: 'GitHub',
          position: 'right',
        },
      ],
    },

    // Footer configuration
    footer: {
      style: 'dark',
      links: [
        {
          title: 'Documentation',
          items: [
            {
              label: 'Introduction',
              to: '/docs/intro',
            },
            {
              label: 'Getting Started',
              to: '/docs/getting-started/',
            },
            {
              label: 'Event Sourcing',
              to: '/docs/event-sourcing/',
            },
          ],
        },
        {
          title: 'Packages',
          items: [
            {
              label: 'Excalibur.*',
              href: 'https://www.nuget.org/packages?q=owner:SystemDeveloper',
            },
          ],
        },
        {
          title: 'Community',
          items: [
            {
              label: 'GitHub Discussions',
              href: 'https://github.com/TrigintaFaces/Excalibur/discussions',
            },
            {
              label: 'Stack Overflow',
              href: 'https://stackoverflow.com/questions/tagged/excalibur-dispatch',
            },
          ],
        },
        {
          title: 'More',
          items: [
            {
              label: 'GitHub',
              href: 'https://github.com/TrigintaFaces/Excalibur',
            },
          ],
        },
      ],
      copyright: `Copyright © ${new Date().getFullYear()} The Excalibur Project. Built with Docusaurus.`,
    },

    // Prism code highlighting configuration
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
      additionalLanguages: [
        'csharp',
        'json',
        'yaml',
        'bash',
        'powershell',
        'sql',
        'markup',
        'diff',
      ],
    },

    // Table of contents configuration
    tableOfContents: {
      minHeadingLevel: 2,
      maxHeadingLevel: 4,
    },

    // Documentation-specific settings
    docs: {
      sidebar: {
        hideable: true,
        autoCollapseCategories: true,
      },
    },
    ...(announcementText
      ? {
        announcementBar: {
          id: announcementId,
          content: announcementText,
          backgroundColor: '#0f172a',
          textColor: '#ffffff',
          isCloseable: true,
        },
      }
      : {}),
  } satisfies Preset.ThemeConfig,
};

export default config;
