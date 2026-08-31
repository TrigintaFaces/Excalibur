// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Google.Cloud.Spanner.Data;

using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Data.Spanner.Tests;

/// <summary>
/// Locks for <see cref="SpannerConnectionProvider"/> that hold without a Spanner server.
/// </summary>
/// <remarks>
/// The retry semantics of <c>ExecuteInRetryableTransactionAsync</c> are deliberately absent here. Replay is
/// welded to connection creation inside that method — there is no seam a test can substitute — so the only
/// honest way to exercise it is against a real Spanner or its emulator. Asserting it against a fake would
/// mean re-implementing the very control flow under test and proving nothing.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SpannerConnectionProviderShould
{
	private const string EmulatorHostVariable = "SPANNER_EMULATOR_HOST";

	private static SpannerConnectionProvider Create(Action<SpannerOptions>? configure = null)
	{
		var options = new SpannerOptions
		{
			ProjectId = "excalibur-project",
			InstanceId = "excalibur-instance",
			DatabaseId = "excalibur-database",
		};

		configure?.Invoke(options);

		return new SpannerConnectionProvider(Options.Create(options));
	}

	[Fact]
	public void BuildItsConnectionStringFromTheConfiguredDatabasePath()
	{
		var provider = Create();

		using var connection = provider.CreateConnection();

		connection.ConnectionString.ShouldContain(
			"projects/excalibur-project/instances/excalibur-instance/databases/excalibur-database");
	}

	/// <summary>
	/// Each call hands back a distinct connection. The interface documents that the caller owns the
	/// lifetime and must dispose it, which is only safe if callers are not sharing one instance — a
	/// disposed shared connection would take every other caller down with it.
	/// </summary>
	[Fact]
	public void ReturnADistinctConnectionPerCall()
	{
		var provider = Create();

		using var first = provider.CreateConnection();
		using var second = provider.CreateConnection();

		first.ShouldNotBeSameAs(second);
		first.ConnectionString.ShouldBe(second.ConnectionString);
	}

	/// <summary>
	/// A provider targeting production must leave the process environment alone. The emulator is selected by
	/// a process-wide variable, so an unconditional write here — even of an empty value — would silently
	/// redirect every Spanner client in the host away from the real service.
	/// </summary>
	/// <remarks>
	/// The sentinel is not decoration. Reading the variable's incumbent value and asserting it is unchanged
	/// looks equivalent and is not: the provider writes this variable process-wide, so a sibling test that
	/// had already written it would make the "before" and "after" reads agree on the polluted value and the
	/// arm would pass against a provider that writes unconditionally. Planting a known value makes the
	/// assertion independent of what else has run.
	/// </remarks>
	[Fact]
	public void LeaveTheEmulatorEnvironmentUntouched_WhenNoEmulatorHostIsConfigured()
	{
		var original = Environment.GetEnvironmentVariable(EmulatorHostVariable);
		const string Sentinel = "excalibur-sentinel:0";

		try
		{
			Environment.SetEnvironmentVariable(EmulatorHostVariable, Sentinel);

			_ = Create();

			Environment.GetEnvironmentVariable(EmulatorHostVariable).ShouldBe(Sentinel);
		}
		finally
		{
			Environment.SetEnvironmentVariable(EmulatorHostVariable, original);
		}
	}

	[Fact]
	public void Reject_ANullOptionsAccessor()
		=> Should.Throw<ArgumentNullException>(() => new SpannerConnectionProvider(null!));

	[Fact]
	public async Task Reject_ANullOperation()
	{
		var provider = Create();

		_ = await Should.ThrowAsync<ArgumentNullException>(
			async () => await provider.ExecuteInRetryableTransactionAsync<int>(
				operation: null!,
				TestContext.Current.CancellationToken));
	}

	/// <summary>
	/// Cancellation is observed before a connection is opened. This matters on the retry path: an operation
	/// cancelled while a replay is pending must not open one more connection to a database it is no longer
	/// going to use, and on a shutdown that is the difference between a clean stop and a hung one.
	/// </summary>
	[Fact]
	public async Task HonorAnAlreadyCancelledToken_BeforeOpeningAnyConnection()
	{
		var provider = Create();
		using var cancellation = new CancellationTokenSource();
		await cancellation.CancelAsync();

		var operationRan = false;

		_ = await Should.ThrowAsync<OperationCanceledException>(
			async () => await provider.ExecuteInRetryableTransactionAsync(
				(_, _) =>
				{
					operationRan = true;
					return Task.FromResult(0);
				},
				cancellation.Token));

		operationRan.ShouldBeFalse();
	}
}
