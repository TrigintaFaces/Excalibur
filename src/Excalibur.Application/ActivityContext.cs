using Excalibur.Core;
using Excalibur.Core.Concurrency;
using Excalibur.Domain;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Application;

/// <summary>
///     Represents a context for managing activity-related data within an application.
/// </summary>
public class ActivityContext : IActivityContext
{
	private readonly Dictionary<string, object> _data = [];

	private readonly string[] _injectedValueNames =
	[
		nameof(TenantId),
		nameof(CorrelationId),
		nameof(ETag),
		nameof(IDomainDb),
		nameof(IConfiguration),
		nameof(ClientAddress),
		nameof(IServiceProvider)
	];

	/// <summary>
	///     Initializes a new instance of the <see cref="ActivityContext" /> class with essential dependencies.
	/// </summary>
	/// <param name="tenantId"> The tenant identifier. </param>
	/// <param name="correlationId"> The correlation identifier. </param>
	/// <param name="eTag"> The ETag for concurrency control. </param>
	/// <param name="domainDb"> The domain database interface. </param>
	/// <param name="configuration"> The application configuration. </param>
	/// <param name="clientAddress"> The client address. </param>
	/// <param name="serviceProvider"> The service provider for dependency resolution. </param>
	public ActivityContext(
		ITenantId tenantId,
		ICorrelationId correlationId,
		IETag eTag,
		IDomainDb domainDb,
		IConfiguration configuration,
		IClientAddress clientAddress,
		IServiceProvider serviceProvider
	)
	{
		_data.Add(nameof(TenantId), tenantId);
		_data.Add(nameof(CorrelationId), correlationId);
		_data.Add(nameof(ETag), eTag);
		_data.Add(nameof(IDomainDb), domainDb);
		_data.Add(nameof(IConfiguration), configuration);
		_data.Add(nameof(ClientAddress), clientAddress);
		_data.Add(nameof(IServiceProvider), serviceProvider);
	}

	/// <inheritdoc />
	public T Get<T>(string key, T defaultValue) => _data.TryGetValue(key, out var value) ? (T)value : defaultValue;

	/// <inheritdoc />
	public void Set<T>(string key, T value)
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
