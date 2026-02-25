import type { ReactNode } from 'react';
import clsx from 'clsx';
import Link from '@docusaurus/Link';
import styles from './styles.module.css';

export interface FeatureCardProps {
  /** Card title */
  title: string;
  /** Description text or React node */
  description: ReactNode;
  /** Icon (emoji, SVG component, or string) */
  icon?: ReactNode;
  /** Optional link to navigate to */
  link?: string;
  /** Link label for accessibility */
  linkLabel?: string;
  /** Additional CSS class */
  className?: string;
}

/**
 * FeatureCard - A card component for highlighting features
 *
 * @example
 * <FeatureCard
 *   title="Event Sourcing"
 *   description="Store events, rebuild state, maintain audit trails"
 *   icon="📜"
 *   link="/docs/patterns/event-sourcing"
 * />
 */
export default function FeatureCard({
  title,
  description,
  icon,
  link,
  linkLabel,
  className,
}: FeatureCardProps): ReactNode {
  const content = (
    <>
      {icon && <div className={styles.icon}>{icon}</div>}
      <div className={styles.content}>
        <h3 className={styles.title}>{title}</h3>
        <p className={styles.description}>{description}</p>
      </div>
      {link && (
        <div className={styles.linkArrow}>
          <span aria-hidden="true">→</span>
        </div>
      )}
    </>
  );

  if (link) {
    return (
      <Link
        to={link}
        className={clsx(styles.card, styles.cardLink, className)}
        aria-label={linkLabel || `Learn more about ${title}`}
      >
        {content}
      </Link>
    );
  }

  return <div className={clsx(styles.card, className)}>{content}</div>;
}
