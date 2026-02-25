import type { ReactNode } from 'react';
import clsx from 'clsx';
import styles from './styles.module.css';

export interface Package {
  /** Package name (e.g., "Dispatch", "Excalibur.Domain") */
  name: string;
  /** Short description */
  description?: string;
  /** NuGet package link */
  nugetUrl?: string;
  /** Dependencies (other package names) */
  dependencies?: string[];
  /** Visual tier/level (0 = bottom, higher = top) */
  tier?: number;
  /** Is this a core/primary package */
  isPrimary?: boolean;
}

export interface PackageDiagramProps {
  /** Array of packages to display */
  packages: Package[];
  /** Title for the diagram */
  title?: string;
  /** Show dependency arrows */
  showDependencies?: boolean;
  /** Additional CSS class */
  className?: string;
}

/**
 * PackageDiagram - Visualizes NuGet package dependency tree
 *
 * @example
 * <PackageDiagram
 *   title="Package Structure"
 *   packages={[
 *     { name: "Excalibur.Dispatch", tier: 0, isPrimary: true },
 *     { name: "Excalibur.Dispatch.Abstractions", tier: 1, dependencies: ["Excalibur.Dispatch"] },
 *   ]}
 * />
 */
export default function PackageDiagram({
  packages,
  title,
  showDependencies = true,
  className,
}: PackageDiagramProps): ReactNode {
  // Group packages by tier
  const tiers = packages.reduce<Record<number, Package[]>>((acc, pkg) => {
    const tier = pkg.tier ?? 0;
    if (!acc[tier]) acc[tier] = [];
    acc[tier].push(pkg);
    return acc;
  }, {});

  const tierNumbers = Object.keys(tiers)
    .map(Number)
    .sort((a, b) => b - a); // Sort descending (top to bottom)

  return (
    <div className={clsx(styles.container, className)}>
      {title && <h3 className={styles.title}>{title}</h3>}
      <div className={styles.diagram}>
        {tierNumbers.map((tierNum) => (
          <div key={tierNum} className={styles.tier}>
            {tiers[tierNum].map((pkg) => (
              <PackageNode
                key={pkg.name}
                pkg={pkg}
                showDependencies={showDependencies}
              />
            ))}
          </div>
        ))}
      </div>
      {showDependencies && packages.some((p) => p.dependencies?.length) && (
        <div className={styles.legend}>
          <span className={styles.legendArrow}>↓</span>
          <span className={styles.legendText}>depends on</span>
        </div>
      )}
    </div>
  );
}

function PackageNode({
  pkg,
  showDependencies,
}: {
  pkg: Package;
  showDependencies: boolean;
}): ReactNode {
  const content = (
    <div
      className={clsx(styles.package, {
        [styles.packagePrimary]: pkg.isPrimary,
      })}
    >
      <div className={styles.packageName}>{pkg.name}</div>
      {pkg.description && (
        <div className={styles.packageDescription}>{pkg.description}</div>
      )}
      {showDependencies && pkg.dependencies && pkg.dependencies.length > 0 && (
        <div className={styles.dependencies}>
          {pkg.dependencies.map((dep) => (
            <span key={dep} className={styles.dependency}>
              ↓ {dep}
            </span>
          ))}
        </div>
      )}
    </div>
  );

  if (pkg.nugetUrl) {
    return (
      <a
        href={pkg.nugetUrl}
        target="_blank"
        rel="noopener noreferrer"
        className={styles.packageLink}
        aria-label={`View ${pkg.name} on NuGet`}
      >
        {content}
      </a>
    );
  }

  return content;
}
