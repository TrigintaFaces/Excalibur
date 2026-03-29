// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3;
using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Policy.Opa;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring the OPA authorization evaluator on <see cref="IA3Builder"/>.
/// </summary>
public static class A3BuilderOpaExtensions
{
	/// <summary>
	/// Registers an Open Policy Agent (OPA) HTTP adapter as the <see cref="IAuthorizationEvaluator"/>.
	/// </summary>
	/// <param name="builder">The A3 builder.</param>
	/// <param name="configure">Action to configure <see cref="OpaOptions"/>.</param>
	/// <returns>The builder for chaining.</returns>
	public static IA3Builder UseOpaPolicy(
		this IA3Builder builder,
		Action<OpaOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		builder.Services.Configure(configure);
		builder.Services.AddOptionsWithValidateOnStart<OpaOptions>()
			.ValidateDataAnnotations();

		builder.Services.AddHttpClient<IAuthorizationEvaluator, OpaAuthorizationEvaluator>(
			(sp, client) =>
			{
				var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpaOptions>>().Value;
				client.BaseAddress = new Uri(options.Endpoint);
				client.Timeout = TimeSpan.FromMilliseconds(options.TimeoutMs);
			});

		return builder;
	}
}
