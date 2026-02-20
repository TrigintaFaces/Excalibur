// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Conformance.TransportProvider.Fixtures;

/// <summary>
///     Base class for transport-specific fixtures that provide an <see cref="ITransportTestHarness" />.
/// </summary>
public abstract class TransportConformanceFixtureBase : IAsyncLifetime
{
	private ITransportTestHarness? _harness;

	/// <summary>
	///     Gets the human readable transport name used for diagnostics.
	/// </summary>
	public abstract string TransportName { get; }

	/// <summary>
	///     Gets the active transport harness.
	/// </summary>
	public ITransportTestHarness Harness => _harness ?? throw new InvalidOperationException("Fixture not initialized");

	/// <inheritdoc />
	public async Task InitializeAsync()
	{
		_harness = CreateHarness() ?? throw new InvalidOperationException("Harness factory returned null");
		await _harness.InitializeAsync().ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task DisposeAsync()
	{
		if (_harness is not null)
		{
			await _harness.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	///     Clears any queued messages prior to executing a test.
	/// </summary>
	public Task PurgeAsync(CancellationToken cancellationToken = default) =>
		_harness?.PurgeAsync(cancellationToken).AsTask() ?? Task.CompletedTask;

	/// <summary>
	///     Creates the transport-specific harness implementation.
	/// </summary>
	protected virtual ITransportTestHarness CreateHarness() => new InMemoryTransportHarness();
}
