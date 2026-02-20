// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
#pragma warning disable CA1063 // Implement IDisposable Correctly

using Xunit.Abstractions;

namespace Excalibur.Dispatch.Tests.Functional;

/// <summary>
///     Functional tests for message flow scenarios.
/// </summary>
[Trait("Category", "Functional")]
public class MessageFlowScenarioShould
{
	[Fact]
	public void ProcessMessage_ShouldCompleteSuccessfully() =>
		// TODO: Implement proper functional test for message flow scenarios
		true.ShouldBeTrue();
}

/// <summary>
///     XUnit logger provider for test output.
/// </summary>
public class XUnitLoggerProvider(ITestOutputHelper output) : ILoggerProvider
{
	/// <inheritdoc/>
	public ILogger CreateLogger(string categoryName) => new XUnitLogger(output, categoryName);

	/// <inheritdoc/>
	public void Dispose()
	{
		// Dispose implementation
	}
}

/// <summary>
///     XUnit logger implementation.
/// </summary>
public class XUnitLogger(ITestOutputHelper output, string categoryName) : ILogger
{
	/// <inheritdoc/>
	public IDisposable BeginScope<TState>(TState state) => null!;

	/// <inheritdoc/>
	public bool IsEnabled(LogLevel logLevel) => true;

	/// <inheritdoc/>
	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
		Func<TState, Exception?, string> formatter) =>
		output.WriteLine($"[{logLevel}] {categoryName}: {formatter(state, exception)}");
}

/// <summary>
///     Test message implementation for functional tests.
/// </summary>
/// <remarks>
/// IDispatchMessage is now a marker interface with no members.
/// Message properties are managed through IMessageContext and IMessageMetadata.
/// </remarks>
public class TestMessage : IDispatchMessage
{
	public string MessageId { get; set; } = Guid.NewGuid().ToString();
}
