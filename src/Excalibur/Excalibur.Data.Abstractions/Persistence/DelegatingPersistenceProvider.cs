// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Base class for persistence provider decorators that delegate to an inner provider.
/// Follows the <c>DelegatingChatClient</c> pattern from Microsoft.Extensions.AI — all virtual
/// methods forward to the inner provider. Subclasses override specific methods to add
/// cross-cutting behavior (telemetry, caching, retry, circuit-breaking, etc.).
/// </summary>
/// <remarks>
/// <para>
/// Reference: <c>Microsoft.Extensions.AI.DelegatingChatClient</c> — decorator base
/// that wraps a single inner instance with all members virtual.
/// </para>
/// <para>
/// Use <see cref="PersistenceProviderBuilder"/> to compose multiple decorators
/// in a fluent pipeline.
/// </para>
/// </remarks>
public abstract class DelegatingPersistenceProvider : IPersistenceProvider
{
	/// <summary>
	/// Gets the inner provider that this decorator delegates to.
	/// </summary>
	/// <value>The inner <see cref="IPersistenceProvider"/>.</value>
	protected IPersistenceProvider InnerProvider { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="DelegatingPersistenceProvider"/> class.
	/// </summary>
	/// <param name="innerProvider">The inner provider to delegate to.</param>
	protected DelegatingPersistenceProvider(IPersistenceProvider innerProvider) =>
		InnerProvider = innerProvider ?? throw new ArgumentNullException(nameof(innerProvider));

	/// <inheritdoc />
	public virtual string Name => InnerProvider.Name;

	/// <inheritdoc />
	public virtual string ProviderType => InnerProvider.ProviderType;

	/// <inheritdoc />
	public virtual Task<TResult> ExecuteAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		CancellationToken cancellationToken)
		where TConnection : IDisposable =>
		InnerProvider.ExecuteAsync(request, cancellationToken);

	/// <inheritdoc />
	public virtual Task InitializeAsync(IPersistenceOptions options, CancellationToken cancellationToken) =>
		InnerProvider.InitializeAsync(options, cancellationToken);

	/// <inheritdoc />
	public virtual object? GetService(Type serviceType) =>
		InnerProvider.GetService(serviceType);

	/// <inheritdoc />
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Releases managed and/or unmanaged resources. Override in subclasses to release
	/// decorator-specific resources before delegating to the inner provider.
	/// </summary>
	/// <param name="disposing">
	/// <see langword="true"/> to release managed resources; <see langword="false"/> for unmanaged only.
	/// </param>
	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
		{
			InnerProvider.Dispose();
		}
	}

	/// <inheritdoc />
	public virtual ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);
		return InnerProvider.DisposeAsync();
	}
}
