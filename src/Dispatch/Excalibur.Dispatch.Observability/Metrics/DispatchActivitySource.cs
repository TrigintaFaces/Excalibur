// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Observability.Metrics;

/// <summary>
/// Provides centralized activity source for Dispatch tracing operations.
/// </summary>
public static class DispatchActivitySource
{
	/// <summary>
	/// The activity source name for Dispatch tracing.
	/// </summary>
	public const string Name = "Excalibur.Dispatch";

	/// <summary>
	/// Maximum number of entries allowed in each type name cache.
	/// When the cap is reached, new lookups compute names without caching to prevent unbounded memory growth.
	/// </summary>
	private const int MaxCacheEntries = 1024;

	/// <summary>
	/// The activity source instance for Dispatch operations.
	/// Process-lifetime singleton — do not dispose.
	/// </summary>
	public static ActivitySource Instance { get; } = new(Name);

	/// <summary>
	/// Caches Type.Name strings to avoid repeated reflection per dispatch.
	/// </summary>
	private static readonly ConcurrentDictionary<Type, string> TypeNameCache = new();

	/// <summary>
	/// Caches middleware activity names ("middleware.{TypeName}") to avoid string interpolation per dispatch.
	/// </summary>
	private static readonly ConcurrentDictionary<Type, string> MiddlewareActivityNameCache = new();

	/// <summary>
	/// Gets the cached name for a type, avoiding repeated reflection.
	/// </summary>
	private static string GetCachedTypeName(Type type)
	{
		if (TypeNameCache.TryGetValue(type, out var cached))
		{
			return cached;
		}

		if (TypeNameCache.Count >= MaxCacheEntries)
		{
			return type.Name;
		}

		return TypeNameCache.GetOrAdd(type, static t => t.Name);
	}

	/// <summary>
	/// Gets the cached middleware activity name for a type.
	/// </summary>
	private static string GetCachedMiddlewareActivityName(Type type)
	{
		if (MiddlewareActivityNameCache.TryGetValue(type, out var cached))
		{
			return cached;
		}

		if (MiddlewareActivityNameCache.Count >= MaxCacheEntries)
		{
			return string.Concat("middleware.", type.Name);
		}

		return MiddlewareActivityNameCache.GetOrAdd(type, static t => string.Concat("middleware.", t.Name));
	}

	/// <summary>
	/// Starts a new activity for message processing.
	/// </summary>
	/// <param name="message"> The message being processed. </param>
	/// <param name="activityName"> The name of the activity. </param>
	/// <returns> The started activity, or null if no listener is interested. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="message" /> is null. </exception>
	public static Activity? StartActivity(IDispatchMessage message, string activityName)
	{
		ArgumentNullException.ThrowIfNull(message);

		var activity = Instance.StartActivity(activityName);

		if (activity != null)
		{
			_ = activity.SetTag("message.type", GetCachedTypeName(message.GetType()));
			_ = activity.SetTag("dispatch.operation", activityName);
		}

		return activity;
	}

	/// <summary>
	/// Starts a new activity for message publishing.
	/// </summary>
	/// <param name="message"> The message being published. </param>
	/// <param name="destination"> The destination where the message is being published. </param>
	/// <returns> The started activity, or null if no listener is interested. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="message" /> is null. </exception>
	public static Activity? StartPublishActivity(IDispatchMessage message, string destination)
	{
		ArgumentNullException.ThrowIfNull(message);

		var activity = Instance.StartActivity("message.publish");

		if (activity != null)
		{
			_ = activity.SetTag("message.type", GetCachedTypeName(message.GetType()));
			_ = activity.SetTag("message.destination", destination);
			_ = activity.SetTag("dispatch.operation", "publish");
		}

		return activity;
	}

	/// <summary>
	/// Starts a new activity for message handling.
	/// </summary>
	/// <param name="message"> The message being handled. </param>
	/// <param name="handlerType"> The type of handler processing the message. </param>
	/// <returns> The started activity, or null if no listener is interested. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="message" /> or <paramref name="handlerType" /> is null. </exception>
	public static Activity? StartHandleActivity(IDispatchMessage message, Type handlerType)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(handlerType);

		var activity = Instance.StartActivity("message.handle");

		if (activity != null)
		{
			_ = activity.SetTag("message.type", GetCachedTypeName(message.GetType()));
			_ = activity.SetTag("handler.type", GetCachedTypeName(handlerType));
			_ = activity.SetTag("dispatch.operation", "handle");
		}

		return activity;
	}

	/// <summary>
	/// Starts a new activity for middleware processing.
	/// </summary>
	/// <param name="middlewareType"> The type of middleware. </param>
	/// <param name="message"> The message being processed. </param>
	/// <returns> The started activity, or null if no listener is interested. </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="middlewareType" /> or <paramref name="message" /> is null.
	/// </exception>
	public static Activity? StartMiddlewareActivity(Type middlewareType, IDispatchMessage message)
	{
		ArgumentNullException.ThrowIfNull(middlewareType);
		ArgumentNullException.ThrowIfNull(message);

		var activityName = GetCachedMiddlewareActivityName(middlewareType);

		var activity = Instance.StartActivity(activityName);

		if (activity != null)
		{
			_ = activity.SetTag("middleware.type", GetCachedTypeName(middlewareType));
			_ = activity.SetTag("message.type", GetCachedTypeName(message.GetType()));
			_ = activity.SetTag("dispatch.operation", "middleware");
		}

		return activity;
	}
}
