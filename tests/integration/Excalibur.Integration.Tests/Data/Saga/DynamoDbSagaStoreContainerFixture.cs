// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Testcontainers.LocalStack;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// LocalStack (DynamoDB) container fixture for the DynamoDb saga-store optimistic-concurrency
/// conformance suite.
/// </summary>
/// <remarks>
/// <para>
/// <b>This used to be a bespoke <c>IAsyncLifetime</c> that swallowed its own startup exception</b> and
/// set an <c>IsInitialized</c> flag nothing read. The consequence was not a graceful degradation: the
/// container never started, the flag was never checked, and the first property access threw
/// <c>"Could not find resource 'LocalStackContainer'. Please create the resource by calling
/// StartAsync"</c> — an error about the SYMPTOM, with the actual startup failure discarded inside the
/// empty catch. A full-shard run produced 19 of those, and the reason the container failed to start
/// was unrecoverable from the output.
/// </para>
/// <para>
/// <b>It also had no retry, which is what actually bit.</b> The same suite passed 38 arms in one shard
/// and failed 19 in another against the identical tree, while every other LocalStack-backed suite in
/// the run passed. That is container-startup contention, not a product defect — and it is exactly
/// what <see cref="ContainerFixtureBase"/>'s attempt budget exists to absorb.
/// </para>
/// <para>
/// The base also owns the availability POLICY, which is hard failure rather than a skip: a
/// real-infrastructure test that passes by never executing certifies a guarantee nothing checked.
/// <see cref="ContainerFixtureBase.AllowGracefulDegradation"/> is left at its default of
/// <see langword="false"/> deliberately — the concurrency conformance this fixture backs is about what
/// the ENGINE does under a version-gated write, and there is nothing to learn from it without the
/// engine.
/// </para>
/// </remarks>
public sealed class DynamoDbSagaStoreContainerFixture : ContainerFixtureBase
{
	private LocalStackContainer? _container;

	/// <summary>Gets the LocalStack edge endpoint, which is the DynamoDB <c>ServiceUrl</c>.</summary>
	/// <value>The endpoint of the running container.</value>
	/// <remarks>
	/// Calls <see cref="ContainerFixtureBase.EnsureAvailable"/> first, so a caller reaching this
	/// property on a fixture that failed to start gets the RECORDED startup failure rather than a
	/// Testcontainers message about a resource that was never created.
	/// </remarks>
	public string ServiceUrl
	{
		get
		{
			EnsureAvailable();
			return _container!.GetConnectionString();
		}
	}

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new LocalStackBuilder()
			.WithImage("localstack/localstack:4")
			.WithName($"localstack-saga-dynamodb-{Guid.NewGuid():N}")
			.WithEnvironment("SERVICES", "dynamodb")
			.WithCleanUp(true)
			.Build();

		// Deliberately NOT wrapped in a try/catch. The base class owns retry, the total budget, and
		// recording the failure; swallowing here is what made the original undiagnosable.
		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		if (_container is not null)
		{
			await _container.DisposeAsync().ConfigureAwait(false);
		}
	}
}
