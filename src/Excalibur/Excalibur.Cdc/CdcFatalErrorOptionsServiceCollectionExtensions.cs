// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Excalibur.Cdc;

/// <summary>
/// Registers startup validation of <see cref="CdcFatalErrorOptions{TEvent}"/> for a CDC provider.
/// </summary>
internal static class CdcFatalErrorOptionsServiceCollectionExtensions
{
	/// <summary>
	/// Validates the provider's fatal-error options when the host starts, so an invalid limit or delay fails
	/// the deployment rather than the first reconnect.
	/// </summary>
	/// <typeparam name="TEvent">The provider-specific change-event type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The same service collection.</returns>
	public static IServiceCollection AddCdcFatalErrorOptionsValidation<TEvent>(this IServiceCollection services)
		where TEvent : class
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddOptions<CdcFatalErrorOptions<TEvent>>().ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<CdcFatalErrorOptions<TEvent>>, CdcFatalErrorOptionsValidator<TEvent>>());
		return services;
	}
}
