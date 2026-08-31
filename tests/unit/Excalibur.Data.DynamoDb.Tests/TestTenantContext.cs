// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// A fixed ambient tenant context, standing in for the per-request context a host supplies to a store.
/// The stores under test take <see cref="ITenantContext"/> as a required constructor dependency, so a
/// test must state which partition it is running as rather than leaving the choice implicit.
/// </summary>
/// <remarks>
/// <para>
/// Two inhabitants, and they are not interchangeable. <see cref="SingleTenant"/> is the identity a
/// deployment without multi-tenancy actually operates as -- the same value <c>SingleTenantContext</c>
/// supplies in production -- and is the right choice for a test with no tenant notion at all.
/// <see cref="Untenanted"/> binds the reserved untenanted marker, and is the right choice only where the
/// untenanted partition is the subject of the test.
/// </para>
/// <para>
/// It takes its tenant at construction and has no null state: a context resolving no tenant fails closed
/// on first read, so a fixture that could produce one would turn a tenancy decision into an unrelated
/// throw somewhere downstream.
/// </para>
/// </remarks>
internal sealed class TestTenantContext : ITenantContext
{
	private TestTenantContext(string tenantId) => TenantId = tenantId;

	/// <summary>
	/// The single canonical tenant a host without multi-tenancy runs as.
	/// </summary>
	internal static ITenantContext SingleTenant { get; } = new TestTenantContext(TenantDefaults.DefaultTenantId);

	/// <summary>
	/// The reserved untenanted partition, for tests whose subject is the untenanted case itself.
	/// </summary>
	internal static ITenantContext Untenanted { get; } = new TestTenantContext(TenantScope.UntenantedSentinel);

	/// <inheritdoc />
	public string? TenantId { get; }

	/// <inheritdoc />
	public bool HasTenant => true;
}
