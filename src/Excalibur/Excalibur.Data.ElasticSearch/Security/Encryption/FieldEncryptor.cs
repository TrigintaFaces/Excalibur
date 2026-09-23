// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Excalibur.Compliance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Production implementation of field-level encryption for Elasticsearch documents with enterprise-grade security features and
/// comprehensive audit capabilities.
/// </summary>
/// <remarks>
/// Field-encryption key material never crosses into this package. Every key resolution, algorithm choice, and cryptographic
/// operation is delegated to <see cref="IKeyManagementProvider"/> and <see cref="IEncryptionProviderRegistry"/> -- this class
/// never sees a raw key byte. That is the same envelope-encryption model <c>Excalibur.Compliance.CryptoShredding.FieldEncryptor</c>
/// already uses; a cloud KMS or HSM-backed provider can therefore back Elasticsearch field encryption without ever exporting
/// key material to this process.
/// </remarks>
public sealed class FieldEncryptor : IElasticsearchFieldEncryptor, IDisposable, IAsyncDisposable
{
	private const string DefaultPurpose = "elasticsearch-field-encryption";

	private readonly IKeyManagementProvider _keyProvider;
	private readonly IEncryptionProviderRegistry _registry;
	private readonly ILogger<FieldEncryptor> _logger;
	private readonly EncryptionOptions _settings;
	private readonly Dictionary<ElasticSearchDataClassification, Regex> _classificationPatterns;
	private readonly SemaphoreSlim _encryptionSemaphore;
	// System.Threading.Timer's maximum dueTime/period is uint.MaxValue-1 ms (~49.7 days). A configured rotation
	// interval longer than this (the default is 90 days) would throw ArgumentOutOfRangeException at construction,
	// so the timer is armed in clamped chunks and re-armed from the callback until the full interval elapses.
	private static readonly TimeSpan MaxTimerInterval = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

	private readonly Timer? _keyRotationTimer;
	private readonly TimeSpan _keyRotationInterval;
	private DateTimeOffset _nextKeyRotationDueUtc;
	private ConcurrentBag<Task> _trackedTasks = [];
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="FieldEncryptor" /> class.
	/// </summary>
	/// <param name="keyProvider"> The key management provider that resolves and rotates encryption keys, without ever exposing key material. </param>
	/// <param name="registry"> The encryption provider registry used to encrypt and decrypt field values. </param>
	/// <param name="options"> The encryption configuration options. </param>
	/// <param name="logger"> The logger for security and operational events. </param>
	/// <exception cref="ArgumentNullException"> Thrown when required dependencies are null. </exception>
	public FieldEncryptor(
		IKeyManagementProvider keyProvider,
		IEncryptionProviderRegistry registry,
		IOptions<EncryptionOptions> options,
		ILogger<FieldEncryptor> logger)
	{
		_keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
		_registry = registry ?? throw new ArgumentNullException(nameof(registry));
		_settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		_classificationPatterns = BuildClassificationPatterns();
		_encryptionSemaphore = new SemaphoreSlim(Environment.ProcessorCount * 2, Environment.ProcessorCount * 2);

		// Initialize key rotation timer. Every IKeyManagementProvider supports RotateKeyAsync as a first-class
		// interface member, so arming depends only on a positive configured interval. Arm only the first
		// (clamped) chunk and re-arm from the callback -- a configured interval longer than Timer's max would
		// otherwise throw at construction.
		if (_settings.KeyManagement.KeyRotationInterval > TimeSpan.Zero)
		{
			_keyRotationInterval = _settings.KeyManagement.KeyRotationInterval;
			_nextKeyRotationDueUtc = DateTimeOffset.UtcNow + _keyRotationInterval;
			_keyRotationTimer = new Timer(PerformScheduledKeyRotation, state: null,
				ClampToTimerMax(_keyRotationInterval), Timeout.InfiniteTimeSpan);
		}

		_logger.LogInformation(
			"FieldEncryptor initialized with {RuleCount} classification rules",
			_settings.ClassificationRules.Count);
	}

	/// <inheritdoc />
	public event EventHandler<FieldEncryptedEventArgs>? FieldEncrypted;

	/// <inheritdoc />
	public event EventHandler<FieldDecryptedEventArgs>? FieldDecrypted;

	/// <inheritdoc />
	public event EventHandler<EncryptionKeyRotatedEventArgs>? KeyRotated;

	/// <inheritdoc />
	public IReadOnlyCollection<string> SupportedAlgorithms { get; } = ["AES-256-GCM"];

	/// <inheritdoc />
	public bool SupportsKeyRotation => true;

	/// <inheritdoc />
	public bool SupportsIntegrityValidation => true;

	/// <inheritdoc />
	[RequiresUnreferencedCode("JSON serialization may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("JSON serialization uses reflection to dynamically access and serialize types")]
	public async Task<object> EncryptDocumentAsync(object document, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(document);

		if (!_settings.FieldLevelEncryption)
		{
			_logger.LogDebug("Field-level encryption is disabled, returning document unchanged");
			return document;
		}

		await _encryptionSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			// Convert document to JSON for field processing
			var json = JsonSerializer.Serialize(document);
			var jsonDocument = JsonDocument.Parse(json);
			var encryptedProperties = new Dictionary<string, object>(StringComparer.Ordinal);

			await ProcessJsonElementAsync(jsonDocument.RootElement, encryptedProperties, string.Empty, cancellationToken)
				.ConfigureAwait(false);

			_logger.LogDebug(
				"Document encryption completed with {FieldCount} encrypted fields",
				encryptedProperties.Count);

			return encryptedProperties;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to encrypt document");
			throw new SecurityException("Document encryption failed", ex);
		}
		finally
		{
			_ = _encryptionSemaphore.Release();
		}
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	[RequiresDynamicCode("This method uses dynamic code generation and may not work correctly with AOT")]
	public async Task<object> DecryptDocumentAsync(object encryptedDocument, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(encryptedDocument);

		if (!_settings.FieldLevelEncryption)
		{
			return encryptedDocument;
		}

		await _encryptionSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var decryptedProperties = new Dictionary<string, object>(StringComparer.Ordinal);

			if (encryptedDocument is Dictionary<string, object> documentDict)
			{
				foreach (var kvp in documentDict)
				{
					if (kvp.Value is EncryptedFieldResult encryptedField)
					{
						var decryptedValue = await DecryptFieldAsync(kvp.Key, encryptedField, cancellationToken).ConfigureAwait(false);
						decryptedProperties[kvp.Key] = decryptedValue;
					}
					else
					{
						decryptedProperties[kvp.Key] = kvp.Value;
					}
				}
			}

			_logger.LogDebug(
				"Document decryption completed with {FieldCount} decrypted fields",
				decryptedProperties.Count);

			return decryptedProperties;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to decrypt document");
			throw new SecurityException("Document decryption failed", ex);
		}
		finally
		{
			_ = _encryptionSemaphore.Release();
		}
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("JSON serialization may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("JSON serialization uses reflection to dynamically access and serialize types")]
	public async Task<EncryptedFieldResult> EncryptFieldAsync(
		string fieldName,
		object fieldValue,
		ElasticSearchDataClassification classification,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(fieldName))
		{
			throw new ArgumentException("Field name cannot be null or empty", nameof(fieldName));
		}

		ArgumentNullException.ThrowIfNull(fieldValue);

		try
		{
			var plaintext = JsonSerializer.Serialize(fieldValue);
			var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

			var keyName = GetKeyNameForClassification(classification);
			var keyMetadata = await _keyProvider.GetKeyAsync(keyName, cancellationToken).ConfigureAwait(false);

			if (keyMetadata is null)
			{
				// First use for this classification: create the key. RotateKeyAsync creates-or-rotates, so an
				// absent key is created here rather than through a separate "generate" seam.
				var creation = await _keyProvider.RotateKeyAsync(
					keyName, EncryptionAlgorithm.Aes256Gcm, purpose: DefaultPurpose, expiresAt: null, cancellationToken)
					.ConfigureAwait(false);

				keyMetadata = creation.NewKey
					?? throw new SecurityException($"Failed to create encryption key '{keyName}': {creation.ErrorMessage}");
			}

			var algorithmName = AlgorithmToWireName(keyMetadata.Algorithm);

			// Bind the field's crypto context (fieldName|keyVersion|algorithm|classification) as Associated
			// Authenticated Data. The AAD is authenticated but not encrypted; decryption with a different
			// context fails the auth tag -- preventing ciphertext-swapping between fields (e.g. moving an
			// encrypted salary blob into a notes field and decrypting it).
			var keyVersionText = keyMetadata.Version.ToString(CultureInfo.InvariantCulture);
			var context = new EncryptionContext
			{
				KeyId = keyName,
				KeyVersion = keyMetadata.Version,
				Algorithm = keyMetadata.Algorithm,
				AssociatedData = BuildFieldAssociatedData(fieldName, keyVersionText, algorithmName, classification),
			};

			var encrypted = await _registry.GetPrimary().EncryptAsync(plaintextBytes, context, cancellationToken).ConfigureAwait(false);

			var result = new EncryptedFieldResult(
				Convert.ToBase64String(encrypted.Ciphertext),
				algorithmName,
				keyVersionText,
				Convert.ToBase64String(encrypted.Iv),
				encrypted.AuthTag is { } authTag ? Convert.ToBase64String(authTag) : null,
				classification,
				EncryptedFieldResult.CurrentFormatVersion);

			// Raise encryption event for auditing
			FieldEncrypted?.Invoke(this, new FieldEncryptedEventArgs(
				fieldName, classification, algorithmName, keyVersionText, DateTimeOffset.UtcNow));

			_logger.LogDebug(
				"Field {FieldName} encrypted with classification {Classification} using {Algorithm}",
				fieldName, classification, algorithmName);

			return result;
		}
		catch (SecurityException)
		{
			throw;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to encrypt field {FieldName}", fieldName);
			throw new SecurityException($"Field encryption failed for {fieldName}", ex);
		}
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("JSON deserialization may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("JSON deserialization uses reflection to dynamically create and populate types")]
	public async Task<object> DecryptFieldAsync(
		string fieldName,
		EncryptedFieldResult encryptedField,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(fieldName))
		{
			throw new ArgumentException("Field name cannot be null or empty", nameof(fieldName));
		}

		ArgumentNullException.ThrowIfNull(encryptedField);

		try
		{
			// Dispatch on the envelope format-version discriminator. An unknown format is surfaced as an error rather
			// than parsed best-effort, providing a safe forward-migration path for future envelope changes.
			if (!string.Equals(encryptedField.FormatVersion, EncryptedFieldResult.CurrentFormatVersion, StringComparison.Ordinal))
			{
				throw new SecurityException(
					$"Unknown encrypted-field format version '{encryptedField.FormatVersion}' for field {fieldName}; expected '{EncryptedFieldResult.CurrentFormatVersion}'.");
			}

			// No separate integrity pre-check: the AEAD decrypt below authenticates the ciphertext against
			// its tag and throws on a mismatch. Verifying first would decrypt the field twice to learn the same
			// thing. ValidateIntegrityAsync exists for callers that want that answer WITHOUT the plaintext.
			var (envelope, context) = BuildDecryptionEnvelope(fieldName, encryptedField);

			var provider = _registry.FindDecryptionProvider(envelope)
				?? throw new SecurityException(
					$"No registered encryption provider supports algorithm '{encryptedField.Algorithm}' for field {fieldName}.");

			var decryptedBytes = await provider.DecryptAsync(envelope, context, cancellationToken).ConfigureAwait(false);
			var decryptedJson = Encoding.UTF8.GetString(decryptedBytes);
			var decryptedValue = JsonSerializer.Deserialize<object>(decryptedJson);

			// Raise decryption event for auditing
			FieldDecrypted?.Invoke(this, new FieldDecryptedEventArgs(
				fieldName, encryptedField.Classification, encryptedField.Algorithm,
				encryptedField.KeyVersion, DateTimeOffset.UtcNow));

			_logger.LogDebug("Field {FieldName} decrypted successfully", fieldName);

			return decryptedValue!;
		}
		catch (SecurityException)
		{
			throw;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to decrypt field {FieldName}", fieldName);
			throw new SecurityException($"Field decryption failed for {fieldName}", ex);
		}
	}

	/// <inheritdoc />
	public bool ShouldEncryptField(string fieldName, object? fieldValue)
	{
		if (string.IsNullOrEmpty(fieldName) || !_settings.FieldLevelEncryption)
		{
			return false;
		}

		// Check classification rules to determine if field should be encrypted
		var classification = GetFieldClassification(fieldName, fieldValue);
		return classification != ElasticSearchDataClassification.Public;
	}

	/// <inheritdoc />
	public ElasticSearchDataClassification GetFieldClassification(string fieldName, object? fieldValue)
	{
		if (string.IsNullOrEmpty(fieldName))
		{
			return ElasticSearchDataClassification.Public;
		}

		// Check against configured classification rules
		foreach (var rule in _settings.ClassificationRules.Where(static r => r.Enabled))
		{
			if (_classificationPatterns.TryGetValue(rule.Classification, out var pattern) &&
				pattern.IsMatch(fieldName))
			{
				return rule.Classification;
			}
		}

		// Check for common PII patterns in field names
		if (IsPiiField(fieldName))
		{
			return ElasticSearchDataClassification.PersonallyIdentifiable;
		}

		// Check for health information patterns
		if (IsHealthInformationField(fieldName))
		{
			return ElasticSearchDataClassification.HealthInformation;
		}

		return ElasticSearchDataClassification.Public;
	}

	/// <inheritdoc />
	public async Task<bool> ValidateIntegrityAsync(
		string fieldName,
		EncryptedFieldResult encryptedField,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(fieldName))
		{
			throw new ArgumentException("Field name cannot be null or empty", nameof(fieldName));
		}

		ArgumentNullException.ThrowIfNull(encryptedField);

		// Every branch below that is not a verified tag returns false. Integrity is a property that must be
		// positively established: an envelope this build cannot open has not been shown to be intact, and
		// reporting it as valid is the defect this method previously had.
		if (!encryptedField.HasIntegrityProtection || string.IsNullOrEmpty(encryptedField.InitializationVector))
		{
			_logger.LogWarning(
				"Integrity validation requested for field {FieldName}, which carries no authentication tag or IV",
				fieldName);
			return false;
		}

		if (!string.Equals(encryptedField.FormatVersion, EncryptedFieldResult.CurrentFormatVersion, StringComparison.Ordinal))
		{
			_logger.LogWarning(
				"Integrity validation cannot interpret envelope format version {FormatVersion} for field {FieldName}; expected {ExpectedFormatVersion}",
				encryptedField.FormatVersion, fieldName, EncryptedFieldResult.CurrentFormatVersion);
			return false;
		}

		try
		{
			// Address the key by the exact version stamped on the envelope, as decryption does. The current key
			// cannot authenticate pre-rotation ciphertext.
			var (envelope, context) = BuildDecryptionEnvelope(fieldName, encryptedField);

			var provider = _registry.FindDecryptionProvider(envelope);
			if (provider is null)
			{
				_logger.LogWarning(
					"Integrity validation could not resolve a provider for field {FieldName} algorithm {Algorithm}",
					fieldName, encryptedField.Algorithm);
				return false;
			}

			// The AEAD decrypt IS the verification: it recomputes the tag over the ciphertext and the associated
			// data and throws unless the stored tag matches. Nothing short of this authenticates anything -- a
			// well-formed tag that was never computed from this ciphertext fails here. The recovered plaintext
			// is discarded; callers wanting the value use DecryptFieldAsync.
			_ = await provider.DecryptAsync(envelope, context, cancellationToken).ConfigureAwait(false);

			_logger.LogDebug("Integrity validated for field {FieldName}", fieldName);
			return true;
		}
		catch (FormatException)
		{
			_logger.LogWarning(
				"Integrity validation failed for field {FieldName}: the envelope is not valid Base64",
				fieldName);
			return false;
		}
		catch (EncryptionException ex)
		{
			// Tag mismatch, an unresolvable key, or an unsupported algorithm -- integrity was never established.
			_logger.LogWarning(ex, "Integrity validation FAILED for field {FieldName}", fieldName);
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<EncryptionKeyRotationResult> RotateEncryptionKeysAsync(
		ElasticSearchDataClassification classification,
		CancellationToken cancellationToken)
	{
		try
		{
			var keyName = GetKeyNameForClassification(classification);
			var previous = await _keyProvider.GetKeyAsync(keyName, cancellationToken).ConfigureAwait(false);

			var rotation = await _keyProvider.RotateKeyAsync(
				keyName, EncryptionAlgorithm.Aes256Gcm, purpose: DefaultPurpose, expiresAt: null, cancellationToken)
				.ConfigureAwait(false);

			if (!rotation.Success || rotation.NewKey is null)
			{
				return EncryptionKeyRotationResult.CreateFailure(classification, rotation.ErrorMessage ?? "Unknown error");
			}

			var newVersion = rotation.NewKey.Version.ToString(CultureInfo.InvariantCulture);
			var previousKey = rotation.PreviousKey ?? previous;
			var previousVersion = previousKey?.Version.ToString(CultureInfo.InvariantCulture)
				?? throw new InvalidOperationException("Key rotation succeeded but no previous key version is available.");

			// Raise key rotation event
			KeyRotated?.Invoke(this, new EncryptionKeyRotatedEventArgs(
				classification, newVersion, previousVersion,
				0, DateTimeOffset.UtcNow)); // Document count would need separate tracking

			_logger.LogInformation(
				"Encryption key rotated for classification {Classification}, new version {NewVersion}",
				classification, newVersion);

			return EncryptionKeyRotationResult.CreateSuccess(classification, newVersion, previousVersion);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to rotate encryption keys for classification {Classification}", classification);
			return EncryptionKeyRotationResult.CreateFailure(classification, ex.Message);
		}
	}

	/// <summary>
	/// Releases all resources used by the FieldEncryptor.
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_encryptionSemaphore?.Dispose();
		_keyRotationTimer?.Dispose();
		_disposed = true;
	}

	/// <summary>
	/// Asynchronously releases resources, ensuring tracked timer callbacks have completed.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		_encryptionSemaphore?.Dispose();
		if (_keyRotationTimer != null)
		{
			await _keyRotationTimer.DisposeAsync().ConfigureAwait(false);
		}

		try
		{
			await Task.WhenAll(_trackedTasks).ConfigureAwait(false);
		}
		catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
		{
			// Expected during shutdown
		}
	}

	/// <summary>
	/// Builds the envelope and matching context to decrypt (or verify) a persisted encrypted field, reconstructing
	/// the exact associated data bound at encryption time from the envelope's own stamped values.
	/// </summary>
	private static (EncryptedData Envelope, EncryptionContext Context) BuildDecryptionEnvelope(
		string fieldName, EncryptedFieldResult encryptedField)
	{
		var keyName = GetKeyNameForClassification(encryptedField.Classification);
		var keyVersion = int.Parse(encryptedField.KeyVersion, CultureInfo.InvariantCulture);
		var algorithm = WireNameToAlgorithm(encryptedField.Algorithm);
		var associatedData = BuildFieldAssociatedData(
			fieldName, encryptedField.KeyVersion, encryptedField.Algorithm, encryptedField.Classification);

		var envelope = new EncryptedData
		{
			Ciphertext = Convert.FromBase64String(encryptedField.EncryptedValue),
			KeyId = keyName,
			KeyVersion = keyVersion,
			Algorithm = algorithm,
			Iv = Convert.FromBase64String(encryptedField.InitializationVector!),
			AuthTag = encryptedField.AuthenticationTag is { } tag ? Convert.FromBase64String(tag) : null,
		};

		var context = new EncryptionContext
		{
			KeyId = keyName,
			KeyVersion = keyVersion,
			Algorithm = algorithm,
			AssociatedData = associatedData,
		};

		return (envelope, context);
	}

	/// <summary>
	/// Builds the Associated Authenticated Data for a field, byte-identical on encrypt and decrypt.
	/// The four components are concatenated without a delimiter, so distinct contexts whose concatenations
	/// coincide produce the same associated data; changing the encoding would invalidate every stored
	/// envelope and so belongs to a format-version change rather than to this method.
	/// </summary>
	private static byte[] BuildFieldAssociatedData(
		string fieldName,
		string keyVersion,
		string algorithm,
		ElasticSearchDataClassification classification) =>
		Encoding.UTF8.GetBytes(
			string.Create(
				CultureInfo.InvariantCulture,
				$"{fieldName}{keyVersion}{algorithm}{(int)classification}"));

	/// <summary>
	/// Maps a Compliance encryption algorithm to the wire name persisted on <see cref="EncryptedFieldResult"/>.
	/// </summary>
	private static string AlgorithmToWireName(EncryptionAlgorithm algorithm) => algorithm switch
	{
		EncryptionAlgorithm.Aes256Gcm => "AES-256-GCM",
		EncryptionAlgorithm.Aes256CbcHmac => "AES-256-CBC-HMAC",
		_ => throw new SecurityException($"Unsupported encryption algorithm: {algorithm}"),
	};

	/// <summary>
	/// Maps a wire name persisted on <see cref="EncryptedFieldResult"/> back to a Compliance encryption algorithm.
	/// </summary>
	private static EncryptionAlgorithm WireNameToAlgorithm(string algorithm) => algorithm.ToUpperInvariant() switch
	{
		"AES-256-GCM" => EncryptionAlgorithm.Aes256Gcm,
		"AES-256-CBC-HMAC" => EncryptionAlgorithm.Aes256CbcHmac,
		_ => throw new SecurityException($"Unsupported encryption algorithm: {algorithm}"),
	};

	/// <summary>
	/// Checks if a field name indicates personally identifiable information.
	/// </summary>
	private static bool IsPiiField(string fieldName)
	{
		var piiPatterns = new[]
		{
			"email", "phone", "ssn", "social", "passport", "license", "credit", "card", "account", "name", "address", "zip", "postal",
			"birth", "dob", "age", "gender", "race", "ethnicity", "religion",
		};

		return piiPatterns.Any(pattern =>
			fieldName.Contains(pattern, StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// Checks if a field name indicates protected health information.
	/// </summary>
	private static bool IsHealthInformationField(string fieldName)
	{
		var phiPatterns = new[]
		{
			"medical", "health", "diagnosis", "treatment", "medication", "prescription", "patient", "doctor", "physician", "hospital",
			"clinic", "insurance", "medicare", "medicaid", "hipaa", "condition", "symptom", "allergy",
		};

		return phiPatterns.Any(pattern =>
			fieldName.Contains(pattern, StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// Gets the key name for a specific data classification level.
	/// </summary>
	private static string GetKeyNameForClassification(ElasticSearchDataClassification classification) =>
		$"elasticsearch-encryption-{classification.ToString().ToLowerInvariant()}";

	/// <summary>
	/// Processes a JSON element recursively to encrypt sensitive fields.
	/// </summary>
	[RequiresUnreferencedCode(
		"Calls Excalibur.Data.ElasticSearch.Security.Encryption.FieldEncryptor.EncryptFieldAsync(String, Object, ElasticSearchDataClassification, CancellationToken)")]
	[RequiresDynamicCode(
		"Calls Excalibur.Data.ElasticSearch.Security.Encryption.FieldEncryptor.EncryptFieldAsync(String, Object, ElasticSearchDataClassification, CancellationToken)")]
	private async Task ProcessJsonElementAsync(
		JsonElement element,
		Dictionary<string, object> result,
		string prefix,
		CancellationToken cancellationToken)
	{
		switch (element.ValueKind)
		{
			case JsonValueKind.Object:
				foreach (var property in element.EnumerateObject())
				{
					var fieldName = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";

					if (ShouldEncryptField(fieldName, property.Value))
					{
						var classification = GetFieldClassification(fieldName, property.Value);
						var encrypted = await EncryptFieldAsync(fieldName, property.Value, classification, cancellationToken)
							.ConfigureAwait(false);
						result[property.Name] = encrypted;
					}
					else
					{
						var nestedResult = new Dictionary<string, object>(StringComparer.Ordinal);
						await ProcessJsonElementAsync(property.Value, nestedResult, fieldName, cancellationToken).ConfigureAwait(false);
						result[property.Name] = nestedResult.Count > 0 ? nestedResult : GetJsonElementValue(property.Value);
					}
				}

				break;

			case JsonValueKind.Array:
				var array = new List<object>();
				var index = 0;
				foreach (var item in element.EnumerateArray())
				{
					var itemResult = new Dictionary<string, object>(StringComparer.Ordinal);
					await ProcessJsonElementAsync(item, itemResult, $"{prefix}[{index}]", cancellationToken).ConfigureAwait(false);
					array.Add(itemResult.Count > 0 ? itemResult : GetJsonElementValue(item));
					index++;
				}

				break;
			case JsonValueKind.Undefined:
			case JsonValueKind.String:
			case JsonValueKind.Number:
			case JsonValueKind.True:
			case JsonValueKind.False:
			case JsonValueKind.Null:
				break;
			default:
				break;
		}
	}

	/// <summary>
	/// Extracts the actual value from a JsonElement.
	/// </summary>
	private static object GetJsonElementValue(JsonElement element) =>
		element.ValueKind switch
		{
			JsonValueKind.String => element.GetString()!,
			JsonValueKind.Number => element.TryGetInt32(out var intVal) ? intVal : element.GetDouble(),
			JsonValueKind.True => true,
			JsonValueKind.False => false,
			JsonValueKind.Null => null!,
			_ => element.GetRawText(),
		};

	/// <summary>
	/// Builds regex patterns for field classification based on configuration rules.
	/// </summary>
	private Dictionary<ElasticSearchDataClassification, Regex> BuildClassificationPatterns()
	{
		var patterns = new Dictionary<ElasticSearchDataClassification, Regex>();

		foreach (var rule in _settings.ClassificationRules.Where(static r => r.Enabled))
		{
			try
			{
				patterns[rule.Classification] = new Regex(
					rule.FieldPattern,
					RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
			}
			catch (ArgumentException ex)
			{
				_logger.LogWarning(ex, "Invalid regex pattern for classification rule {Classification}: {Pattern}",
					rule.Classification, rule.FieldPattern);
			}
		}

		return patterns;
	}

	/// <summary>Clamps a timer interval to <see cref="System.Threading.Timer"/>'s maximum dueTime to avoid overflow.</summary>
	private static TimeSpan ClampToTimerMax(TimeSpan interval) =>
		interval > MaxTimerInterval ? MaxTimerInterval : interval;

	/// <summary>
	/// Performs scheduled key rotation for all data classification levels.
	/// </summary>
	private void PerformScheduledKeyRotation(object? state)
	{
		if (_disposed)
		{
			return;
		}

		// The timer may fire before the full configured interval has elapsed because each arm is clamped to Timer's
		// max dueTime. Only rotate once actually due; always re-arm for the remaining time to the next due point.
		if (DateTimeOffset.UtcNow < _nextKeyRotationDueUtc)
		{
			RearmKeyRotationTimer();
			return;
		}

		_nextKeyRotationDueUtc = DateTimeOffset.UtcNow + _keyRotationInterval;
		RearmKeyRotationTimer();

		var task = Task.Factory.StartNew(async () =>
		{
			try
			{
				var classifications = Enum.GetValues<ElasticSearchDataClassification>()
					.Where(static c => c != ElasticSearchDataClassification.Public);

				foreach (var classification in classifications)
				{
					_ = await RotateEncryptionKeysAsync(classification, CancellationToken.None).ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during scheduled key rotation");
			}
		}, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default).Unwrap();
		_trackedTasks.Add(task);

		// Drain completed tasks to prevent unbounded growth
		var snapshot = Interlocked.Exchange(ref _trackedTasks, new ConcurrentBag<Task>());
		foreach (var t in snapshot)
		{
			if (!t.IsCompleted)
			{
				_trackedTasks.Add(t);
			}
		}
	}

	/// <summary>
	/// Re-arms the rotation timer for the clamped remaining time until the next rotation is due, supporting
	/// configured intervals longer than <see cref="System.Threading.Timer"/>'s maximum dueTime.
	/// </summary>
	private void RearmKeyRotationTimer()
	{
		if (_disposed)
		{
			return;
		}

		var remaining = _nextKeyRotationDueUtc - DateTimeOffset.UtcNow;
		if (remaining < TimeSpan.Zero)
		{
			remaining = TimeSpan.Zero;
		}

		try
		{
			_ = _keyRotationTimer?.Change(ClampToTimerMax(remaining), Timeout.InfiniteTimeSpan);
		}
		catch (ObjectDisposedException)
		{
			// The timer was disposed concurrently (shutdown); nothing to re-arm.
		}
	}
}
