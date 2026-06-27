// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Testcontainers.LocalStack;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// LocalStack (DynamoDB) container fixture for the DynamoDb saga-store optimistic-concurrency conformance
/// (e1tsq2, S853). Mirrors the event-store telemetry fixture's LocalStack setup; degrades gracefully
/// (<see cref="IsInitialized"/> = false) when the container can't start in a constrained CI environment.
/// </summary>
public sealed class DynamoDbSagaStoreContainerFixture : IAsyncLifetime
{
	private readonly LocalStackContainer _container;

	public DynamoDbSagaStoreContainerFixture()
	{
		_container = new LocalStackBuilder()
			.WithImage("localstack/localstack:latest")
			.WithName($"localstack-saga-dynamodb-{Guid.NewGuid():N}")
			.WithEnvironment("SERVICES", "dynamodb")
			.WithCleanUp(true)
			.Build();
	}

	/// <summary>Gets a value indicating whether the LocalStack container started.</summary>
	public bool IsInitialized { get; private set; }

	/// <summary>Gets the LocalStack edge endpoint (the DynamoDB <c>ServiceUrl</c>).</summary>
	public string ServiceUrl => _container.GetConnectionString();

	/// <inheritdoc/>
	public async ValueTask InitializeAsync()
	{
		try
		{
			await _container.StartAsync().ConfigureAwait(false);
			IsInitialized = true;
		}
		catch (Exception)
		{
			// Container may fail to start in constrained CI environments.
			IsInitialized = false;
		}
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		try
		{
			var disposeTask = _container.DisposeAsync().AsTask();
			var completed = await Task.WhenAny(disposeTask, Task.Delay(TimeSpan.FromSeconds(30))).ConfigureAwait(false);
			if (completed == disposeTask)
			{
				await disposeTask.ConfigureAwait(false);
			}
		}
		catch
		{
			// Best effort — allow the test host to exit cleanly.
		}
	}
}
