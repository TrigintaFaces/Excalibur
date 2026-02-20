// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Context information for request interception.
/// </summary>
public sealed class InterceptionContext
{
	/// <summary>
	/// Initializes a new instance of the <see cref="InterceptionContext" /> class.
	/// </summary>
	public InterceptionContext()
	{
		Properties = new Dictionary<string, object?>(StringComparer.Ordinal);
		StartTime = DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Gets or sets the provider name.
	/// </summary>
	/// <value>The current <see cref="ProviderName"/> value.</value>
	public string? ProviderName { get; set; }

	/// <summary>
	/// Gets or sets the operation type.
	/// </summary>
	/// <value>The current <see cref="OperationType"/> value.</value>
	public string? OperationType { get; set; }

	/// <summary>
	/// Gets the start time of the operation.
	/// </summary>
	/// <value>The current <see cref="StartTime"/> value.</value>
	public DateTimeOffset StartTime { get; }

	/// <summary>
	/// Gets the elapsed time since the operation started.
	/// </summary>
	/// <value>The current <see cref="ElapsedTime"/> value.</value>
	public TimeSpan ElapsedTime => DateTimeOffset.UtcNow - StartTime;

	/// <summary>
	/// Gets or sets a value indicating whether the result should be cached.
	/// </summary>
	/// <value>The current <see cref="ShouldCache"/> value.</value>
	public bool ShouldCache { get; set; }

	/// <summary>
	/// Gets or sets the cache key if caching is enabled.
	/// </summary>
	/// <value>The current <see cref="CacheKey"/> value.</value>
	public string? CacheKey { get; set; }

	/// <summary>
	/// Gets or sets the cache duration if caching is enabled.
	/// </summary>
	/// <value>The current <see cref="CacheDuration"/> value.</value>
	public TimeSpan? CacheDuration { get; set; }

	/// <summary>
	/// Gets additional properties for the context.
	/// </summary>
	/// <value>The current <see cref="Properties"/> value.</value>
	public IDictionary<string, object?> Properties { get; }

	/// <summary>
	/// Gets or sets the correlation ID for distributed tracing.
	/// </summary>
	/// <value>The current <see cref="CorrelationId"/> value.</value>
	public string? CorrelationId { get; set; }

	/// <summary>
	/// Gets or sets the user identity.
	/// </summary>
	/// <value>The current <see cref="UserIdentity"/> value.</value>
	public string? UserIdentity { get; set; }
}
