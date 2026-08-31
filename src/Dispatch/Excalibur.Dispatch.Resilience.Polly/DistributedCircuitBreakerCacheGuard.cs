// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Options marker whose <c>ValidateOnStart</c> registration triggers the distributed-circuit-breaker
/// store gate at host startup. Carries no configuration; it exists only to hang a startup validation off.
/// </summary>
internal sealed class DistributedCircuitBreakerCacheGuardOptions;

/// <summary>
/// Fails fast at host startup when a distributed circuit breaker is registered without a store that is
/// actually shared between instances.
/// </summary>
/// <remarks>
/// The breaker coordinates entirely through <see cref="IDistributedCache"/>. With no cache registered it
/// cannot be constructed at all; with the in-process <c>MemoryDistributedCache</c> it constructs happily
/// and every replica then trips independently — a per-instance breaker reached through a method that
/// promises coordination, failing silently because nothing throws and nothing degrades visibly. Refusing
/// the composition at boot, naming the remedy, is the only outcome that keeps the name honest.
/// </remarks>
internal sealed class DistributedCircuitBreakerCacheGuard(IServiceProvider serviceProvider)
	: IValidateOptions<DistributedCircuitBreakerCacheGuardOptions>
{
	// The in-process implementation is matched by name rather than by type so this package need not
	// reference the memory-cache assembly to recognise it. This mirrors how the caching package decides
	// whether a registered IDistributedCache is a real cross-instance backend.
	private const string InProcessCacheTypeName = "MemoryDistributedCache";

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, DistributedCircuitBreakerCacheGuardOptions options)
	{
		var cache = serviceProvider.GetService<IDistributedCache>();

		if (cache is null)
		{
			return ValidateOptionsResult.Fail(
				"AddDistributedCircuitBreaker requires an IDistributedCache that is shared across instances, "
				+ "and none is registered. Register a cross-instance backend before it — for example "
				+ "AddStackExchangeRedisCache(...) or AddDistributedSqlServerCache(...). A distributed circuit "
				+ "breaker with no shared store cannot coordinate anything.");
		}

		if (string.Equals(cache.GetType().Name, InProcessCacheTypeName, StringComparison.Ordinal))
		{
			return ValidateOptionsResult.Fail(
				"AddDistributedCircuitBreaker is registered against the in-process distributed cache "
				+ "(AddDistributedMemoryCache), which is not shared between instances. Every replica would "
				+ "trip its own circuit and none would ever observe another's, silently — the breaker would "
				+ "report as distributed while behaving per-instance. Register a cross-instance backend "
				+ "instead — for example AddStackExchangeRedisCache(...) or AddDistributedSqlServerCache(...) "
				+ "— or use AddPollyCircuitBreaker for a deliberately per-instance breaker.");
		}

		return ValidateOptionsResult.Success;
	}
}
