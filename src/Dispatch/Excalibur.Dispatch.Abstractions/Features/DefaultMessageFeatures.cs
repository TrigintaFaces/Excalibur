// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Default in-memory implementation of <see cref="IMessageFeatures" /> that avoids any ASP.NET dependencies.
/// </summary>
public sealed class DefaultMessageFeatures : IMessageFeatures
{
	private readonly ConcurrentDictionary<Type, object> _features = new();

	/// <inheritdoc />
	public TFeature? GetFeature<TFeature>()
		where TFeature : class
	{
		if (_features.TryGetValue(typeof(TFeature), out var value))
		{
			return value as TFeature;
		}

		return null;
	}

	/// <inheritdoc />
	public void SetFeature<TFeature>(TFeature? instance)
		where TFeature : class
	{
		if (instance is null)
		{
			_ = _features.TryRemove(typeof(TFeature), out _);
			return;
		}

		_features[typeof(TFeature)] = instance;
	}
}
