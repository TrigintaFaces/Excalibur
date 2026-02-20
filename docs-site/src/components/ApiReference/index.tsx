import type { ReactNode } from 'react';
import clsx from 'clsx';
import CodeBlock from '@theme/CodeBlock';
import styles from './styles.module.css';

export interface ApiMember {
  /** Member name (method, property, etc.) */
  name: string;
  /** Member type: method, property, event, field */
  kind: 'method' | 'property' | 'event' | 'field' | 'constructor';
  /** Method signature or property type */
  signature?: string;
  /** Description of the member */
  description?: string;
  /** Return type for methods */
  returnType?: string;
  /** Parameters for methods */
  parameters?: Array<{
    name: string;
    type: string;
    description?: string;
    optional?: boolean;
  }>;
  /** Example code */
  example?: string;
  /** Is this member static */
  isStatic?: boolean;
  /** Access modifier */
  access?: 'public' | 'protected' | 'private' | 'internal';
}

export interface ApiReferenceProps {
  /** Namespace (e.g., "Dispatch", "Excalibur.Domain") */
  namespace: string;
  /** Type name (class, interface, struct) */
  typeName: string;
  /** Type kind */
  typeKind?: 'class' | 'interface' | 'struct' | 'enum' | 'record';
  /** Type description */
  description?: string;
  /** List of members */
  members?: ApiMember[];
  /** Base types or interfaces implemented */
  inherits?: string[];
  /** Additional CSS class */
  className?: string;
}

/**
 * ApiReference - Shell component for API documentation
 *
 * @example
 * <ApiReference
 *   namespace="Dispatch"
 *   typeName="IDispatcher"
 *   typeKind="interface"
 *   description="Core interface for dispatching messages"
 *   members={[
 *     {
 *       name: "DispatchAsync",
 *       kind: "method",
 *       signature: "Task<IMessageResult> DispatchAsync<TMessage>(TMessage message, CancellationToken ct)",
 *       description: "Dispatches a message to its handler"
 *     }
 *   ]}
 * />
 */
export default function ApiReference({
  namespace,
  typeName,
  typeKind = 'class',
  description,
  members = [],
  inherits,
  className,
}: ApiReferenceProps): ReactNode {
  const kindIcons: Record<string, string> = {
    class: '📦',
    interface: '📋',
    struct: '🔲',
    enum: '🔢',
    record: '📝',
  };

  const memberKindIcons: Record<string, string> = {
    method: 'M',
    property: 'P',
    event: 'E',
    field: 'F',
    constructor: 'C',
  };

  return (
    <div className={clsx(styles.container, className)}>
      {/* Type Header */}
      <div className={styles.header}>
        <div className={styles.typeInfo}>
          <span className={styles.typeIcon}>{kindIcons[typeKind]}</span>
          <div className={styles.typeMeta}>
            <span className={styles.namespace}>{namespace}</span>
            <h2 className={styles.typeName}>
              {typeName}
              <span className={styles.typeKind}>{typeKind}</span>
            </h2>
          </div>
        </div>
        {inherits && inherits.length > 0 && (
          <div className={styles.inherits}>
            <span className={styles.inheritsLabel}>Implements:</span>
            {inherits.map((type) => (
              <code key={type} className={styles.inheritsType}>
                {type}
              </code>
            ))}
          </div>
        )}
      </div>

      {/* Description */}
      {description && <p className={styles.description}>{description}</p>}

      {/* Members */}
      {members.length > 0 && (
        <div className={styles.members}>
          <h3 className={styles.membersTitle}>Members</h3>
          <div className={styles.membersList}>
            {members.map((member) => (
              <MemberCard key={member.name} member={member} icons={memberKindIcons} />
            ))}
          </div>
        </div>
      )}

      {/* Empty State */}
      {members.length === 0 && (
        <div className={styles.emptyState}>
          <p>API documentation coming soon.</p>
        </div>
      )}
    </div>
  );
}

function MemberCard({
  member,
  icons,
}: {
  member: ApiMember;
  icons: Record<string, string>;
}): ReactNode {
  return (
    <div className={styles.member}>
      <div className={styles.memberHeader}>
        <span
          className={clsx(styles.memberKind, styles[`memberKind${member.kind}`])}
          title={member.kind}
        >
          {icons[member.kind]}
        </span>
        <span className={styles.memberName}>{member.name}</span>
        {member.isStatic && <span className={styles.memberStatic}>static</span>}
      </div>

      {member.signature && (
        <code className={styles.memberSignature}>{member.signature}</code>
      )}

      {member.description && (
        <p className={styles.memberDescription}>{member.description}</p>
      )}

      {member.parameters && member.parameters.length > 0 && (
        <div className={styles.parameters}>
          <div className={styles.parametersTitle}>Parameters</div>
          <table className={styles.parametersTable}>
            <thead>
              <tr>
                <th>Name</th>
                <th>Type</th>
                <th>Description</th>
              </tr>
            </thead>
            <tbody>
              {member.parameters.map((param) => (
                <tr key={param.name}>
                  <td>
                    <code>{param.name}</code>
                    {param.optional && <span className={styles.optional}>?</span>}
                  </td>
                  <td>
                    <code>{param.type}</code>
                  </td>
                  <td>{param.description || '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {member.example && (
        <div className={styles.example}>
          <CodeBlock language="csharp">{member.example}</CodeBlock>
        </div>
      )}
    </div>
  );
}
