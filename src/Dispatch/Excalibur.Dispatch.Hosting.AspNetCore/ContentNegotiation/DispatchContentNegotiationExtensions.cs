// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Hosting.AspNetCore.ContentNegotiation;
using Excalibur.Dispatch.Serialization;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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
	/// Requires the Dispatch serialization infrastructure. Registration ORDER does not matter: the
	/// formatters are built when <c>MvcOptions</c> is first materialised, which is after every
	/// serializer registration has run, so a serializer added after this call is still picked up.
	/// <code>
	/// services.AddPluggableSerialization();
	/// services.AddPluggableSerializer(id: 1, new SystemTextJsonSerializer());
	///
	/// services.AddControllers()
	///     .AddDispatchContentNegotiation();
	/// </code>
	/// </para>
	/// </remarks>
	public static IMvcBuilder AddDispatchContentNegotiation(this IMvcBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Configure MvcOptions through the options system rather than building a second container.
		// The setup class takes ISerializerRegistry by constructor injection, so the formatters get the
		// APPLICATION's registry, resolved when MvcOptions is first materialised -- which is after every
		// serializer registration has run, whatever order the Add* calls were made in.
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Transient<IConfigureOptions<MvcOptions>, DispatchContentNegotiationMvcOptionsSetup>());

		return builder;
	}
}
