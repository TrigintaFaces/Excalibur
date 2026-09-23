// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Text;

using Excalibur.Dispatch.Patterns.ClaimCheck;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.CloudNativePatterns.Examples.ClaimCheck;

/// <summary>
/// Runs the multi-tenant claim check configuration (<see cref="ConfigurationExamples.ConfigureMultiTenant"/>)
/// and checks the property it exists for: a payload stored under one tenant is retrievable under that tenant
/// and under no other. Throws if the property does not hold.
/// </summary>
public static class MultiTenantClaimCheckDemo
{
	/// <summary>
	/// Stores a payload as TenantA, retrieves it as TenantA, then shows that TenantB and an operation with no
	/// tenant cannot reach it.
	/// </summary>
	/// <param name="services">The host's root service provider.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task that completes when every check has passed.</returns>
	public static async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(services);

		var payload = Encoding.UTF8.GetBytes("TenantA invoice batch");
		ClaimCheckReference reference;

		// A host opens the tenant scope from whatever its inbound pipeline resolved (a header, a token claim);
		// services resolved in a DI scope inside it see that tenant through ITenantContext.
		using (TenantContextHolder.BeginScope("TenantA"))
		{
			await using var scope = services.CreateAsyncScope();
			var store = scope.ServiceProvider.GetRequiredService<IClaimCheckProvider>();

			reference = await store.StoreAsync(payload, cancellationToken).ConfigureAwait(false);
			var retrieved = await store.RetrieveAsync(reference, cancellationToken).ConfigureAwait(false);
			if (!retrieved.AsSpan().SequenceEqual(payload))
			{
				throw new InvalidOperationException("TenantA did not get its own payload back.");
			}

			Console.WriteLine($"TenantA stored and retrieved claim check {reference.Id}.");
		}

		using (TenantContextHolder.BeginScope("TenantB"))
		{
			await using var scope = services.CreateAsyncScope();
			var store = scope.ServiceProvider.GetRequiredService<IClaimCheckProvider>();

			var visible = true;
			try
			{
				_ = await store.RetrieveAsync(reference, cancellationToken).ConfigureAwait(false);
			}
			catch (KeyNotFoundException)
			{
				visible = false;
			}

			if (visible)
			{
				throw new InvalidOperationException("TenantB retrieved a payload stored by TenantA.");
			}

			Console.WriteLine("TenantB cannot retrieve TenantA's claim check.");
		}

		// No tenant scope: the resolver refuses instead of falling back to some store.
		await using (var scope = services.CreateAsyncScope())
		{
			var refused = false;
			try
			{
				_ = scope.ServiceProvider.GetRequiredService<IClaimCheckProvider>();
			}
			catch (InvalidOperationException)
			{
				refused = true;
			}

			if (!refused)
			{
				throw new InvalidOperationException("A claim check store was resolved with no tenant established.");
			}

			Console.WriteLine("An operation with no tenant is refused a claim check store.");
		}

		using (TenantContextHolder.BeginScope("TenantA"))
		{
			await using var scope = services.CreateAsyncScope();
			_ = await scope.ServiceProvider.GetRequiredService<IClaimCheckProvider>()
				.DeleteAsync(reference, cancellationToken).ConfigureAwait(false);
		}
	}
}
