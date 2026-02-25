// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch.Compliance;

using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Encryption.Decorators;

/// <summary>
/// Decorates an <see cref="IProjectionStore{TProjection}"/> with transparent field-level encryption.
/// </summary>
/// <remarks>
/// <para>
/// This decorator provides mixed-mode read support during encryption migration.
/// It encrypts/decrypts <see cref="byte"/>[] properties marked with <see cref="EncryptedFieldAttribute"/>
/// using <see cref="EncryptedData.IsFieldEncrypted(byte[])"/> to detect encrypted data.
/// </para>
/// <para>
/// <b>Note:</b> Only properties of type <c>byte[]</c> are encrypted. For string properties,
/// use a separate serialization layer that converts strings to/from encrypted byte arrays.
/// </para>
/// </remarks>
/// <typeparam name="TProjection">The projection type.</typeparam>
public sealed class EncryptingProjectionStoreDecorator<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TProjection> : IProjectionStore<TProjection>
	where TProjection : class
{
	private readonly IProjectionStore<TProjection> _inner;
	private readonly IEncryptionProviderRegistry _registry;
	private readonly IOptions<EncryptionOptions> _options;
	private readonly EncryptionContext _defaultContext;
	private readonly PropertyInfo[] _encryptedProperties;

	/// <summary>
	/// Initializes a new instance of the <see cref="EncryptingProjectionStoreDecorator{TProjection}"/> class.
	/// </summary>
	/// <param name="inner">The underlying projection store to decorate.</param>
	/// <param name="registry">The encryption provider registry for multi-provider support.</param>
	/// <param name="options">The encryption configuration options.</param>
	public EncryptingProjectionStoreDecorator(
		IProjectionStore<TProjection> inner,
		IEncryptionProviderRegistry registry,
		IOptions<EncryptionOptions> options)
	{
		_inner = inner ?? throw new ArgumentNullException(nameof(inner));
		_registry = registry ?? throw new ArgumentNullException(nameof(registry));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_defaultContext = new EncryptionContext
		{
			Purpose = options.Value.DefaultPurpose,
			TenantId = options.Value.DefaultTenantId,
			RequireFipsCompliance = options.Value.RequireFipsCompliance
		};

		_encryptedProperties =
		[
			.. typeof(TProjection)
				.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(p => p.PropertyType == typeof(byte[]) &&
							p.GetCustomAttribute<EncryptedFieldAttribute>() is not null &&
							p.CanRead && p.CanWrite)
		];
	}

	/// <inheritdoc/>
	public async Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken)
	{
		var projection = await _inner.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
		if (projection is null)
		{
			return null;
		}

		return await DecryptProjectionAsync(projection, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task UpsertAsync(string id, TProjection projection, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(projection);

		var mode = _options.Value.Mode;

		if (mode == EncryptionMode.DecryptOnlyReadOnly)
		{
			throw new InvalidOperationException(
				Resources.Encryption_ReadOnlyProjectionStore);
		}

		if (mode is EncryptionMode.EncryptAndDecrypt or EncryptionMode.EncryptNewDecryptAll)
		{
			await EncryptProjectionAsync(projection, cancellationToken).ConfigureAwait(false);
		}

		await _inner.UpsertAsync(id, projection, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public Task DeleteAsync(string id, CancellationToken cancellationToken)
	{
		var mode = _options.Value.Mode;

		if (mode == EncryptionMode.DecryptOnlyReadOnly)
		{
			throw new InvalidOperationException(
				Resources.Encryption_ReadOnlyProjectionStore);
		}

		return _inner.DeleteAsync(id, cancellationToken);
	}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<TProjection>> QueryAsync(
		IDictionary<string, object>? filters,
		QueryOptions? options,
		CancellationToken cancellationToken)
	{
		var projections = await _inner.QueryAsync(filters, options, cancellationToken).ConfigureAwait(false);
		var results = new List<TProjection>(projections.Count);

		foreach (var projection in projections)
		{
			results.Add(await DecryptProjectionAsync(projection, cancellationToken).ConfigureAwait(false));
		}

		return results;
	}

	/// <inheritdoc/>
	public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken)
	{
		return _inner.CountAsync(filters, cancellationToken);
	}

	[UnconditionalSuppressMessage(
		"ReflectionAnalysis",
		"IL2026:RequiresUnreferencedCode",
		Justification =
			"Encryption envelope serialization uses JsonSerializer for a known type at runtime.")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Using RequiresDynamicCode member in AOT",
		Justification =
			"Encryption envelope serialization uses JsonSerializer for a known type at runtime.")]
	private static byte[] SerializeEncryptedData(EncryptedData encryptedData)
	{
		var jsonBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(encryptedData);
		var result = new byte[EncryptedData.MagicBytes.Length + jsonBytes.Length];
		EncryptedData.MagicBytes.CopyTo(result.AsSpan());
		jsonBytes.CopyTo(result, EncryptedData.MagicBytes.Length);
		return result;
	}

	[UnconditionalSuppressMessage(
		"ReflectionAnalysis",
		"IL2026:RequiresUnreferencedCode",
		Justification =
			"Encryption envelope deserialization uses JsonSerializer for a known type at runtime.")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Using RequiresDynamicCode member in AOT",
		Justification =
			"Encryption envelope deserialization uses JsonSerializer for a known type at runtime.")]
	private static EncryptedData DeserializeEncryptedData(byte[] data)
	{
		var envelopeData = data.AsSpan(EncryptedData.MagicBytes.Length);
		return System.Text.Json.JsonSerializer.Deserialize<EncryptedData>(envelopeData)
			   ?? throw new EncryptionException(
				   Resources.Encryption_FailedToDeserializeEncryptedDataEnvelope);
	}

	private async Task<TProjection> DecryptProjectionAsync(TProjection projection, CancellationToken cancellationToken)
	{
		var mode = _options.Value.Mode;

		if (mode == EncryptionMode.Disabled || _encryptedProperties.Length == 0)
		{
			return projection;
		}

		foreach (var prop in _encryptedProperties)
		{
			var value = (byte[]?)prop.GetValue(projection);
			if (value is null || value.Length == 0)
			{
				continue;
			}

			if (EncryptedData.IsFieldEncrypted(value))
			{
				var decrypted = await TryDecryptFieldAsync(value, cancellationToken).ConfigureAwait(false);
				prop.SetValue(projection, decrypted);
			}
		}

		return projection;
	}

	private async Task EncryptProjectionAsync(TProjection projection, CancellationToken cancellationToken)
	{
		if (_encryptedProperties.Length == 0)
		{
			return;
		}

		foreach (var prop in _encryptedProperties)
		{
			var value = (byte[]?)prop.GetValue(projection);
			if (value is null || value.Length == 0)
			{
				continue;
			}

			// Don't double-encrypt
			if (EncryptedData.IsFieldEncrypted(value))
			{
				continue;
			}

			var encrypted = await EncryptPayloadAsync(value, cancellationToken).ConfigureAwait(false);
			prop.SetValue(projection, encrypted);
		}
	}

	private async ValueTask<byte[]> EncryptPayloadAsync(byte[] data, CancellationToken cancellationToken)
	{
		var provider = _registry.GetPrimary();
		var encryptedData = await provider.EncryptAsync(data, _defaultContext, cancellationToken).ConfigureAwait(false);
		return SerializeEncryptedData(encryptedData);
	}

	private async ValueTask<byte[]> TryDecryptFieldAsync(byte[] data, CancellationToken cancellationToken)
	{
		if (!EncryptedData.IsFieldEncrypted(data))
		{
			return data;
		}

		var encryptedData = DeserializeEncryptedData(data);
		var provider = _registry.FindDecryptionProvider(encryptedData)
					   ?? throw new EncryptionException(
						   Resources.Encryption_NoProviderCanDecrypt);

		return await provider.DecryptAsync(encryptedData, _defaultContext, cancellationToken).ConfigureAwait(false);
	}
}
