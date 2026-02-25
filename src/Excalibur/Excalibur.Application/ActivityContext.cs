// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

using Excalibur.Domain;
using Excalibur.Domain.Concurrency;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Application;

/// <summary>
/// Represents a context for managing activity-related data within an application.
/// </summary>
public sealed class ActivityContext : IActivityContext
{
	private readonly Dictionary<string, object> _data = [];

	private readonly string[] _injectedValueNames =
	[
		nameof(TenantId),
		nameof(CorrelationId),
		nameof(ETag),
		nameof(IConfiguration),
		nameof(ClientAddress),
		nameof(IServiceProvider),
	];

	/// <summary>
	/// Initializes a new instance of the <see cref="ActivityContext" /> class with essential dependencies.
	/// </summary>
	/// <param name="tenantId"> The tenant identifier. </param>
	/// <param name="correlationId"> The correlation identifier. </param>
	/// <param name="eTag"> The ETag for concurrency control. </param>
	/// <param name="configuration"> The application configuration. </param>
	/// <param name="clientAddress"> The client address. </param>
	/// <param name="serviceProvider"> The service provider for dependency Excalibur.Tests.Integration. </param>
	public ActivityContext(
		ITenantId tenantId,
		ICorrelationId correlationId,
		IETag eTag,
		IConfiguration configuration,
		IClientAddress clientAddress,
		IServiceProvider serviceProvider)
	{
		_data.Add(nameof(TenantId), tenantId);
		_data.Add(nameof(CorrelationId), correlationId);
		_data.Add(nameof(ETag), eTag);
		_data.Add(nameof(IConfiguration), configuration);
		_data.Add(nameof(ClientAddress), clientAddress);
		_data.Add(nameof(IServiceProvider), serviceProvider);
	}

	/// <inheritdoc />
	public T GetValue<T>(string key, T defaultValue) => _data.TryGetValue(key, out var value) ? (T)value : defaultValue;

	/// <inheritdoc />
	public void SetValue<T>(string key, T value)
	{
		ArgumentNullException.ThrowIfNull(value);

		if (_injectedValueNames.Any(x => x.Equals(key, StringComparison.OrdinalIgnoreCase)))
		{
			throw new InvalidOperationException($"'{key}' is an injected value and cannot be replaced.");
		}

		_data[key] = value;
	}

	/// <inheritdoc />
	public bool ContainsKey(string key)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);

		return _data.ContainsKey(key);
	}

	/// <inheritdoc />
	public void Remove(string key)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);

		if (_injectedValueNames.Any(x => x.Equals(key, StringComparison.OrdinalIgnoreCase)))
		{
			throw new InvalidOperationException($"'{key}' is an injected value and cannot be removed.");
		}

		_ = _data.Remove(key);
	}
}
