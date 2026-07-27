// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0

using Xunit.v3;

namespace Excalibur.Dispatch.Tests.Conformance.Snapshot;

/// <summary>
/// Runs each decorated test inside an ambient tenant scope, so a conformance arm exercising a
/// tenant-aware store meets the store's precondition without every arm restating it.
/// </summary>
/// <remarks>
/// <para>
/// A store constructed with an <see cref="ITenantContext"/> has multi-tenancy active, and
/// <c>TenantScope.FromContext</c> then fails closed on any call resolving no tenant — by design.
/// Production satisfies that precondition by resolving a tenant in inbound middleware before the store
/// is ever reached; this attribute is the fixture's equivalent.
/// </para>
/// <para>
/// It is applied at the CLASS level so it covers arms added later. Wrapping each arm by hand works
/// equally well today and silently stops covering the next arm somebody writes, which is the failure
/// mode worth designing out — a new arm would fail with a tenant error unrelated to what it tests.
/// </para>
/// <para>
/// Why this works where fixture setup does not: <c>InitializeAsync</c> is <c>async</c>, so awaiting
/// captures and restores <c>ExecutionContext</c> and an <c>AsyncLocal</c> written inside it is unwound
/// before the arm runs — measured, 13 failed either way. <see cref="Before"/> is synchronous and
/// mutates the ambient context in place, so the value is still present when the arm executes.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class TenantScopedConformanceAttribute : BeforeAfterTestAttribute
{
	/// <summary>
	/// The tenant every decorated arm runs under unless it opens its own nested scope.
	/// </summary>
	public const string Tenant = "conformance-tenant";

	private static readonly AsyncLocal<IDisposable?> Scope = new();

	/// <inheritdoc />
	public override void Before(System.Reflection.MethodInfo methodUnderTest, IXunitTest test) =>
		Scope.Value = TenantContextHolder.BeginScope(Tenant);

	/// <inheritdoc />
	public override void After(System.Reflection.MethodInfo methodUnderTest, IXunitTest test)
	{
		Scope.Value?.Dispose();
		Scope.Value = null;
	}
}
