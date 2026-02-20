// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Extension methods for pipeline profile configuration.
/// </summary>
public static class DispatchBuilderPipelineExtensions
{
	/// <summary>
	/// Configures pipeline profiles using a fluent API.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <param name="configure"> Configuration action for pipeline profiles. </param>
	/// <returns> The builder for chaining. </returns>
	/// <exception cref="InvalidOperationException"></exception>
	public static IDispatchBuilder WithPipelineProfiles(
		this IDispatchBuilder builder,
		Action<IPipelineProfilesConfigurationBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		// Cast to concrete type to access the method
		if (builder is DispatchBuilder dispatchBuilder)
		{
			return dispatchBuilder.WithPipelineProfiles(configure);
		}

		throw new InvalidOperationException(
			ErrorMessages.DispatchBuilderDoesNotSupportPipelineProfilesConfiguration);
	}
}
