import type { ReactNode } from 'react';
import CodeBlock from '@theme/CodeBlock';
import styles from './styles.module.css';

export interface CodeComparisonProps {
  /** Code before the change */
  beforeCode: string;
  /** Code after the change */
  afterCode: string;
  /** Programming language for syntax highlighting */
  language?: string;
  /** Label for the "before" panel */
  beforeLabel?: string;
  /** Label for the "after" panel */
  afterLabel?: string;
  /** Title for the before code block */
  beforeTitle?: string;
  /** Title for the after code block */
  afterTitle?: string;
}

/**
 * CodeComparison - Side-by-side code comparison component
 *
 * @example
 * <CodeComparison
 *   beforeLabel="MediatR"
 *   afterLabel="Excalibur.Dispatch"
 *   beforeCode="services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));"
 *   afterCode="services.AddDispatch().AddHandlers(h => h.DiscoverFromEntryAssembly());"
 *   language="csharp"
 * />
 */
export default function CodeComparison({
  beforeCode,
  afterCode,
  language = 'csharp',
  beforeLabel = 'Before',
  afterLabel = 'After',
  beforeTitle,
  afterTitle,
}: CodeComparisonProps): ReactNode {
  return (
    <div className={styles.container}>
      <div className={styles.panel}>
        <div className={styles.headerBefore}>
          <span className={styles.badge}>{beforeLabel}</span>
        </div>
        <CodeBlock language={language} title={beforeTitle}>
          {beforeCode}
        </CodeBlock>
      </div>
      <div className={styles.panel}>
        <div className={styles.headerAfter}>
          <span className={styles.badge}>{afterLabel}</span>
        </div>
        <CodeBlock language={language} title={afterTitle}>
          {afterCode}
        </CodeBlock>
      </div>
    </div>
  );
}
