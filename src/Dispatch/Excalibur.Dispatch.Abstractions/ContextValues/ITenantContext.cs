// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Read-only ambient accessor for the tenant identifier associated with the current
/// execution flow.
/// </summary>
/// <remarks>
/// <para>
/// The ambient tenant flows through the logical call context (an <c>AsyncLocal</c>-backed
/// scope established by the tenant resolver / inbound middleware), never a thread-static
/// slot. This contract exposes no mutator: the ambient tenant is structurally immutable
/// through <see cref="ITenantContext"/>, and can only be established by the resolving
/// scope. Consumers read the current tenant; they cannot reassign it.
/// </para>
/// <para>
/// <b>Invariant (history constraint).</b> Across every implementation, the tenant scope observed for a
/// given execution flow is preserved — never mutated or downgraded — through this contract: repeated reads
/// within the same flow return the same value. Implementations MUST also keep <see cref="HasTenant"/>
/// consistent with <see cref="TenantId"/>: <see cref="HasTenant"/> is <see langword="true"/> exactly when
/// <see cref="TenantId"/> is a non-<see langword="null"/>, non-empty identifier. A behavioral subtype may
/// not weaken this: it may not report a tenant present while exposing a null/empty id, nor silently switch
/// the resolved tenant of a flow already in progress.
/// </para>
/// </remarks>
public interface ITenantContext
{
	/// <summary>
	/// Gets the identifier of the current ambient tenant.
	/// </summary>
	/// <value>The current tenant identifier, or <see langword="null"/> when no tenant has been resolved.</value>
	string? TenantId { get; }

	/// <summary>
	/// Gets a value indicating whether a tenant is resolved for the current execution flow.
	/// </summary>
	/// <value><see langword="true"/> when an ambient tenant is present; otherwise, <see langword="false"/>.</value>
	bool HasTenant { get; }
}
