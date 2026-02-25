// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Builds a composed <see cref="IPersistenceProvider"/> by stacking decorators around an inner provider.
/// Follows the <c>ChatClientBuilder</c> pattern from Microsoft.Extensions.AI.
/// </summary>
/// <remarks>
/// Decorators are applied in registration order: the first registered decorator is the outermost wrapper.
/// <code>
/// var provider = new PersistenceProviderBuilder(nativeProvider)
///     .Use(inner => new TelemetryPersistenceProvider(inner, meter, activitySource))
///     .Use(inner => new CachingPersistenceProvider(inner, cache))
///     .Build();
/// </code>
/// </remarks>
public sealed class PersistenceProviderBuilder
{
	private readonly IPersistenceProvider _innerProvider;
	private readonly List<Func<IPersistenceProvider, IPersistenceProvider>> _decorators = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="PersistenceProviderBuilder"/> class.
	/// </summary>
	/// <param name="innerProvider">The native persistence provider to decorate.</param>
	public PersistenceProviderBuilder(IPersistenceProvider innerProvider) =>
		_innerProvider = innerProvider ?? throw new ArgumentNullException(nameof(innerProvider));

	/// <summary>
	/// Adds a decorator to the provider pipeline.
	/// </summary>
	/// <param name="decorator">A factory that wraps the current provider with a decorator.</param>
	/// <returns>This builder for chaining.</returns>
	public PersistenceProviderBuilder Use(Func<IPersistenceProvider, IPersistenceProvider> decorator)
	{
		ArgumentNullException.ThrowIfNull(decorator);
		_decorators.Add(decorator);
		return this;
	}

	/// <summary>
	/// Builds the composed <see cref="IPersistenceProvider"/> by applying all registered decorators.
	/// </summary>
	/// <returns>The fully composed persistence provider.</returns>
	public IPersistenceProvider Build()
	{
		var provider = _innerProvider;
		foreach (var decorator in _decorators)
		{
			provider = decorator(provider);
		}

		return provider;
	}
}
