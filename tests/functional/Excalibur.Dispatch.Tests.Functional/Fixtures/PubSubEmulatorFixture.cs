// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Testcontainers.PubSub;

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Tests.Functional.Fixtures;

/// <summary>
///     Provides a Google Pub/Sub emulator container for integration tests.
/// </summary>
public sealed class PubSubEmulatorFixture : ContainerFixtureBase
{
	private PubSubContainer? _container;

	/// <summary>
	///     Gets the emulator endpoint.
	/// </summary>
	public string Endpoint => _container?.GetEmulatorEndpoint() ?? string.Empty;

	/// <inheritdoc />
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new PubSubBuilder().Build();
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
