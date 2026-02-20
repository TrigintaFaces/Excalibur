// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics;

namespace Excalibur.Dispatch.Hosting.Serverless;

/// <summary>
/// Base implementation of serverless context with common functionality.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="ServerlessContextBase" /> class. </remarks>
/// <param name="platformContext"> The platform-specific context object. </param>
/// <param name="platform"> The serverless platform. </param>
/// <param name="logger"> The logger instance. </param>
public abstract class ServerlessContextBase(object platformContext, ServerlessPlatform platform, ILogger logger) : IServerlessContext, IServerlessPlatformDetails
{
	private readonly long _startTimestamp = Stopwatch.GetTimestamp();
	private volatile bool _disposed;

	/// <inheritdoc />
	public abstract string RequestId { get; }

	/// <inheritdoc />
	public abstract string FunctionName { get; }

	/// <inheritdoc />
	public abstract string FunctionVersion { get; }

	/// <inheritdoc />
	public abstract string InvokedFunctionArn { get; }

	/// <inheritdoc />
	public abstract int MemoryLimitInMB { get; }

	/// <inheritdoc />
	public abstract string LogGroupName { get; }

	/// <inheritdoc />
	public abstract string LogStreamName { get; }

	/// <inheritdoc />
	public ILogger Logger { get; } = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public abstract string CloudProvider { get; }

	/// <inheritdoc />
	public abstract string Region { get; }

	/// <inheritdoc />
	public abstract string AccountId { get; }

	/// <inheritdoc />
	public abstract DateTimeOffset ExecutionDeadline { get; }

	/// <inheritdoc />
	public TimeSpan ElapsedTime => Stopwatch.GetElapsedTime(_startTimestamp);

	/// <inheritdoc />
	public IDictionary<string, object> Properties { get; } = new ConcurrentDictionary<string, object>(StringComparer.Ordinal);

	/// <inheritdoc />
	public virtual TraceContext? TraceContext { get; set; }

	/// <inheritdoc />
	public ServerlessPlatform Platform { get; } = platform;

	/// <inheritdoc />
	public TimeSpan RemainingTime => ExecutionDeadline - DateTimeOffset.UtcNow;

	/// <inheritdoc />
	public object PlatformContext { get; } = platformContext ?? throw new ArgumentNullException(nameof(platformContext));

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		return serviceType.IsAssignableFrom(GetType()) ? this : null;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Disposes of managed and unmanaged resources.
	/// </summary>
	/// <param name="disposing"> True if disposing managed resources. </param>
	protected virtual void Dispose(bool disposing)
	{
		if (!_disposed)
		{
			if (disposing)
			{
				// Dispose managed resources
				if (Properties is IDisposable disposableProperties)
				{
					disposableProperties.Dispose();
				}
			}

			_disposed = true;
		}
	}

	/// <summary>
	/// Ensures the object has not been disposed.
	/// </summary>
	/// <exception cref="ObjectDisposedException">Thrown when the object has already been disposed.</exception>
	protected void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
