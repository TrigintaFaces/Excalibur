// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing.SqlServer.Requests;

using OracleSaveSnapshotRequest = Excalibur.EventSourcing.Oracle.Requests.SaveSnapshotRequest;
using PostgresSaveSnapshotRequest = Excalibur.EventSourcing.Postgres.Requests.SaveSnapshotRequest;

namespace Excalibur.EventSourcing.Tests.SqlServer.Requests;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class SaveSnapshotRequestShould
{
	[Fact]
	public void CreateSuccessfully()
	{
		// Arrange
		var snapshot = A.Fake<ISnapshot>();
		A.CallTo(() => snapshot.SnapshotId).Returns("snap-1");
		A.CallTo(() => snapshot.AggregateId).Returns("agg-1");
		A.CallTo(() => snapshot.AggregateType).Returns("Order");
		A.CallTo(() => snapshot.Version).Returns(5L);
		A.CallTo(() => snapshot.Data).Returns(new byte[] { 1, 2, 3 });
		A.CallTo(() => snapshot.CreatedAt).Returns(DateTimeOffset.UtcNow);

		// Act
		var sut = new SaveSnapshotRequest(snapshot, TenantScope.Untenanted, CancellationToken.None);

		// Assert
		sut.ShouldNotBeNull();
		sut.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void ThrowWhenSnapshotIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new SaveSnapshotRequest(null!, TenantScope.Untenanted, CancellationToken.None));
	}

	[Fact]
	public void ExposeResolveAsync()
	{
		// Arrange
		var snapshot = A.Fake<ISnapshot>();
		A.CallTo(() => snapshot.Data).Returns(ReadOnlyMemory<byte>.Empty);

		// Act
		var sut = new SaveSnapshotRequest(snapshot, TenantScope.Untenanted, CancellationToken.None);

		// Assert
		sut.ResolveAsync.ShouldNotBeNull();
	}

	// --- Tenant-scope-required lock (k28ac1) -------------------------------------------------
	// SaveSnapshotRequest is a tenant-KEYED store: the tenant term is part of the unique upsert key,
	// so an unscoped save (TenantScope.Untenanted emitted by omission) is unsafe. The `scope` parameter MUST
	// be required so the unsafe omission is a COMPILE error, not a runtime default — enforce the
	// invariant structurally. These locks bind that requirement across ALL three providers.

	public static TheoryData<Type> SnapshotRequestTypes => new()
	{
		typeof(SaveSnapshotRequest),          // SqlServer
		typeof(PostgresSaveSnapshotRequest),
		typeof(OracleSaveSnapshotRequest),
	};

	[Theory]
	[MemberData(nameof(SnapshotRequestTypes))]
	public void RequireAnExplicitTenantScope(Type requestType)
	{
		// SAFETY: the `scope` ctor parameter must not be optional — omitting the tenant scope must be
		// inexpressible (a compile error), so no caller can silently take the unsafe TenantScope.Untenanted path.
		var ctor = requestType.GetConstructors(BindingFlags.Public | BindingFlags.Instance).ShouldHaveSingleItem();

		var scopeParam = ctor.GetParameters().SingleOrDefault(p => p.ParameterType == typeof(TenantScope));

		scopeParam.ShouldNotBeNull($"{requestType.FullName} must take a TenantScope parameter.");
		scopeParam.IsOptional.ShouldBeFalse(
			$"{requestType.FullName}.scope must be REQUIRED (no default) so an unscoped snapshot save is inexpressible.");
	}

	[Fact]
	public void StillBuildWhenAnExplicitScopeIsSupplied()
	{
		// LIVENESS: supplying an explicit scope must still produce a working request — the fix removes the
		// unsafe default, it does not break the permitted (scoped / untenanted) construction.
		var snapshot = A.Fake<ISnapshot>();
		A.CallTo(() => snapshot.SnapshotId).Returns("snap-1");
		A.CallTo(() => snapshot.AggregateId).Returns("agg-1");
		A.CallTo(() => snapshot.AggregateType).Returns("Order");
		A.CallTo(() => snapshot.Version).Returns(5L);
		A.CallTo(() => snapshot.Data).Returns(ReadOnlyMemory<byte>.Empty);
		A.CallTo(() => snapshot.CreatedAt).Returns(DateTimeOffset.UtcNow);

		var untenanted = new SaveSnapshotRequest(snapshot, TenantScope.Untenanted, CancellationToken.None);
		var scoped = new SaveSnapshotRequest(snapshot, TenantScope.Scoped("tenant-1"), CancellationToken.None);

		untenanted.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
		scoped.Command.CommandText.ShouldNotBeNullOrWhiteSpace();
	}
}
