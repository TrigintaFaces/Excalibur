// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Testcontainers.LocalStack;

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Tests.Functional.Fixtures;

/// <summary>
///     Provides a LocalStack container with SQS for integration tests.
/// </summary>
public sealed class AwsSqsContainerFixture : ContainerFixtureBase
{
	private LocalStackContainer? _container;

	/// <summary>
	///     Gets the LocalStack endpoint connection string.
	/// </summary>
	public string ConnectionString => _container?.GetConnectionString() ?? string.Empty;

	/// <inheritdoc />
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new LocalStackBuilder()
			.WithImage("localstack/localstack:latest")
			.WithEnvironment("SERVICES", "sqs")
			.WithPortBinding(4566, true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		if (_container is not null)
		{
			await _container.DisposeAsync().ConfigureAwait(false);
		}
	}
}
