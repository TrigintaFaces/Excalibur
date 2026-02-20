// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Xunit.Abstractions;

namespace Tests.Shared;

/// <summary>
/// Test output sink that wraps xUnit's ITestOutputHelper for async disposal.
/// </summary>
public sealed class TestOutputSink : IAsyncDisposable, IDisposable
{
	private readonly ITestOutputHelper _output;
	private bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="TestOutputSink"/> class.
	/// </summary>
	/// <param name="output">The xUnit test output helper.</param>
	public TestOutputSink(ITestOutputHelper output)
	{
		_output = output ?? throw new ArgumentNullException(nameof(output));
	}

	/// <summary>
	/// Writes a message to the test output.
	/// </summary>
	/// <param name="message">The message to write.</param>
	public void Write(string message)
	{
		if (!_disposed)
		{
			_output.WriteLine(message);
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		_disposed = true;
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		_disposed = true;
		return ValueTask.CompletedTask;
	}
}
