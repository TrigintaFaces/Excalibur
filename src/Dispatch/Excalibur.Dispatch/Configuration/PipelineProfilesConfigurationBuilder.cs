// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Default implementation of the pipeline profiles configuration builder.
/// </summary>
internal sealed class PipelineProfilesConfigurationBuilder : IPipelineProfilesConfigurationBuilder
{
	private readonly IPipelineProfileRegistry _registry;

	public PipelineProfilesConfigurationBuilder(IPipelineProfileRegistry registry)
	{
		ArgumentNullException.ThrowIfNull(registry);
		_registry = registry;
	}

	/// <inheritdoc/>
	public IPipelineProfilesConfigurationBuilder RegisterProfile(string name, Action<IPipelineProfileBuilder> configure)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(configure);

		var builder = new PipelineProfileBuilder(name, $"Profile: {name}");
		configure(builder);

		var profile = builder.Build();
		_registry.RegisterProfile(profile);

		return this;
	}

	/// <inheritdoc/>
	public IPipelineProfilesConfigurationBuilder SetDefaultProfile(string name, Action<IPipelineProfileBuilder> configure)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(configure);

		var builder = new PipelineProfileBuilder(name, $"Default profile: {name}");
		configure(builder);

		var profile = builder.Build();
		_registry.RegisterProfile(profile);
		_registry.SetDefaultProfile(name);

		return this;
	}
}
