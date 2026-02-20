import type { ReactNode } from 'react';
import clsx from 'clsx';
import Link from '@docusaurus/Link';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import Layout from '@theme/Layout';
import Heading from '@theme/Heading';
import CodeBlock from '@theme/CodeBlock';

import styles from './index.module.css';

// --- SVG Icon Components (brand cyan #00d4ff) ---

function IconLightning() {
  return (
    <svg width="40" height="40" viewBox="0 0 40 40" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
      <path d="M22 4L8 24h10l-2 12 14-20H20l2-12z" fill="var(--dispatch-primary)" />
    </svg>
  );
}

function IconTimeline() {
  return (
    <svg width="40" height="40" viewBox="0 0 40 40" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
      <path d="M8 8v24h24" stroke="var(--dispatch-primary)" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
      <circle cx="14" cy="26" r="3" fill="var(--dispatch-primary)" />
      <circle cx="22" cy="18" r="3" fill="var(--dispatch-primary)" />
      <circle cx="30" cy="12" r="3" fill="var(--dispatch-primary)" />
      <path d="M14 26l8-8 8-6" stroke="var(--dispatch-primary)" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" opacity="0.5" />
    </svg>
  );
}

function IconShield() {
  return (
    <svg width="40" height="40" viewBox="0 0 40 40" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
      <path d="M20 4L6 10v10c0 8.4 5.6 16.2 14 18 8.4-1.8 14-9.6 14-18V10L20 4z" fill="var(--dispatch-primary)" opacity="0.15" />
      <path d="M20 4L6 10v10c0 8.4 5.6 16.2 14 18 8.4-1.8 14-9.6 14-18V10L20 4z" stroke="var(--dispatch-primary)" strokeWidth="2" strokeLinejoin="round" />
      <path d="M15 20l4 4 6-8" stroke="var(--dispatch-primary)" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function IconPipeline() {
  return (
    <svg width="40" height="40" viewBox="0 0 40 40" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
      <rect x="4" y="16" width="8" height="8" rx="2" fill="var(--dispatch-primary)" />
      <rect x="16" y="16" width="8" height="8" rx="2" fill="var(--dispatch-primary)" opacity="0.7" />
      <rect x="28" y="16" width="8" height="8" rx="2" fill="var(--dispatch-primary)" opacity="0.4" />
      <path d="M12 20h4M24 20h4" stroke="var(--dispatch-primary)" strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}

function IconServer() {
  return (
    <svg width="40" height="40" viewBox="0 0 40 40" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
      <rect x="6" y="6" width="28" height="10" rx="3" stroke="var(--dispatch-primary)" strokeWidth="2" />
      <circle cx="12" cy="11" r="2" fill="var(--dispatch-primary)" />
      <rect x="6" y="24" width="28" height="10" rx="3" stroke="var(--dispatch-primary)" strokeWidth="2" />
      <circle cx="12" cy="29" r="2" fill="var(--dispatch-primary)" />
      <path d="M20 16v8" stroke="var(--dispatch-primary)" strokeWidth="2" strokeLinecap="round" strokeDasharray="2 2" />
    </svg>
  );
}

function IconSwap() {
  return (
    <svg width="40" height="40" viewBox="0 0 40 40" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
      <path d="M10 14h20l-6-6" stroke="var(--dispatch-primary)" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M30 26H10l6 6" stroke="var(--dispatch-primary)" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" opacity="0.6" />
    </svg>
  );
}

// Hero paper airplane illustration (decorative, matches brand logo geometry)
function HeroAirplanes() {
  return (
    <div className={styles.heroIllustrationRight} aria-hidden="true">
      <svg width="500" height="300" viewBox="0 0 500 300" fill="none" xmlns="http://www.w3.org/2000/svg">
        {/* Horizontal trail lines from each airplane tail to left edge */}
        <line x1="0" y1="170" x2="130" y2="170" stroke="var(--dispatch-primary)" strokeWidth="1" opacity="0.15" />
        <line x1="0" y1="140" x2="215" y2="140" stroke="var(--dispatch-primary)" strokeWidth="1.2" opacity="0.2" />
        <line x1="0" y1="100" x2="310" y2="100" stroke="var(--dispatch-primary)" strokeWidth="1.5" opacity="0.3" />
        {/* Airplane 1 (smallest, farthest) — logo shape: nose right, fold left */}
        <g transform="translate(170,170) scale(0.4)" opacity="0.3">
          <path d="M0 0L-100-38L-50 0Z" fill="var(--dispatch-primary)" />
          <path d="M0 0L-100 38L-50 0Z" fill="var(--dispatch-primary)" />
          <line x1="0" y1="0" x2="-100" y2="0" stroke="var(--brand-bg-dark)" strokeWidth="5" />
        </g>
        {/* Airplane 2 (medium) */}
        <g transform="translate(270,140) scale(0.55)" opacity="0.55">
          <path d="M0 0L-100-38L-50 0Z" fill="var(--dispatch-primary)" />
          <path d="M0 0L-100 38L-50 0Z" fill="var(--dispatch-primary)" />
          <line x1="0" y1="0" x2="-100" y2="0" stroke="var(--brand-bg-dark)" strokeWidth="5" />
        </g>
        {/* Airplane 3 (largest, closest) */}
        <g transform="translate(390,100) scale(0.8)" opacity="0.85">
          <path d="M0 0L-100-38L-50 0Z" fill="var(--dispatch-primary)" />
          <path d="M0 0L-100 38L-50 0Z" fill="var(--dispatch-primary)" />
          <line x1="0" y1="0" x2="-100" y2="0" stroke="var(--brand-bg-dark)" strokeWidth="5" />
        </g>
      </svg>
    </div>
  );
}

// Excalibur sword illustration (decorative, matches brand crystalline blade)
function HeroSword() {
  return (
    <div className={styles.heroIllustrationLeft} aria-hidden="true">
      <svg width="200" height="360" viewBox="-80 -180 160 420" fill="none" xmlns="http://www.w3.org/2000/svg">
        {/* Outer blade facets */}
        <path d="M0-160L-25-85L-17.5 150L0 180" fill="var(--excalibur-primary)" opacity="0.7" />
        <path d="M0-160L25-85L17.5 150L0 180" fill="var(--excalibur-primary)" opacity="0.7" />
        {/* Inner blade facets */}
        <path d="M0-160L-12.5-85L-8.8 150L0 180" fill="var(--excalibur-light)" opacity="0.9" />
        <path d="M0-160L12.5-85L8.8 150L0 180" fill="var(--excalibur-light)" opacity="0.9" />
        {/* Center highlight */}
        <path d="M0-160L-5-85L-3.8 150L0 180" fill="var(--excalibur-lighter)" />
        <path d="M0-160L5-85L3.8 150L0 180" fill="var(--excalibur-lighter)" />
        {/* Core light */}
        <line x1="0" y1="-160" x2="0" y2="180" stroke="#e0f2fe" strokeWidth="2.5" opacity="0.6" />
        {/* Guard */}
        <polygon points="-62,150 -31,155 31,155 62,150 60,170 -60,170" fill="#0ea5e9" />
        <polygon points="-60,170 -30,174 30,174 60,170" fill="#06b6d4" opacity="0.8" />
        <rect x="-56" y="153" width="112" height="4" fill="#67e8f9" opacity="0.5" />
        {/* Grip */}
        <rect x="-12" y="174" width="24" height="35" fill="var(--brand-bg-darker)" rx="1" />
        <rect x="-10" y="176" width="20" height="30" fill="var(--brand-bg-dark)" rx="1" />
        {/* Pommel */}
        <polygon points="-22,209 0,220 22,209 19,230 -19,230" fill="#0ea5e9" />
        <circle cx="0" cy="219" r="7.5" fill="#67e8f9" />
        <circle cx="0" cy="219" r="4" fill="#e0f2fe" opacity="0.8" />
      </svg>
    </div>
  );
}

// Feature data for the landing page
const features: { title: string; icon: ReactNode; description: string }[] = [
  {
    title: 'Blazing Fast Messaging',
    icon: <IconLightning />,
    description:
      'Zero-allocation message dispatching with minimal overhead. Built for high-throughput scenarios where every microsecond counts.',
  },
  {
    title: 'Event Sourcing Built-in',
    icon: <IconTimeline />,
    description:
      'First-class support for event sourcing patterns. Store events, rebuild state, and maintain a complete audit trail of your domain.',
  },
  {
    title: 'Type-Safe by Design',
    icon: <IconShield />,
    description:
      'Leverage C# generics and pattern matching for compile-time safety. No magic strings, no runtime surprises.',
  },
  {
    title: 'Middleware Pipeline',
    icon: <IconPipeline />,
    description:
      'Composable middleware for cross-cutting concerns: validation, logging, retry policies, circuit breakers, and more.',
  },
  {
    title: 'Production Ready',
    icon: <IconServer />,
    description:
      'Outbox pattern, leader election, OpenTelemetry integration, and health checks. Deploy with confidence.',
  },
  {
    title: 'Drop-in MediatR Replacement',
    icon: <IconSwap />,
    description:
      'Familiar patterns with enhanced capabilities. Migrate gradually with our compatibility layer.',
  },
];

// Quick start code example
const quickStartCode = `// 1. Define your command
public record CreateOrder(string CustomerId, decimal Amount) : IDispatchAction;

// 2. Create a handler
public class CreateOrderHandler : IActionHandler<CreateOrder>
{
    public async Task HandleAsync(
        CreateOrder command,
        CancellationToken ct)
    {
        // Your business logic here
        await _repository.CreateAsync(new Order(command.CustomerId, command.Amount));
    }
}

// 3. Register and dispatch
builder.Services.AddDispatch()
    .AddHandlers(h => h.DiscoverFromEntryAssembly());

// 4. Dispatch your command
await dispatcher.DispatchAsync(new CreateOrder("cust-123", 99.99m));`;

function FeatureCard({ title, icon, description }: { title: string; icon: ReactNode; description: string }) {
  return (
    <div className={clsx('col col--4', styles.featureCol)}>
      <div className="feature-card">
        <div className="feature-card__icon">{icon}</div>
        <div className="feature-card__title">{title}</div>
        <p className="feature-card__description">{description}</p>
      </div>
    </div>
  );
}

function HomepageHeader() {
  const { siteConfig } = useDocusaurusContext();
  return (
    <header className={clsx('hero', styles.heroBanner)}>
      <HeroSword />
      <HeroAirplanes />
      <div className="container">
        <div className={styles.heroContent}>
          <span className={styles.heroBadge}>Open Source .NET Framework</span>
          <Heading as="h1" className="hero__title">
            {siteConfig.title}
          </Heading>
          <p className="hero__subtitle">
            Dispatch messaging core + Excalibur CQRS/hosting wrapper for modern .NET services
          </p>
          <div className={styles.heroButtons}>
            <Link className="cta-button cta-button--primary" to="/docs/intro">
              Get Started
            </Link>
            <Link
              className="cta-button cta-button--secondary"
              href="https://github.com/TrigintaFaces/Excalibur"
            >
              View on GitHub
            </Link>
          </div>
        </div>
      </div>
    </header>
  );
}

function FeaturesSection() {
  return (
    <section className={styles.featuresSection}>
      <div className="container">
        <div className={styles.sectionHeader}>
          <Heading as="h2">Why Excalibur + Dispatch?</Heading>
          <p>Start lightweight with Dispatch and opt into Excalibur when you need CQRS, hosting, or compliance</p>
        </div>
        <div className="row">
          {features.map((props, idx) => (
            <FeatureCard key={idx} {...props} />
          ))}
        </div>
      </div>
    </section>
  );
}

function DecisionMatrixSection() {
  return (
    <section className={styles.decisionSection}>
      <div className="container">
        <div className={styles.sectionHeader}>
          <Heading as="h2">Which Packages Do You Need?</Heading>
          <p>Start with Excalibur.Dispatch. Add Excalibur when you need more.</p>
        </div>
        <div className={styles.decisionMatrix}>
          <table>
            <thead>
              <tr>
                <th>Scenario</th>
                <th>Packages</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td><strong>Simple messaging (MediatR replacement)</strong></td>
                <td><code>Excalibur.Dispatch</code> + <code>Excalibur.Dispatch.Abstractions</code></td>
              </tr>
              <tr>
                <td><strong>Add transport (Kafka, RabbitMQ, etc.)</strong></td>
                <td>+ <code>Excalibur.Dispatch.Transport.*</code></td>
              </tr>
              <tr>
                <td><strong>Domain modeling (aggregates, entities)</strong></td>
                <td>+ <code>Excalibur.Domain</code></td>
              </tr>
              <tr>
                <td><strong>Event sourcing with persistence</strong></td>
                <td>+ <code>Excalibur.EventSourcing</code> + <code>Excalibur.EventSourcing.SqlServer</code></td>
              </tr>
              <tr>
                <td><strong>Full CQRS/hosting with compliance</strong></td>
                <td>+ <code>Excalibur.Hosting.Web</code></td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>
  );
}

function QuickStartSection() {
  return (
    <section className="quick-start-section">
      <div className="container">
        <div className={styles.sectionHeader}>
          <Heading as="h2">Quick Start</Heading>
          <p>Get up and running in minutes</p>
        </div>
        <div className={styles.quickStartGrid}>
          <div className={styles.quickStartInstall}>
            <h3>Install Dispatch (messaging core)</h3>
            <CodeBlock language="bash">
{`dotnet add package Dispatch
dotnet add package Excalibur.Dispatch.Abstractions`}
            </CodeBlock>
            <h4 style={{ marginTop: '1rem' }}>Add Excalibur when you need CQRS/hosting</h4>
            <CodeBlock language="bash">
{`dotnet add package Excalibur.Domain
dotnet add package Excalibur.EventSourcing
dotnet add package Excalibur.Hosting.Web`}
            </CodeBlock>
          </div>
          <div className={styles.quickStartCode}>
            <h3>Write your first handler</h3>
            <CodeBlock language="csharp" title="Program.cs">
              {quickStartCode}
            </CodeBlock>
          </div>
        </div>
      </div>
    </section>
  );
}

function ComparisonSection() {
  return (
    <section className={styles.comparisonSection}>
      <div className="container">
        <div className={styles.sectionHeader}>
          <Heading as="h2">How It Compares</Heading>
          <p>Built for performance without sacrificing developer experience</p>
        </div>
        <div className={styles.comparisonTable}>
          <table>
            <thead>
              <tr>
                <th>Feature</th>
                <th>Dispatch + Excalibur</th>
                <th>MediatR</th>
                <th>MassTransit</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>Zero-allocation dispatch</td>
                <td>✅</td>
                <td>❌</td>
                <td>❌</td>
              </tr>
              <tr>
                <td>Event sourcing</td>
                <td>✅ Built-in</td>
                <td>❌</td>
                <td>❌</td>
              </tr>
              <tr>
                <td>Outbox pattern</td>
                <td>✅ Built-in</td>
                <td>❌</td>
                <td>✅</td>
              </tr>
              <tr>
                <td>OpenTelemetry</td>
                <td>✅</td>
                <td>❌</td>
                <td>✅</td>
              </tr>
              <tr>
                <td>Type-safe middleware</td>
                <td>✅</td>
                <td>⚠️ Limited</td>
                <td>✅</td>
              </tr>
              <tr>
                <td>Serverless support</td>
                <td>✅</td>
                <td>❌</td>
                <td>⚠️ Limited</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>
  );
}

function CTASection() {
  return (
    <section className={styles.ctaSection}>
      <div className="container">
        <div className={styles.ctaContent}>
          <Heading as="h2">Ready to Get Started?</Heading>
          <p>
            Join developers building fast, reliable .NET applications with
            Dispatch + Excalibur
          </p>
          <div className={styles.ctaButtons}>
            <Link className="cta-button cta-button--primary" to="/docs/intro">
              Read the Docs
            </Link>
            <Link
              className="cta-button cta-button--secondary"
              href="https://github.com/TrigintaFaces/Excalibur"
            >
              Star on GitHub
            </Link>
          </div>
        </div>
      </div>
    </section>
  );
}

export default function Home(): ReactNode {
  const { siteConfig } = useDocusaurusContext();
  return (
    <Layout
      title="Fast .NET Messaging Framework"
      description="Dispatch is a zero-allocation .NET messaging framework. Add Excalibur packages for event sourcing, CQRS, and compliance. Drop-in MediatR replacement."
    >
      <HomepageHeader />
      <main>
        <FeaturesSection />
        <DecisionMatrixSection />
        <QuickStartSection />
        <ComparisonSection />
        <CTASection />
      </main>
    </Layout>
  );
}
