// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Attribute to mark dispatch actions for result caching with configurable options. Applied at the class level to enable caching for the
/// entire message handler result.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The handler must be free of observable side effects.</strong> A cached action's handler is run as
/// the value factory of <c>HybridCache</c>, and a value factory is not invoked once per dispatch. It runs
/// only on a miss, it is shared between concurrent callers of the same key so several dispatches produce one
/// invocation, and it can also run on a background task that no dispatch is awaiting. The number of handler
/// invocations is therefore unrelated to the number of dispatches in both directions, and no configuration
/// changes that.
/// </para>
/// <para>
/// Read it as: the handler answers "what is the value for this key", nothing more. Anything that must happen
/// once per request -- writes, sends, counters, audit entries -- belongs outside a cached handler, because
/// it will be skipped on a hit and may be repeated on a background refresh.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
public sealed class CacheResultAttribute : Attribute
{
	/// <summary>
	/// Gets or sets the cache expiration time in seconds. When set to 0 (default),
	/// uses the <see cref="CacheBehaviorOptions.DefaultExpiration"/> value configured in options.
	/// </summary>
	/// <value>The cache expiration time in seconds, or 0 to use the configured default.</value>
	public int ExpirationSeconds { get; set; }

	/// <summary>
	/// Gets or sets the cache tags for this result. Used for bulk cache invalidation scenarios.
	/// </summary>
	/// <value>The cache tags for this result.</value>
	public string[] Tags { get; set; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether to cache only successful results. When true, only successful message results are cached.
	/// Default is true.
	/// </summary>
	/// <value><see langword="true"/> if only successful results should be cached; otherwise, <see langword="false"/>.</value>
	public bool OnlyIfSuccess { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to ignore null results when caching. When true, null results are not cached. Default is true.
	/// </summary>
	/// <value><see langword="true"/> if null results should be ignored when caching; otherwise, <see langword="false"/>.</value>
	public bool IgnoreNullResult { get; set; } = true;
}
