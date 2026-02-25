// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.Functional;

/// <summary>
///     Compatibility namespace for TestContainers Wait API changes.
/// </summary>
public static class Wait
{
	/// <summary>
	///     Creates a wait strategy for Unix containers.
	/// </summary>
	public static object ForUnixContainer() => DotNet.Testcontainers.Builders.Wait.ForUnixContainer();

	/// <summary>
	///     Creates a wait strategy for Windows containers.
	/// </summary>
	public static object ForWindowsContainer() => DotNet.Testcontainers.Builders.Wait.ForWindowsContainer();
}

/// <summary>
///     Test event for functional tests.
/// </summary>
public class TestEvent : IDispatchMessage
{
	/// <summary>
	///     Gets or sets the event ID.
	/// </summary>
	public required string Id { get; set; } = Guid.NewGuid().ToString();

	/// <summary>
	///     Gets or sets the event data.
	/// </summary>
	public required string Data { get; set; } = string.Empty;

	/// <inheritdoc />
	public required string MessageId { get; set; } = Guid.NewGuid().ToString();

	/// <inheritdoc />
	public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

	/// <inheritdoc />
	public required IDictionary<string, object> Headers { get; set; } = new Dictionary<string, object>();

	/// <inheritdoc />
	public string MessageType => GetType().Name;
}

/// <summary>
///     Encrypted test event.
/// </summary>
public class EncryptedTestEvent : TestEvent
{
	/// <summary>
	///     Gets or sets a value indicating whether gets or sets whether the event is encrypted.
	/// </summary>
	public bool IsEncrypted { get; set; }
}

// Note: TestActivityListener and TestLogScope removed as they already exist in EndToEndObservabilityShould.cs
// Note: All duplicate Dispatch types removed - using actual implementations from source projects

// Additional compatibility for FunctionalTestBase
/// <summary>
///     Extension methods for FunctionalTestBase.
/// </summary>
public static class FunctionalTestBaseExtensions
{
	/// <summary>
	///     Creates a host for testing.
	/// </summary>
	public static IHost CreateHost(this FunctionalTestBase testBase, Action<IServiceCollection> configureServices = null)
	{
		var builder = Host.CreateDefaultBuilder()
			.ConfigureServices((context, services) => configureServices?.Invoke(services));

		return builder.Build();
	}
}
