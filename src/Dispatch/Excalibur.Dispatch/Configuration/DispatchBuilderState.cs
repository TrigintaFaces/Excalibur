// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Options.Configuration;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// The configuration a dispatch composition accumulates, held by the service collection rather than by a
/// builder instance.
/// </summary>
/// <remarks>
/// Every builder over one service collection shares one of these. That is what makes the composition
/// continuable: the runtime state registered in the container reads these collections when the provider
/// resolves it, so configuration added later -- by a chained call, or by a second builder over the same
/// collection -- is visible to a pipeline that was already registered. Holding the same state in builder
/// fields instead meant a second builder accumulated into collections nothing resolved, and the
/// configuration was discarded with no error and no log.
/// </remarks>
internal sealed class DispatchBuilderState
{
	/// <summary> Gets the named pipeline configuration callbacks. </summary>
	public Dictionary<string, Action<IPipelineBuilder>> PipelineConfigurations { get; } = new(StringComparer.Ordinal);

	/// <summary> Gets the transport adapters registered by name. </summary>
	public Dictionary<string, ITransportAdapter> TransportAdapters { get; } = new(StringComparer.Ordinal);

	/// <summary> Gets the transport binding configuration callbacks. </summary>
	public List<Action<IBindingConfigurationBuilder>> BindingConfigurations { get; } = [];

	/// <summary> Gets the middleware types applied to every pipeline. </summary>
	public List<Type> GlobalMiddleware { get; } = [];

	/// <summary> Gets the options instance the composition mutates. </summary>
	public DispatchOptions Options { get; } = new();

	/// <summary> Gets the pipeline profile registry for the composition. </summary>
	public PipelineProfileRegistry ProfileRegistry { get; } = new();

	/// <summary> Gets the transport binding registry for the composition. </summary>
	public TransportBindingRegistry BindingRegistry { get; } = new();

	/// <summary> Gets or sets a value indicating whether handler registrations were configured. </summary>
	public bool HasHandlerRegistrations { get; set; }

	/// <summary> Gets or sets a value indicating whether <c>DispatchBuilder.Build()</c> has run for this composition. </summary>
	public bool IsBuilt { get; set; }

	/// <summary>
	/// Returns the state already attached to <paramref name="services"/>, attaching a new one if this is the
	/// first builder over that collection.
	/// </summary>
	/// <param name="services"> The service collection the composition is accumulating into. </param>
	/// <returns> The single state instance shared by every builder over that collection. </returns>
	public static DispatchBuilderState GetOrAdd(IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		var existing = services.FirstOrDefault(
			static d => d.ServiceType == typeof(DispatchBuilderState) && d.GetImplementationInstance() is not null);

		if (existing?.GetImplementationInstance() is DispatchBuilderState state)
		{
			return state;
		}

		var created = new DispatchBuilderState();
		services.AddSingleton(created);
		return created;
	}
}
