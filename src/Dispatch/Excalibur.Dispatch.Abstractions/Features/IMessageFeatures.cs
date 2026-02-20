// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Provider-neutral feature collection for message metadata and extensions.
/// </summary>
public interface IMessageFeatures
{
	/// <summary>
	/// Gets a feature instance of type <typeparamref name="TFeature" /> if present; otherwise null.
	/// </summary>
	/// <typeparam name="TFeature"> Feature contract type. </typeparam>
	/// <returns> The feature instance or null if not present. </returns>
	TFeature? GetFeature<TFeature>()
		where TFeature : class;

	/// <summary>
	/// Sets a feature instance for type <typeparamref name="TFeature" />.
	/// </summary>
	/// <typeparam name="TFeature"> Feature contract type. </typeparam>
	/// <param name="instance"> Instance to associate; null removes the feature. </param>
	void SetFeature<TFeature>(TFeature? instance)
		where TFeature : class;
}
