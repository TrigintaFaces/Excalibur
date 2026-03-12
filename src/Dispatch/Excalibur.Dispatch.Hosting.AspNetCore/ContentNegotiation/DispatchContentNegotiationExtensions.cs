// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Hosting.AspNetCore.ContentNegotiation;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for adding Dispatch content negotiation formatters to ASP.NET Core MVC.
/// </summary>
public static class DispatchContentNegotiationExtensions
{
	/// <summary>
	/// Adds Dispatch content negotiation formatters (<see cref="DispatchInputFormatter"/> and
	/// <see cref="DispatchOutputFormatter"/>) to the MVC pipeline.
	/// </summary>
	/// <param name="builder">The MVC builder.</param>
	/// <returns>The MVC builder for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers Dispatch-backed input and output formatters that use the
	/// <see cref="ISerializerRegistry"/> to resolve serializers based on content type.
	/// Each registered <see cref="ISerializer"/> with a non-empty <see cref="ISerializer.ContentType"/>
	/// automatically becomes a supported media type.
	/// </para>
	/// <para>
	/// Requires the Dispatch serialization infrastructure to be configured first:
	/// <code>
	/// services.AddDispatchSerialization(serializers =>
	/// {
	///     serializers.AddSystemTextJson();
	///     serializers.AddMessagePack();
	/// });
	///
	/// services.AddControllers()
	///     .AddDispatchContentNegotiation();
	/// </code>
	/// </para>
	/// </remarks>
	public static IMvcBuilder AddDispatchContentNegotiation(this IMvcBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.AddMvcOptions(options =>
		{
			// Build a temporary service provider to resolve the registry
			// This is acceptable since formatters are configured once at startup
			var sp = builder.Services.BuildServiceProvider();
			var registry = sp.GetRequiredService<ISerializerRegistry>();

			options.InputFormatters.Insert(0, new DispatchInputFormatter(registry));
			options.OutputFormatters.Insert(0, new DispatchOutputFormatter(registry));
		});

		return builder;
	}
}
