// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Abstract base class for <see cref="IPersistenceProvider"/> decorators.
/// All methods are virtual and forward to the inner provider by default.
/// </summary>
/// <remarks>
/// <para>
/// Follows the <c>DelegatingHandler</c> / <c>DelegatingChatClient</c> pattern from Microsoft.
/// Subclasses override only the methods they need to intercept (e.g., logging, metrics,
/// retry policies, multi-tenancy routing).
/// </para>
/// <para>
/// Example: a logging decorator that intercepts <see cref="ExecuteAsync{TConnection,TResult}"/>:
/// </para>
/// <code>
/// public class LoggingPersistenceProvider(IPersistenceProvider inner, ILogger logger)
///     : DelegatingPersistenceProvider(inner)
/// {
///     public override async Task&lt;TResult&gt; ExecuteAsync&lt;TConnection, TResult&gt;(
///         IDataRequest&lt;TConnection, TResult&gt; request, CancellationToken cancellationToken)
///     {
///         logger.LogDebug("Executing {Request}", request.GetType().Name);
///         return await base.ExecuteAsync(request, cancellationToken);
///     }
/// }
/// </code>
/// </remarks>
public abstract class DelegatingPersistenceProvider : IPersistenceProvider
{
	/// <summary>
	/// Gets the inner persistence provider being decorated.
	/// </summary>
	protected IPersistenceProvider Inner { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="DelegatingPersistenceProvider"/> class.
	/// </summary>
	/// <param name="inner">The inner persistence provider to delegate to.</param>
	protected DelegatingPersistenceProvider(IPersistenceProvider inner)
	{
		Inner = inner ?? throw new ArgumentNullException(nameof(inner));
	}

	/// <inheritdoc />
	public virtual string Name => Inner.Name;

	/// <inheritdoc />
	public virtual string ProviderType => Inner.ProviderType;

	/// <inheritdoc />
	public virtual Task<TResult> ExecuteAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		CancellationToken cancellationToken)
		where TConnection : IDisposable
		=> Inner.ExecuteAsync(request, cancellationToken);

	/// <inheritdoc />
	public virtual Task InitializeAsync(IPersistenceOptions options, CancellationToken cancellationToken)
		=> Inner.InitializeAsync(options, cancellationToken);

	/// <inheritdoc />
	public virtual object? GetService(Type serviceType)
		=> Inner.GetService(serviceType);

	/// <inheritdoc />
	public virtual async ValueTask DisposeAsync()
	{
		await Inner.DisposeAsync().ConfigureAwait(false);
		GC.SuppressFinalize(this);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Releases resources used by the <see cref="DelegatingPersistenceProvider"/>.
	/// </summary>
	/// <param name="disposing">
	/// <see langword="true"/> to release both managed and unmanaged resources;
	/// <see langword="false"/> to release only unmanaged resources.
	/// </param>
	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
		{
			Inner.Dispose();
		}
	}
}
