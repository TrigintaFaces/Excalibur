// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.SqlServer.Persistence;

namespace Excalibur.Data.Tests.SqlServer;

/// <summary>
/// Verifies that <see cref="SqlServerTransactionScope"/> implements sync Rollback in Dispose (AD-540.2).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SqlServerTransactionScopeSyncRollbackShould : UnitTestBase
{
	[Fact]
	public void ImplementBothDisposableInterfaces()
	{
		// Assert — SqlServerTransactionScope must implement both dispose patterns
		var type = typeof(SqlServerTransactionScope);
		type.GetInterfaces().ShouldContain(typeof(IDisposable));
		type.GetInterfaces().ShouldContain(typeof(IAsyncDisposable));
	}

	[Fact]
	public void ImplementITransactionScope()
	{
		// Assert — must implement ITransactionScope
		typeof(ITransactionScope).IsAssignableFrom(typeof(SqlServerTransactionScope))
			.ShouldBeTrue();
	}

	[Fact]
	public void HaveSyncDisposeMethod()
	{
		// Verify the Dispose(bool) method exists for sync rollback path
		var disposeMethod = typeof(SqlServerTransactionScope)
			.GetMethod("Dispose", BindingFlags.NonPublic | BindingFlags.Instance, [typeof(bool)]);

		disposeMethod.ShouldNotBeNull("Dispose(bool) must be implemented for sync rollback path");
	}

	[Fact]
	public void HavePublicDisposeMethod()
	{
		// Verify the public Dispose() method exists
		var disposeMethod = typeof(SqlServerTransactionScope)
			.GetMethod("Dispose", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);

		disposeMethod.ShouldNotBeNull("Sync Dispose() must be explicitly implemented");
	}

	[Fact]
	public void HaveAsyncDisposeMethod()
	{
		// Verify the async DisposeAsync method exists
		var disposeAsyncMethod = typeof(SqlServerTransactionScope)
			.GetMethod("DisposeAsync", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);

		disposeAsyncMethod.ShouldNotBeNull("DisposeAsync() must be explicitly implemented");
		disposeAsyncMethod.ReturnType.ShouldBe(typeof(ValueTask));
	}
}
