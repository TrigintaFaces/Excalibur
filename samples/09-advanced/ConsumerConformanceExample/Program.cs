// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// ---------------------------------------------------------------------------------------------------
// Consumer Testing Toolkit — worked example.
//
// Demonstrates the two things a consumer of Excalibur does with the testing toolkit:
//   (a) run a framework CONFORMANCE KIT against their OWN implementation of a contract, and
//   (b) spin up a real backend with an opt-in TestContainers FIXTURE to run a kit against it.
//
// Everything here uses ONLY supported public APIs from the shipped packages:
//   • Excalibur.Testing.Conformance  — the abstract *ConformanceTestKit base classes
//   • Excalibur.Testing.Containers    — opt-in TestContainers fixtures (SqlServerContainerFixture, …)
//
// Run it: `dotnet run` (part (a) needs no Docker). Pass `--with-container` to also run part (b),
// which requires a running Docker daemon.
// ---------------------------------------------------------------------------------------------------

using Excalibur.Data.Resilience;
using Excalibur.Testing;
using Excalibur.Testing.Conformance;
using Excalibur.Testing.Containers;

var runContainerDemo = args.Contains("--with-container", StringComparer.Ordinal);

Console.WriteLine("== (a) Running RetryPolicyConformanceTestKit against a custom IDataRequestRetryPolicy ==");
var failures = ConformanceRunner.RunRetryPolicyConformance();
Console.WriteLine(failures == 0
	? "All retry-policy conformance checks PASSED — the custom adapter honors the contract.\n"
	: $"{failures} conformance check(s) FAILED.\n");

if (runContainerDemo)
{
	Console.WriteLine("== (b) Spinning up a SQL Server container fixture ==");
	await ConformanceRunner.RunContainerFixtureDemoAsync().ConfigureAwait(false);
}
else
{
	Console.WriteLine("(b) Container-fixture demo skipped. Re-run with `--with-container` (requires Docker) "
		+ "to start a real SQL Server via SqlServerContainerFixture and obtain a live connection string.");
}

return failures == 0 ? 0 : 1;

/// <summary>Drives the toolkit and reports pass/fail without depending on any test framework.</summary>
internal static class ConformanceRunner
{
	/// <summary>
	/// (a) Runs every conformance check the kit exposes against a consumer-supplied policy and returns the
	/// failure count. A consumer would normally do this from a <c>[Fact]</c>; here we invoke the public
	/// virtual methods directly so the sample is a plain runnable program.
	/// </summary>
	public static int RunRetryPolicyConformance()
	{
		var kit = new SampleRetryPolicyConformance();
		var checks = new (string Name, Action Run)[]
		{
			(nameof(kit.Policy_ShouldImplementIDataRequestRetryPolicy), kit.Policy_ShouldImplementIDataRequestRetryPolicy),
			(nameof(kit.MaxRetryAttempts_ShouldMatchConfiguredValue), kit.MaxRetryAttempts_ShouldMatchConfiguredValue),
			(nameof(kit.MaxRetryAttempts_ShouldBeNonNegative), kit.MaxRetryAttempts_ShouldBeNonNegative),
			(nameof(kit.BaseRetryDelay_ShouldBeNonNegative), kit.BaseRetryDelay_ShouldBeNonNegative),
			(nameof(kit.ShouldRetry_WithRetryableException_ReturnsExpectedResult), kit.ShouldRetry_WithRetryableException_ReturnsExpectedResult),
			(nameof(kit.ShouldRetry_WithNonRetryableException_ReturnsFalse), kit.ShouldRetry_WithNonRetryableException_ReturnsFalse),
			(nameof(kit.BaseRetryDelay_ForNonNullPolicy_ShouldBePositive), kit.BaseRetryDelay_ForNonNullPolicy_ShouldBePositive),
		};

		var failures = 0;
		foreach (var (name, run) in checks)
		{
			try
			{
				run();
				Console.WriteLine($"  [PASS] {name}");
			}
			catch (TestFixtureAssertionException ex)
			{
				failures++;
				Console.WriteLine($"  [FAIL] {name}: {ex.Message}");
			}
		}

		return failures;
	}

	/// <summary>
	/// (b) Shows how a consumer starts a real backend with an opt-in fixture. The same
	/// <see cref="SqlServerContainerFixture"/> would be handed to a store conformance kit
	/// (for example an <c>OutboxStoreConformanceTestKit</c>) via its connection string.
	/// </summary>
	public static async Task RunContainerFixtureDemoAsync()
	{
		var fixture = new SqlServerContainerFixture();
		await fixture.InitializeAsync().ConfigureAwait(false);
		try
		{
			Console.WriteLine($"  SQL Server ready (Docker available: {fixture.DockerAvailable}).");
			Console.WriteLine($"  Connection string: {fixture.ConnectionString}");
			Console.WriteLine("  A consumer would now: new MyOutboxStore(fixture.ConnectionString) and run "
				+ "OutboxStoreConformanceTestKit against it.");
		}
		finally
		{
			await fixture.DisposeAsync().ConfigureAwait(false);
		}
	}
}

/// <summary>
/// The consumer's own retry policy — the implementation under test. In a real project this would be your
/// production <see cref="IDataRequestRetryPolicy"/>; the conformance kit proves it honors the contract.
/// </summary>
internal sealed class SampleRetryPolicy(int maxRetryAttempts) : IDataRequestRetryPolicy
{
	public int MaxRetryAttempts { get; } = maxRetryAttempts;

	public TimeSpan BaseRetryDelay { get; } = TimeSpan.FromMilliseconds(100);

	// Retry transient timeouts; never retry argument/validation errors.
	public bool ShouldRetry(Exception exception) => exception is TimeoutException;
}

/// <summary>
/// Binds the consumer's <see cref="SampleRetryPolicy"/> to the framework conformance kit by implementing
/// its three factory hooks. That is the entire integration surface a consumer writes.
/// </summary>
internal sealed class SampleRetryPolicyConformance : RetryPolicyConformanceTestKit
{
	protected override IDataRequestRetryPolicy CreatePolicy(int maxRetryAttempts) =>
		new SampleRetryPolicy(maxRetryAttempts);

	protected override Exception CreateRetryableException() => new TimeoutException("transient");

	protected override Exception CreateNonRetryableException() => new ArgumentException("permanent");
}
