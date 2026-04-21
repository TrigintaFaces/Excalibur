// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3;
using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Policy.Cedar;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring the Cedar authorization evaluator on <see cref="IA3Builder"/>.
/// </summary>
public static class A3BuilderCedarExtensions
{
	/// <summary>
	/// Registers a Cedar HTTP adapter as the <see cref="IAuthorizationEvaluator"/>.
	/// Supports both local Cedar agents and Amazon Verified Permissions (AVP).
	/// </summary>
	/// <param name="builder">The A3 builder.</param>
	/// <param name="configure">Action to configure <see cref="CedarOptions"/>.</param>
	/// <returns>The builder for chaining.</returns>
	public static IA3Builder UseCedarPolicy(
		this IA3Builder builder,
		Action<CedarOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		builder.Services.Configure(configure);
		builder.Services.AddOptionsWithValidateOnStart<CedarOptions>();
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<CedarOptions>, CedarOptionsValidator>());

		builder.Services.AddHttpClient<IAuthorizationEvaluator, CedarAuthorizationEvaluator>(
			(sp, client) =>
			{
				var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CedarOptions>>().Value;
				client.BaseAddress = new Uri(options.Endpoint);
				client.Timeout = TimeSpan.FromMilliseconds(options.TimeoutMs);
			});

		return builder;
	}
}
