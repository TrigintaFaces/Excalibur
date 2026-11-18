// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Hosting.AspNetCore;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration for capturing inbound HTTP request headers into a message context.
/// </summary>
public static class RequestHeaderCaptureServiceCollectionExtensions
{
	/// <summary>
	/// Registers request-header capture and validates it at startup.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure">
	/// Names the headers to capture and, optionally, adjusts the caps. Capturing nothing is the default,
	/// so a registration with no configuration changes no behaviour.
	/// </param>
	/// <returns> The same service collection, for chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="services"/> is null. </exception>
	/// <remarks>
	/// <para>
	/// Register through this method rather than configuring
	/// <see cref="RequestHeaderCaptureOptions"/> directly: it is what installs the validator that
	/// <strong>refuses a credential-bearing header at startup</strong>. Configuring the options by hand
	/// still captures headers, but a configuration naming <c>Authorization</c>, <c>Cookie</c> or
	/// <c>Proxy-Authorization</c> would then be accepted silently, which is the failure this validator
	/// exists to prevent.
	/// </para>
	/// <para>
	/// Validation runs at startup rather than per message, so the check costs one pass per process and
	/// nothing on the dispatch path.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddDispatchRequestHeaderCapture(
		this IServiceCollection services,
		Action<RequestHeaderCaptureOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var builder = services.AddOptions<RequestHeaderCaptureOptions>();

		if (configure is not null)
		{
			_ = builder.Configure(configure);
		}

		_ = builder.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<
				IValidateOptions<RequestHeaderCaptureOptions>,
				RequestHeaderCaptureOptionsValidator>());

		return services;
	}
}
