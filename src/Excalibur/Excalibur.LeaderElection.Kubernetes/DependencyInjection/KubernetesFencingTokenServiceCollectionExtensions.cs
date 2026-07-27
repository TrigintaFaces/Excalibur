// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.Kubernetes;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the Kubernetes-backed fencing token provider.
/// </summary>
public static class KubernetesFencingTokenServiceCollectionExtensions
{
	/// <summary>
	/// Registers the Kubernetes-backed <see cref="IFencingTokenProvider"/> provider and the fencing token
	/// middleware.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Requires an <see cref="global::k8s.IKubernetes"/> to be registered (the same client used by
	/// <c>UseKubernetes(...)</c> leader election). The provider reads the native
	/// <c>Lease.spec.leaseTransitions</c> counter that the election advances on each transition. Uses
	/// <c>TryAdd</c> so a consumer-supplied provider takes precedence. Pair with <c>WithFencingTokens()</c>
	/// on the leader election builder; the startup prerequisite check then passes because a provider is
	/// registered.
	/// </remarks>
	public static IServiceCollection AddKubernetesFencingTokenProvider(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<IFencingTokenProvider, KubernetesFencingTokenProvider>();
		services.TryAddSingleton<FencingTokenMiddleware>();

		return services;
	}
}
