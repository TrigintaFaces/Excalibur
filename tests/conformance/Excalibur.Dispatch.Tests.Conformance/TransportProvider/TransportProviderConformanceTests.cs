// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Tests.Conformance.TransportProvider;

/// <summary>
///     Base conformance test class for transport provider implementations.
/// </summary>
/// <remarks>
///     This abstract class provides a comprehensive test suite for validating that transport provider implementations conform to the
///     expected behavior and contracts defined by the ITransportProvider interface. Concrete test classes should inherit from this class
///     and implement the CreateProvider method to provide the specific transport provider under test.
/// </remarks>
public abstract class TransportProviderConformanceTests
{
	/// <summary>
	///     Gets the expected transport type for the provider under test.
	/// </summary>
	protected abstract string ExpectedTransportType { get; }

	/// <summary>
	///     Gets the expected capabilities for the provider under test.
	/// </summary>
	protected abstract TransportCapabilities ExpectedCapabilities { get; }

	/// <summary>
	///     Gets a value indicating whether the provider should be available in the test environment.
	/// </summary>
	protected virtual bool ExpectedIsAvailable => true;

	[Fact]
	public void NameShouldNotBeNullOrEmpty()
	{
		// Arrange
		var provider = CreateProvider();

		// Act & Assert
		provider.Name.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void TransportTypeShouldMatchExpectedValue()
	{
		// Arrange
		var provider = CreateProvider();

		// Act & Assert
		provider.TransportType.ShouldBe(ExpectedTransportType);
	}

	[Fact]
	public void VersionShouldNotBeNullOrEmpty()
	{
		// Arrange
		var provider = CreateProvider();

		// Act & Assert
		provider.Version.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void CapabilitiesShouldMatchExpectedValue()
	{
		// Arrange
		var provider = CreateProvider();

		// Act & Assert
		provider.Capabilities.ShouldBe(ExpectedCapabilities);
	}

	[Fact]
	public void IsAvailableShouldMatchExpectedValue()
	{
		// Arrange
		var provider = CreateProvider();

		// Act & Assert
		provider.IsAvailable.ShouldBe(ExpectedIsAvailable);
	}

	[Fact]
	public async Task ValidateAsyncShouldReturnSuccessForValidOptions()
	{
		// Arrange
		var provider = CreateProvider();
		var options = CreateTestOptions();

		// Act
		var result = await provider.ValidateAsync(options, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public async Task ValidateAsyncShouldHandleCancellation()
	{
		// Arrange
		var provider = CreateProvider();
		var options = CreateTestOptions();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(() => provider.ValidateAsync(options, cts.Token)).ConfigureAwait(false);
	}

	[Fact]
	public async Task CheckHealthAsyncShouldReturnHealthyWhenAvailable()
	{
		// Arrange
		var provider = CreateProvider();

		// Act
		var result = await provider.CheckHealthAsync(CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		if (ExpectedIsAvailable)
		{
			result.Status.ShouldBe(HealthCheckStatus.Healthy);
		}
		else
		{
			result.Status.ShouldBeOneOf(HealthCheckStatus.Degraded, HealthCheckStatus.Unhealthy);
		}
	}

	[Fact]
	public async Task CheckHealthAsyncShouldHandleCancellation()
	{
		// Arrange
		var provider = CreateProvider();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(() => provider.CheckHealthAsync(cts.Token)).ConfigureAwait(false);
	}

	[Fact]
	public async Task CreateTransportAdapterAsyncShouldSucceedWhenSupported()
	{
		// Arrange
		var provider = CreateProvider();
		var options = CreateTestOptions();

		// Act & Assert
		if (provider.Capabilities.HasFlag(TransportCapabilities.TransportAdapter))
		{
			var adapter = await provider.CreateTransportAdapterAsync("test-adapter", options, CancellationToken.None);
			_ = adapter.ShouldNotBeNull();
		}
		else
		{
			_ = await Should.ThrowAsync<InvalidOperationException>(() =>
				provider.CreateTransportAdapterAsync("test-adapter", options, CancellationToken.None)).ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task CreateMessageBusAdapterAsyncShouldSucceedWhenSupported()
	{
		// Arrange
		var provider = CreateProvider();
		var options = CreateTestOptions();

		// Act & Assert
		if (provider.Capabilities.HasFlag(TransportCapabilities.MessageBusAdapter))
		{
			var adapter = await provider.CreateMessageBusAdapterAsync("test-bus", options, CancellationToken.None);
			_ = adapter.ShouldNotBeNull();
		}
		else
		{
			_ = await Should.ThrowAsync<InvalidOperationException>(() =>
				provider.CreateMessageBusAdapterAsync("test-bus", options, CancellationToken.None)).ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task CreateTransportAdapterAsyncShouldHandleNullAdapterName()
	{
		// Arrange
		var provider = CreateProvider();
		var options = CreateTestOptions();

		// Act & Assert
		if (provider.Capabilities.HasFlag(TransportCapabilities.TransportAdapter))
		{
			_ = await Should.ThrowAsync<ArgumentException>(() => provider.CreateTransportAdapterAsync(null!, options, CancellationToken.None)).ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task CreateTransportAdapterAsyncShouldHandleEmptyAdapterName()
	{
		// Arrange
		var provider = CreateProvider();
		var options = CreateTestOptions();

		// Act & Assert
		if (provider.Capabilities.HasFlag(TransportCapabilities.TransportAdapter))
		{
			_ = await Should.ThrowAsync<ArgumentException>(() => provider.CreateTransportAdapterAsync("", options, CancellationToken.None)).ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task CreateTransportAdapterAsyncShouldHandleNullOptions()
	{
		// Arrange
		var provider = CreateProvider();

		// Act & Assert
		if (provider.Capabilities.HasFlag(TransportCapabilities.TransportAdapter))
		{
			_ = await Should.ThrowAsync<ArgumentNullException>(() =>
				provider.CreateTransportAdapterAsync("test-adapter", null!, CancellationToken.None)).ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task CreateMessageBusAdapterAsyncShouldHandleNullBusName()
	{
		// Arrange
		var provider = CreateProvider();
		var options = CreateTestOptions();

		// Act & Assert
		if (provider.Capabilities.HasFlag(TransportCapabilities.MessageBusAdapter))
		{
			_ = await Should.ThrowAsync<ArgumentException>(() => provider.CreateMessageBusAdapterAsync(null!, options, CancellationToken.None)).ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task CreateMessageBusAdapterAsyncShouldHandleEmptyBusName()
	{
		// Arrange
		var provider = CreateProvider();
		var options = CreateTestOptions();

		// Act & Assert
		if (provider.Capabilities.HasFlag(TransportCapabilities.MessageBusAdapter))
		{
			_ = await Should.ThrowAsync<ArgumentException>(() => provider.CreateMessageBusAdapterAsync("", options, CancellationToken.None)).ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task CreateMessageBusAdapterAsyncShouldHandleNullOptions()
	{
		// Arrange
		var provider = CreateProvider();

		// Act & Assert
		if (provider.Capabilities.HasFlag(TransportCapabilities.MessageBusAdapter))
		{
			_ = await Should.ThrowAsync<ArgumentNullException>(() =>
				provider.CreateMessageBusAdapterAsync("test-bus", null!, CancellationToken.None)).ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task CreateTransportAdapterAsyncShouldHandleCancellation()
	{
		// Arrange
		var provider = CreateProvider();
		var options = CreateTestOptions();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		if (provider.Capabilities.HasFlag(TransportCapabilities.TransportAdapter))
		{
			_ = await Should.ThrowAsync<OperationCanceledException>(() =>
				provider.CreateTransportAdapterAsync("test-adapter", options, cts.Token)).ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task CreateMessageBusAdapterAsyncShouldHandleCancellation()
	{
		// Arrange
		var provider = CreateProvider();
		var options = CreateTestOptions();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		if (provider.Capabilities.HasFlag(TransportCapabilities.MessageBusAdapter))
		{
			_ = await Should.ThrowAsync<OperationCanceledException>(() =>
				provider.CreateMessageBusAdapterAsync("test-bus", options, cts.Token)).ConfigureAwait(false);
		}
	}

	/// <summary>
	///     Creates an instance of the transport provider to be tested.
	/// </summary>
	/// <returns> The transport provider instance under test. </returns>
	protected abstract ITransportProvider CreateProvider();

	/// <summary>
	///     Creates test message bus options for provider validation and adapter creation.
	/// </summary>
	/// <returns> Valid message bus options for testing. </returns>
	protected abstract IMessageBusOptions CreateTestOptions();
}
