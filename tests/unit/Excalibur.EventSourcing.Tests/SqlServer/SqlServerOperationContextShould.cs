// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;

namespace Excalibur.EventSourcing.Tests.SqlServer;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing.SqlServer")]
[Trait("Pattern", "Idempotency")]
public sealed class SqlServerOperationContextShould
{
	[Fact]
	public async Task AllowOnlyOneAppendAcrossParallelChildren()
	{
		var context = CreateContext();
		var accepted = 0;
		await Task.WhenAll(Enumerable.Range(0, 64).Select(_ => Task.Run(() =>
		{
			try
			{
				context.EnterAppend();
				Interlocked.Increment(ref accepted);
			}
			catch (InvalidOperationException) { }
		}))).ConfigureAwait(false);
		accepted.ShouldBe(1);
		context.AppendAttempted.ShouldBeTrue();
	}

	[Fact]
	public void RejectDetachedWorkAfterScopeDisposal()
	{
		var context = CreateContext();
		context.Close();
		Should.Throw<ObjectDisposedException>(context.EnterAppend);
	}

	[Theory]
	[InlineData("other", "db", "dbo", "Events", "tenant")]
	[InlineData("server", "other", "dbo", "Events", "tenant")]
	[InlineData("server", "DB", "dbo", "Events", "tenant")]
	[InlineData("server", "db", "other", "Events", "tenant")]
	[InlineData("server", "db", "dbo", "Other", "tenant")]
	[InlineData("server", "db", "dbo", "Events", "other")]
	[InlineData("server", "db", "dbo", "Events", "TENANT")]
	public void RejectDestinationOrTenantChanges(string server, string database, string schema, string table, string tenant)
	{
		var context = CreateContext();
		using var connection = new SqlConnection($"Server={server};Database={database};Integrated Security=true");
		Should.Throw<InvalidOperationException>(() => context.ValidateDestination(connection, schema, table, tenant));
	}

	[Fact]
	public void AcceptTheExactDestination()
	{
		using var connection = new SqlConnection("Server=server;Database=db;Integrated Security=true");
		CreateContext().ValidateDestination(connection, "dbo", "Events", "tenant");
	}

	[Theory]
	[InlineData("dbo];DROP TABLE x;--", "Events")]
	[InlineData("dbo", "Events.Other")]
	[InlineData("dbo", "Events]")]
	public void RejectUnsafeIdentifiers(string schema, string table)
	{
		Should.Throw<ArgumentException>(() => new SqlServerOperationExecutor(
			() => throw new InvalidOperationException("Must not open a connection"), A.Fake<ITenantContext>(), schema, table));
	}

	[Theory]
	[InlineData("scope", "key", 0)]
	[InlineData("scope", "key", 31)]
	[InlineData("scope", "key", 33)]
	[InlineData("scope\0other", "key", 32)]
	[InlineData("scope", "key\0other", 32)]
	public async Task RejectMalformedIdentityBeforeDatabaseAccess(string scope, string key, int size)
	{
		var executor = new SqlServerOperationExecutor(
			() => throw new InvalidOperationException("Must not open a connection"), A.Fake<ITenantContext>());
		await Should.ThrowAsync<ArgumentException>(() => executor.ExecuteAsync(scope, key, new byte[size],
			_ => Task.CompletedTask, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task RejectNestedOperationsBeforeDatabaseAccess()
	{
		var executor = new SqlServerOperationExecutor(
			() => throw new InvalidOperationException("Must not open a connection"), A.Fake<ITenantContext>());
		SqlServerOperationContext.Current = CreateContext();
		try
		{
			var error = await Should.ThrowAsync<InvalidOperationException>(() => executor.ExecuteAsync("scope", "key", new byte[32],
				_ => Task.CompletedTask, CancellationToken.None)).ConfigureAwait(false);
			error.Message.ShouldContain("Nested");
		}
		finally { SqlServerOperationContext.Current = null; }
	}

	private static SqlServerOperationContext CreateContext() => new("server", "db", "dbo", "Events", "tenant", new byte[32], new byte[32]);

	[Theory]
	[InlineData("source", 31)]
	[InlineData("source\0other", 32)]
	public async Task RejectMalformedSourceBindingBeforeDatabaseAccess(string source, int size)
	{
		var executor = new SqlServerOperationExecutor(
			() => throw new InvalidOperationException("Must not open a connection"), A.Fake<ITenantContext>());
		await Should.ThrowAsync<ArgumentException>(() => executor.BindSourceAsync(source, new byte[size], CancellationToken.None)).ConfigureAwait(false);
	}
}
