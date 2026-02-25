// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Reflection;

using Excalibur.Caching.Diagnostics;
using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Logging;

namespace Excalibur.Caching.Projections;

/// <summary>
/// Invalidates projection cache entries based on messages and configured tag resolvers.
/// Supports both interface-based tag resolution and convention-based tag extraction.
/// </summary>
/// <param name="cache">The cache invalidation service for removing cache entries.</param>
/// <param name="services">The service provider for resolving tag resolver implementations.</param>
/// <param name="logger">The logger for recording invalidation operations.</param>
/// <remarks>
/// <para>
/// This implementation uses a priority-based resolution strategy:
/// </para>
/// <list type="number">
/// <item><description>If the message implements <see cref="IProjectionInvalidationTags"/>, its tags are used directly.</description></item>
/// <item><description>If a <see cref="IProjectionTagResolver{T}"/> is registered for the message type, it resolves the tags.</description></item>
/// <item><description>As a fallback, convention-based extraction is used for messages named *Updated or *Deleted.</description></item>
/// </list>
/// </remarks>
public sealed partial class ProjectionCacheInvalidator(
	ICacheInvalidationService cache,
	IServiceProvider services,
	ILogger<ProjectionCacheInvalidator> logger)
	: IProjectionCacheInvalidator
{
	/// <summary>
	/// Convention suffix for update event types (e.g., "OrderUpdated").
	/// </summary>
	internal const string UpdatedSuffix = "Updated";

	/// <summary>
	/// Convention suffix for deletion event types (e.g., "OrderDeleted").
	/// </summary>
	internal const string DeletedSuffix = "Deleted";

	/// <summary>
	/// Convention property name for message identity.
	/// </summary>
	internal const string MessageIdPropertyName = "MessageId";

	/// <summary>
	/// Convention property name for entity identity.
	/// </summary>
	internal const string EntityIdPropertyName = "EntityId";

	/// <summary>
	/// Method name on <see cref="IProjectionTagResolver{T}"/> used for reflective invocation.
	/// </summary>
	private const string GetTagsMethodName = "GetTags";

	private static readonly Meter CacheMeter = new(CachingTelemetryConstants.MeterName, CachingTelemetryConstants.Version);

	private static readonly Counter<long> InvalidationCounter =
		CacheMeter.CreateCounter<long>("caching.projection.invalidations", "invalidations", "Number of projection cache invalidation operations");

	private static readonly Counter<long> TagsInvalidatedCounter =
		CacheMeter.CreateCounter<long>("caching.projection.tags_invalidated", "tags", "Number of projection cache tags invalidated");

	private static readonly ConcurrentDictionary<Type, (Type ResolverType, MethodInfo? GetTagsMethod)> ResolverTypeCache = new();

	private static readonly ConcurrentDictionary<Type, PropertyInfo?> ConventionPropertyCache = new();

	/// <inheritdoc />
	[RequiresDynamicCode("Calls ExtractTags which uses MakeGenericType for resolver lookup")]
	public async ValueTask InvalidateCacheAsync(object message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var tags = ExtractTags(message);
		if (tags.Count > 0)
		{
			if (logger.IsEnabled(LogLevel.Information))
			{
				LogInvalidatingCacheTags(string.Join(", ", tags));
			}

			InvalidationCounter.Add(1);
			TagsInvalidatedCounter.Add(tags.Count);
			await cache.InvalidateTagsAsync(tags, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			LogNoInvalidationStrategyMatched(message.GetType().Name);
		}
	}

	/// <summary>
	/// Extracts cache tags from the message using various resolution strategies.
	/// </summary>
	/// <param name="message">The message to extract tags from.</param>
	/// <returns>A list of cache tags extracted from the message.</returns>
	[RequiresDynamicCode("Calls System.Type.MakeGenericType(params Type[])")]
	[SuppressMessage("Design", "MA0038:Make method static", Justification = "Method uses services field from primary constructor")]
	[UnconditionalSuppressMessage("Trimming", "IL2075:Type.MakeGenericType may break with trimming",
		Justification = "IProjectionTagResolver<T> is a well-known interface pattern with stable member names")]
	[UnconditionalSuppressMessage("Trimming", "IL2075:Type.GetMethod may break with trimming",
		Justification = "IProjectionTagResolver<T>.GetTags is a well-known method accessed for tag resolution")]
	[UnconditionalSuppressMessage("Trimming", "IL2075:Type.GetProperty may break with trimming",
		Justification = "Convention-based property lookup (MessageId, EntityId) is fallback mechanism with try-catch handling")]
	private List<string> ExtractTags(object message)
	{
		var type = message.GetType();

		// 1. Check IProjectionInvalidationTags
		if (message is IProjectionInvalidationTags explicitTags)
		{
			return [.. explicitTags.GetProjectionCacheTags().Where(t => !string.IsNullOrWhiteSpace(t))];
		}

		// 2. Check IProjectionTagResolver<T>
		var (resolverType, getTagsMethod) = ResolverTypeCache.GetOrAdd(type, static t =>
		{
			var rt = typeof(IProjectionTagResolver<>).MakeGenericType(t);
			return (rt, rt.GetMethod(GetTagsMethodName));
		});

		var resolver = services.GetService(resolverType);

		if (resolver != null && getTagsMethod != null)
		{
			try
			{
				var result = getTagsMethod.Invoke(resolver, [message]);
				if (result is IEnumerable<string> tags)
				{
					return [.. tags.Where(t => !string.IsNullOrWhiteSpace(t))];
				}
			}
			catch (Exception ex) when (ex is TargetException or TargetInvocationException or InvalidCastException)
			{
				LogResolverInvocationFailed(type.Name, ex.GetType().Name);
			}
		}
		else
		{
			LogNoTagResolverRegistered(type.Name);
		}

		// 3. Fallback: convention
		var fallbackTags = new List<string>();
		if (type.Name.EndsWith(UpdatedSuffix, StringComparison.Ordinal) || type.Name.EndsWith(DeletedSuffix, StringComparison.Ordinal))
		{
			var property = ConventionPropertyCache.GetOrAdd(type,
				static t => t.GetProperty(MessageIdPropertyName) ?? t.GetProperty(EntityIdPropertyName));

			if (property != null)
			{
				var id = property.GetValue(message)?.ToString();
				if (!string.IsNullOrEmpty(id))
				{
					fallbackTags.Add($"""
					                  {type.Name.Replace(UpdatedSuffix, string.Empty, StringComparison.Ordinal)
							.Replace(DeletedSuffix, string.Empty, StringComparison.Ordinal)}:{id}
					                  """);
				}
				else
				{
					LogConventionPropertyValueEmpty(type.Name, property.Name);
				}
			}
			else
			{
				LogConventionPropertyNotFound(type.Name);
			}
		}

		return fallbackTags;
	}

	// Source-generated logging methods
	[LoggerMessage(CachingEventId.InvalidatingProjectionCacheTags, LogLevel.Information,
		"Invalidating projection cache tags: {Tags}")]
	private partial void LogInvalidatingCacheTags(string tags);

	[LoggerMessage(CachingEventId.ResolverInvocationFailed, LogLevel.Warning,
		"Tag resolver invocation failed for message type {MessageType}: {ExceptionType}")]
	private partial void LogResolverInvocationFailed(string messageType, string exceptionType);

	[LoggerMessage(CachingEventId.NoInvalidationStrategyMatched, LogLevel.Warning,
		"No invalidation strategy matched for message type {MessageType}. " +
		"The message does not implement IProjectionInvalidationTags, has no registered IProjectionTagResolver<T>, " +
		"and did not match convention-based tag extraction. Cache entries will not be invalidated.")]
	private partial void LogNoInvalidationStrategyMatched(string messageType);

	[LoggerMessage(CachingEventId.NoTagResolverRegistered, LogLevel.Debug,
		"No IProjectionTagResolver<T> registered for message type {MessageType}. Falling back to convention-based extraction.")]
	private partial void LogNoTagResolverRegistered(string messageType);

	[LoggerMessage(CachingEventId.ConventionPropertyNotFound, LogLevel.Debug,
		"Convention-based tag extraction found no MessageId or EntityId property on message type {MessageType}.")]
	private partial void LogConventionPropertyNotFound(string messageType);

	[LoggerMessage(CachingEventId.ConventionPropertyValueEmpty, LogLevel.Debug,
		"Convention-based tag extraction found property {PropertyName} on message type {MessageType} but its value was null or empty.")]
	private partial void LogConventionPropertyValueEmpty(string messageType, string propertyName);
}
