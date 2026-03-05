// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Default in-memory implementation of <see cref="IMessageFeatures" /> that avoids any ASP.NET dependencies.
/// </summary>
public sealed class DefaultMessageFeatures : IMessageFeatures
{
	private ConcurrentDictionary<Type, object>? _features;

	/// <inheritdoc />
	public TFeature? GetFeature<TFeature>()
		where TFeature : class
	{
		var features = _features;
		if (features is not null &&
			features.TryGetValue(typeof(TFeature), out var value))
		{
			return value as TFeature;
		}

		return null;
	}

	/// <inheritdoc />
	public void SetFeature<TFeature>(TFeature? instance)
		where TFeature : class
	{
		var featureType = typeof(TFeature);
		var features = _features;

		if (instance is null)
		{
			if (features is not null)
			{
				_ = features.TryRemove(featureType, out _);
			}

			return;
		}

		features ??= Interlocked.CompareExchange(ref _features, new ConcurrentDictionary<Type, object>(), comparand: null) ?? _features!;
		features[featureType] = instance;
	}
}
