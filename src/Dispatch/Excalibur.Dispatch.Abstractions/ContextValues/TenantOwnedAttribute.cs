// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Declares that a persistence store contract holds rows owned by a tenant, so a deployment using
/// row-discriminator multi-tenancy must not register an implementation of it that cannot confine reads
/// to the ambient tenant or re-establish the owning tenant from the row.
/// </summary>
/// <remarks>
/// <para>
/// This attribute is the <em>oracle</em> for tenant-scoping coverage, and it is deliberately attached to the
/// contract rather than held in a list. A hand-maintained manifest of contracts-that-must-be-gated cannot
/// catch the contract nobody remembered to add to it, which is the only failure this coverage check has ever
/// actually suffered. Marking the interface moves the obligation to the point of declaration: a new
/// tenant-owned contract is covered the moment it is declared, not the moment someone updates a list
/// elsewhere.
/// </para>
/// <para>
/// The shape is deliberately that of the framework attributes a reader already knows - the marker states the
/// obligation at the declaration and the composition root enforces it, exactly as an authorization attribute
/// states an endpoint's obligation and the pipeline enforces it. The attribute asserts <em>ownership</em>
/// only; it says nothing about which mechanism satisfies it.
/// </para>
/// <para>
/// <b>What satisfies it.</b> Registering a contract marked with this attribute under
/// row-discriminator multi-tenancy requires the provider to present one of the two capability markers:
/// <see cref="ITenantScopingCapability{TContract}"/>, meaning reads are confined to the ambient tenant, or
/// <see cref="ITenantPartitionedCapability{TContract}"/>, meaning reads are deliberately estate-wide and the
/// owning tenant is re-established from the row. The two attest different mechanisms and are not
/// interchangeable for any given contract - a store that drains every tenant's rows in one pass must not
/// claim ambient scoping, and a store serving tenant-facing reads must not claim to be estate-wide. This
/// attribute requires that a contract present <em>one</em> of them; which one is correct is a property of the
/// contract and is enforced separately.
/// </para>
/// <para>
/// <b>What it does not do.</b> It confines nothing by itself. Confinement is written in each store's own
/// statements; this attribute only ensures that a store which cannot confine is refused at registration
/// instead of returning another tenant's rows at runtime. Applying it to a contract whose implementations
/// are not tenant-aware makes those registrations fail - which is the intent.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class TenantOwnedAttribute : Attribute;
