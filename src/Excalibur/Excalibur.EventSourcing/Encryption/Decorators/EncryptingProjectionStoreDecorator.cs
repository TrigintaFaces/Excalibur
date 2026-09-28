// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.Decorators;

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
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TProjection>
	: IsolatingProjectionStoreDecorator<TProjection>
	where TProjection : class
{
	private readonly IProjectionStore<TProjection> _inner;
	private readonly IEncryptionProviderRegistry _registry;
	private readonly IOptions<EncryptionOptions> _options;
	/// <summary>
	/// Resolves the tenant of the operation in flight. Consulted PER CALL, never captured.
	/// </summary>
	/// <remarks>
	/// The AES-GCM provider binds this into the Additional Authenticated Data, and its own comment
	/// names cross-tenant decryption as the thing that prevents. Stamping it once at construction from
	/// a process-wide option made every record in a multi-tenant host carry the SAME tenant, so the
	/// component advertised as the cross-tenant control contributed nothing on exactly the paths that
	/// encrypt stored data. A construction-time context cannot carry a per-operation value, so the
	/// cached field is removed rather than corrected -- a constant stamp now has nowhere to live.
	/// </remarks>
	private readonly ITenantContext _tenantContext;
	private readonly PropertyInfo[] _encryptedProperties;

	/// <summary>
	/// Initializes a new instance of the <see cref="EncryptingProjectionStoreDecorator{TProjection}"/> class.
	/// </summary>
	/// <param name="inner">The underlying projection store to decorate.</param>
	/// <param name="registry">The encryption provider registry for multi-provider support.</param>
	/// <param name="options">The encryption configuration options.</param>
	/// <param name="tenantContext">
	/// Resolves the tenant each operation runs as, so the AAD binds the DATA's tenant rather than a
	/// process-wide constant. Required: a single-tenant host receives the framework's single-tenant default.
	/// </param>
	public EncryptingProjectionStoreDecorator(
		IProjectionStore<TProjection> inner,
		IEncryptionProviderRegistry registry,
		IOptions<EncryptionOptions> options,
		ITenantContext tenantContext)
		: base(inner)
	{
		_inner = Inner;
		_registry = registry ?? throw new ArgumentNullException(nameof(registry));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));

		_encryptedProperties = EncryptedFieldBinding.Select<TProjection>();
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public override async Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken)
	{
		var projection = await _inner.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
		if (projection is null)
		{
			return null;
		}

		return await DecryptProjectionAsync(projection, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public override async Task UpsertAsync(string id, TProjection projection, CancellationToken cancellationToken)
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
	public override Task DeleteAsync(string id, CancellationToken cancellationToken)
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
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public override async Task<IReadOnlyList<TProjection>> QueryAsync(
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
	public override Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken)
	{
		return _inner.CountAsync(filters, cancellationToken);
	}

	/// <summary>
	/// Wraps a capability of the decorated store so its results are decrypted before a caller sees them.
	/// </summary>
	/// <param name="serviceType">The capability interface being resolved.</param>
	/// <returns>A decrypting view over the capability, or <see langword="null"/> when the inner store lacks it.</returns>
	/// <remarks>
	/// Every projection this store returns has to pass through the same decryption the base surface applies.
	/// A capability handed over unwrapped would return the stored ciphertext, so each is fronted by a view
	/// that decrypts the page it produces.
	/// </remarks>
	protected override object? WrapCapability(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(IPageableProjectionStore<TProjection>)
			&& Inner.GetService(typeof(IPageableProjectionStore<TProjection>)) is IPageableProjectionStore<TProjection> pageable)
		{
			return new DecryptingPageableView(this, pageable);
		}

		if (serviceType == typeof(ICursorProjectionStore<TProjection>)
			&& Inner.GetService(typeof(ICursorProjectionStore<TProjection>)) is ICursorProjectionStore<TProjection> cursor)
		{
			return new DecryptingCursorView(this, cursor);
		}

		// Unlike the two above, this capability is not an optimisation: declining to wrap it sends the
		// caller to the unconditional write, which is the double application the capability exists to
		// prevent. It must be mediated, not dropped.
		if (serviceType == typeof(IPositionedProjectionStore<TProjection>)
			&& Inner.GetService(typeof(IPositionedProjectionStore<TProjection>))
				is IPositionedProjectionStore<TProjection> positioned)
		{
			return new EncryptingPositionedView(this, positioned);
		}

		return null;
	}

	/// <summary>
	/// Applies the write-side encryption decision, exactly as the unconditional upsert does.
	/// </summary>
	/// <remarks>
	/// Shared rather than duplicated so the positioned write cannot drift from the plain one: a
	/// decorator that encrypted on one path and not the other would write mixed cleartext and
	/// ciphertext into the same projection table.
	/// </remarks>
	private async Task EncryptForWriteAsync(TProjection projection, CancellationToken cancellationToken)
	{
		var mode = _options.Value.Mode;

		if (mode == EncryptionMode.DecryptOnlyReadOnly)
		{
			throw new InvalidOperationException(Resources.Encryption_ReadOnlyProjectionStore);
		}

		if (mode is EncryptionMode.EncryptAndDecrypt or EncryptionMode.EncryptNewDecryptAll)
		{
			await EncryptProjectionAsync(projection, cancellationToken).ConfigureAwait(false);
		}
	}

	private async Task<List<TProjection>> DecryptAllAsync(
		IEnumerable<TProjection> projections,
		CancellationToken cancellationToken)
	{
		var results = new List<TProjection>();

		foreach (var projection in projections)
		{
			results.Add(await DecryptProjectionAsync(projection, cancellationToken).ConfigureAwait(false));
		}

		return results;
	}

	/// <summary>
	/// Encrypts on the way in and decrypts on the way out, while leaving the POSITION untouched.
	/// </summary>
	/// <remarks>
	/// <b>The position stays in cleartext, and that is deliberate.</b> It is a coordination value the
	/// STORE must compare -- the conditional write succeeds only when the stored position equals the
	/// one the caller read. An encrypted position is opaque to that comparison, so every write would be
	/// refused and the projection would never advance again. It carries no personal data: it is an
	/// ordinal into the event stream, and the events themselves are protected where they live.
	/// </remarks>
	private sealed class EncryptingPositionedView(
		EncryptingProjectionStoreDecorator<TProjection> outer,
		IPositionedProjectionStore<TProjection> capability)
		: ProjectionStoreCapabilityView<TProjection>(outer), IPositionedProjectionStore<TProjection>
	{
		[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		public async Task<(TProjection? Projection, ProjectionPosition Position)> GetWithPositionAsync(
			string id,
			CancellationToken cancellationToken)
		{
			var (projection, position) = await capability.GetWithPositionAsync(id, cancellationToken)
				.ConfigureAwait(false);

			if (projection is null)
			{
				return (null, position);
			}

			var decrypted = await outer.DecryptAllAsync([projection], cancellationToken).ConfigureAwait(false);
			return (decrypted.Count > 0 ? decrypted[0] : projection, position);
		}

		/// <inheritdoc />
		/// <remarks>
		/// Encrypts before forwarding, exactly as the positioned writes do. A state written with no
		/// position number is still the subject's data; forwarding it in the clear would write an
		/// unencrypted row through a decorator whose whole purpose is that it cannot.
		/// </remarks>
		[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		public async Task UpsertUnnumberedAsync(
			string id,
			TProjection projection,
			CancellationToken cancellationToken)
		{
			await outer.EncryptForWriteAsync(projection, cancellationToken).ConfigureAwait(false);
			await capability.UpsertUnnumberedAsync(id, projection, cancellationToken).ConfigureAwait(false);
		}

		[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		public async Task<ProjectionAdvanceResult> UpsertAtPositionAsync(
			string id,
			TProjection projection,
			long? expectedPosition,
			long newPosition,
			CancellationToken cancellationToken)
		{
			await outer.EncryptForWriteAsync(projection, cancellationToken).ConfigureAwait(false);

			return await capability.UpsertAtPositionAsync(
				id, projection, expectedPosition, newPosition, cancellationToken).ConfigureAwait(false);
		}

		/// <inheritdoc />
		/// <remarks>
		/// Encrypts before forwarding, exactly as the advancing write does. A re-fold rewrites the
		/// same state a normal write would, so it owes the same protection; forwarding it in the clear
		/// would write an unencrypted row through a decorator whose whole purpose is that it cannot.
		/// </remarks>
		[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		public async Task<ProjectionRefoldResult> RefoldAtPositionAsync(
			string id,
			TProjection projection,
			long atPosition,
			CancellationToken cancellationToken)
		{
			await outer.EncryptForWriteAsync(projection, cancellationToken).ConfigureAwait(false);

			return await capability.RefoldAtPositionAsync(id, projection, atPosition, cancellationToken)
				.ConfigureAwait(false);
		}
	}

	private sealed class DecryptingPageableView(
		EncryptingProjectionStoreDecorator<TProjection> outer,
		IPageableProjectionStore<TProjection> capability)
		: ProjectionStoreCapabilityView<TProjection>(outer), IPageableProjectionStore<TProjection>
	{
		[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		public async Task<PagedResult<TProjection>> QueryPagedAsync(
			IDictionary<string, object>? filters,
			int pageNumber,
			int pageSize,
			QueryOptions? options,
			CancellationToken cancellationToken)
		{
			var page = await capability
				.QueryPagedAsync(filters, pageNumber, pageSize, options, cancellationToken)
				.ConfigureAwait(false);

			var decrypted = await outer.DecryptAllAsync(page.Items, cancellationToken).ConfigureAwait(false);

			return new PagedResult<TProjection>(decrypted, page.PageNumber, page.PageSize, page.TotalItems);
		}
	}

	private sealed class DecryptingCursorView(
		EncryptingProjectionStoreDecorator<TProjection> outer,
		ICursorProjectionStore<TProjection> capability)
		: ProjectionStoreCapabilityView<TProjection>(outer), ICursorProjectionStore<TProjection>
	{
		[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
		public async Task<CursorPagedResult<TProjection>> QueryCursorAsync(
			IDictionary<string, object>? filters,
			string? cursor,
			int pageSize,
			CancellationToken cancellationToken)
		{
			var page = await capability
				.QueryCursorAsync(filters, cursor, pageSize, cancellationToken)
				.ConfigureAwait(false);

			var decrypted = await outer.DecryptAllAsync(page.Items, cancellationToken).ConfigureAwait(false);

			return new CursorPagedResult<TProjection>(
				decrypted,
				page.PageSize,
				page.TotalRecords,
				page.NextCursor,
				page.PreviousCursor);
		}
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
			if (!EncryptedFieldBinding.TryReadEnvelope(prop, projection, out var stored))
			{
				continue;
			}

			var decrypted = await TryDecryptFieldAsync(stored, cancellationToken).ConfigureAwait(false);
			EncryptedFieldBinding.WritePlaintext(prop, projection, decrypted);
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
			// Don't double-encrypt. A WRITER asks only whether the value is marked, and deliberately does
			// not validate it: refusing to store a record because the value already in the field is damaged
			// would make a row whose ciphertext was truncated permanently unwritable, and truncation is a
			// write-side cause. Reading the marker rather than decoding also stops a plaintext that happens
			// to be Base64 of magic-prefixed bytes being mistaken for ciphertext.
			if (EncryptedFieldBinding.IsMarkedEncrypted(prop, projection))
			{
				continue;
			}

			var plaintext = EncryptedFieldBinding.ReadPlaintext(prop, projection);
			if (plaintext is null)
			{
				continue;
			}

			var encrypted = await EncryptPayloadAsync(plaintext, cancellationToken).ConfigureAwait(false);
			EncryptedFieldBinding.WriteEnvelope(prop, projection, encrypted);
		}
	}

	private async ValueTask<byte[]> EncryptPayloadAsync(byte[] data, CancellationToken cancellationToken)
	{
		var provider = _registry.GetPrimary();
		var encryptedData = await provider.EncryptAsync(data, CurrentEncryptionContext(), cancellationToken).ConfigureAwait(false);
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

		return await provider.DecryptAsync(encryptedData, CurrentEncryptionContext(), cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Builds the encryption context for the operation in flight, binding the tenant it is running as.
	/// </summary>
	/// <remarks>
	/// Falls back to the configured default ONLY when no tenant context is registered at all, which is
	/// the single-tenant composition -- there a constant is the correct answer and always was, so this
	/// keeps existing single-tenant ciphertext decryptable. A multi-tenant host always resolves a tenant
	/// and therefore always gets real separation, which is the case the defect was about.
	/// </remarks>
	private EncryptionContext CurrentEncryptionContext() => new()
	{
		Purpose = _options.Value.DefaultPurpose,
		TenantId = _tenantContext.TenantId,
		RequireFipsCompliance = _options.Value.RequireFipsCompliance
	};
}
