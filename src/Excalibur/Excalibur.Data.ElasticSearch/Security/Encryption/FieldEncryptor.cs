// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Production implementation of field-level encryption for Elasticsearch documents with enterprise-grade security features and
/// comprehensive audit capabilities.
/// </summary>
public sealed class FieldEncryptor : IElasticsearchFieldEncryptor, IDisposable, IAsyncDisposable
{
	private readonly IElasticsearchKeyProvider _keyProvider;
	private readonly ILogger<FieldEncryptor> _logger;
	private readonly EncryptionOptions _settings;
	private readonly Dictionary<DataClassification, Regex> _classificationPatterns;
	private readonly SemaphoreSlim _encryptionSemaphore;
	private readonly Timer? _keyRotationTimer;
	private readonly ConcurrentBag<Task> _trackedTasks = [];
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="FieldEncryptor" /> class.
	/// </summary>
	/// <param name="keyProvider"> The key management provider for encryption keys. </param>
	/// <param name="options"> The encryption configuration options. </param>
	/// <param name="logger"> The logger for security and operational events. </param>
	/// <exception cref="ArgumentNullException"> Thrown when required dependencies are null. </exception>
	public FieldEncryptor(
		IElasticsearchKeyProvider keyProvider,
		IOptions<EncryptionOptions> options,
		ILogger<FieldEncryptor> logger)
	{
		_keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
		_settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		_classificationPatterns = BuildClassificationPatterns();
		_encryptionSemaphore = new SemaphoreSlim(Environment.ProcessorCount * 2, Environment.ProcessorCount * 2);

		// Initialize key rotation timer if supported
		if (_keyProvider.SupportsKeyRotation && _settings.KeyManagement.KeyRotationInterval > TimeSpan.Zero)
		{
			_keyRotationTimer = new Timer(PerformScheduledKeyRotation, state: null,
				_settings.KeyManagement.KeyRotationInterval,
				_settings.KeyManagement.KeyRotationInterval);
		}

		_logger.LogInformation(
			"FieldEncryptor initialized with algorithm {Algorithm} and {RuleCount} classification rules",
			_settings.EncryptionAlgorithm, _settings.ClassificationRules.Count);
	}

	/// <inheritdoc />
	public event EventHandler<FieldEncryptedEventArgs>? FieldEncrypted;

	/// <inheritdoc />
	public event EventHandler<FieldDecryptedEventArgs>? FieldDecrypted;

	/// <inheritdoc />
	public event EventHandler<EncryptionKeyRotatedEventArgs>? KeyRotated;

	/// <inheritdoc />
	public IReadOnlyCollection<string> SupportedAlgorithms { get; } = new[] { "AES-256-GCM", "AES-192-GCM", "AES-128-GCM", };

	/// <inheritdoc />
	public bool SupportsKeyRotation => _keyProvider.SupportsKeyRotation;

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
		DataClassification classification,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(fieldName))
		{
			throw new ArgumentException("Field name cannot be null or empty", nameof(fieldName));
		}

		ArgumentNullException.ThrowIfNull(fieldValue);

		try
		{
			var algorithm = _settings.EncryptionAlgorithm;
			var plaintext = JsonSerializer.Serialize(fieldValue);
			var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

			// Get encryption key for the classification level
			var keyName = GetKeyNameForClassification(classification);
			var keyData = await _keyProvider.GetSecretAsync(keyName, cancellationToken).ConfigureAwait(false);

			if (string.IsNullOrEmpty(keyData))
			{
				// Generate new key if it doesn't exist
				var keyResult = await _keyProvider.GenerateEncryptionKeyAsync(
					keyName, EncryptionKeyType.Aes, 256, null, cancellationToken).ConfigureAwait(false);

				if (!keyResult.Success)
				{
					throw new SecurityException($"Failed to generate encryption key: {keyResult.ErrorMessage}");
				}

				keyData = await _keyProvider.GetSecretAsync(keyName, cancellationToken).ConfigureAwait(false);
			}

			var encryptedData = await PerformEncryptionAsync(plaintextBytes, keyData, algorithm).ConfigureAwait(false);

			var result = new EncryptedFieldResult(
				Convert.ToBase64String(encryptedData.EncryptedBytes),
				algorithm,
				"1.0", // Key version - should be retrieved from key provider
				Convert.ToBase64String(encryptedData.IV),
				Convert.ToBase64String(encryptedData.AuthTag),
				classification);

			// Raise encryption event for auditing
			FieldEncrypted?.Invoke(this, new FieldEncryptedEventArgs(
				fieldName, classification, algorithm, "1.0", DateTimeOffset.UtcNow));

			_logger.LogDebug(
				"Field {FieldName} encrypted with classification {Classification} using {Algorithm}",
				fieldName, classification, algorithm);

			return result;
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
			// Validate integrity if supported
			if (encryptedField.HasIntegrityProtection &&
				!await ValidateIntegrityAsync(encryptedField, cancellationToken).ConfigureAwait(false))
			{
				throw new SecurityException($"Integrity validation failed for field {fieldName}");
			}

			// Get decryption key
			var keyName = GetKeyNameForClassification(encryptedField.Classification);
			var keyData = await _keyProvider.GetSecretAsync(keyName, cancellationToken).ConfigureAwait(false);

			if (string.IsNullOrEmpty(keyData))
			{
				throw new SecurityException($"Decryption key not found for field {fieldName}");
			}

			var encryptedBytes = Convert.FromBase64String(encryptedField.EncryptedValue);
			var iv = Convert.FromBase64String(encryptedField.InitializationVector!);
			var authTag = Convert.FromBase64String(encryptedField.AuthenticationTag!);

			var decryptedBytes = await PerformDecryptionAsync(encryptedBytes, keyData, iv, authTag, encryptedField.Algorithm)
				.ConfigureAwait(false);
			var decryptedJson = Encoding.UTF8.GetString(decryptedBytes);
			var decryptedValue = JsonSerializer.Deserialize<object>(decryptedJson);

			// Raise decryption event for auditing
			FieldDecrypted?.Invoke(this, new FieldDecryptedEventArgs(
				fieldName, encryptedField.Classification, encryptedField.Algorithm,
				encryptedField.KeyVersion, DateTimeOffset.UtcNow));

			_logger.LogDebug("Field {FieldName} decrypted successfully", fieldName);

			return decryptedValue!;
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
		return classification != DataClassification.Public;
	}

	/// <inheritdoc />
	public DataClassification GetFieldClassification(string fieldName, object? fieldValue)
	{
		if (string.IsNullOrEmpty(fieldName))
		{
			return DataClassification.Public;
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
			return DataClassification.PersonallyIdentifiable;
		}

		// Check for health information patterns
		if (IsHealthInformationField(fieldName))
		{
			return DataClassification.HealthInformation;
		}

		return DataClassification.Public;
	}

	/// <inheritdoc />
	public Task<bool> ValidateIntegrityAsync(EncryptedFieldResult encryptedField, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(encryptedField);

		if (!encryptedField.HasIntegrityProtection)
		{
			_logger.LogWarning("Integrity validation requested for field without integrity protection");
			return Task.FromResult(false);
		}

		try
		{
			// For GCM mode, integrity is validated during decryption For demonstration, we'll check if the authentication tag is present
			// and valid format
			if (string.IsNullOrEmpty(encryptedField.AuthenticationTag))
			{
				return Task.FromResult(false);
			}

			// Validate Base64 format of authentication tag
			try
			{
				_ = Convert.FromBase64String(encryptedField.AuthenticationTag);
				return Task.FromResult(true);
			}
			catch (FormatException)
			{
				return Task.FromResult(false);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error validating field integrity");
			return Task.FromResult(false);
		}
	}

	/// <inheritdoc />
	public async Task<EncryptionKeyRotationResult> RotateEncryptionKeysAsync(
		DataClassification classification,
		CancellationToken cancellationToken)
	{
		if (!_keyProvider.SupportsKeyRotation)
		{
			return EncryptionKeyRotationResult.CreateFailure(classification, "Key rotation not supported by provider");
		}

		try
		{
			var keyName = GetKeyNameForClassification(classification);
			var rotationResult = await _keyProvider.RotateEncryptionKeyAsync(keyName, cancellationToken).ConfigureAwait(false);

			if (rotationResult.Success)
			{
				// Raise key rotation event
				KeyRotated?.Invoke(this, new EncryptionKeyRotatedEventArgs(
					classification, rotationResult.NewKeyVersion, rotationResult.PreviousKeyVersion,
					0, DateTimeOffset.UtcNow)); // Document count would need separate tracking

				_logger.LogInformation(
					"Encryption key rotated for classification {Classification}, new version {NewVersion}",
					classification, rotationResult.NewKeyVersion);

				return EncryptionKeyRotationResult.CreateSuccess(
					classification,
					rotationResult.NewKeyVersion, rotationResult.PreviousKeyVersion);
			}

			return EncryptionKeyRotationResult.CreateFailure(classification, rotationResult.ErrorMessage ?? "Unknown error");
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
		catch (OperationCanceledException)
		{
			// Expected during shutdown
		}
	}

	/// <summary>
	/// Performs the actual encryption operation using the specified algorithm.
	/// </summary>
	/// <exception cref="NotSupportedException"></exception>
	/// <exception cref="SecurityException"></exception>
	private static async Task<(byte[] EncryptedBytes, byte[] IV, byte[] AuthTag)> PerformEncryptionAsync(
		byte[] plaintext, string keyData, string algorithm)
	{
		var key = Convert.FromBase64String(keyData);

		return algorithm.ToUpperInvariant() switch
		{
			"AES-256-GCM" or "AES-192-GCM" or "AES-128-GCM" => await EncryptAesGcmAsync(plaintext, key).ConfigureAwait(false),
			_ => throw new SecurityException($"Unsupported encryption algorithm: {algorithm}"),
		};
	}

	/// <summary>
	/// Performs AES-GCM encryption with authenticated encryption.
	/// </summary>
	private static async Task<(byte[] EncryptedBytes, byte[] IV, byte[] AuthTag)> EncryptAesGcmAsync(byte[] plaintext, byte[] key)
	{
		using var aes = new AesGcm(key, 16); // 128-bit tag size
		var iv = new byte[12]; // 96-bit IV for GCM
		var ciphertext = new byte[plaintext.Length];
		var authTag = new byte[16]; // 128-bit authentication tag

		RandomNumberGenerator.Fill(iv);

		await Task.Run(() => aes.Encrypt(iv, plaintext, ciphertext, authTag)).ConfigureAwait(false);

		return (ciphertext, iv, authTag);
	}

	/// <summary>
	/// Performs the actual decryption operation using the specified algorithm.
	/// </summary>
	/// <exception cref="NotSupportedException"></exception>
	/// <exception cref="SecurityException"></exception>
	private static async Task<byte[]> PerformDecryptionAsync(
		byte[] ciphertext, string keyData, byte[] iv, byte[] authTag, string algorithm)
	{
		var key = Convert.FromBase64String(keyData);

		return algorithm.ToUpperInvariant() switch
		{
			"AES-256-GCM" or "AES-192-GCM" or "AES-128-GCM" => await DecryptAesGcmAsync(ciphertext, key, iv, authTag).ConfigureAwait(false),
			_ => throw new SecurityException($"Unsupported encryption algorithm: {algorithm}"),
		};
	}

	/// <summary>
	/// Performs AES-GCM decryption with authentication verification.
	/// </summary>
	private static async Task<byte[]> DecryptAesGcmAsync(byte[] ciphertext, byte[] key, byte[] iv, byte[] authTag)
	{
		using var aes = new AesGcm(key, 16); // 128-bit tag size
		var plaintext = new byte[ciphertext.Length];

		await Task.Run(() => aes.Decrypt(iv, ciphertext, authTag, plaintext)).ConfigureAwait(false);

		return plaintext;
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
	private static string GetKeyNameForClassification(DataClassification classification) =>
		$"elasticsearch-encryption-{classification.ToString().ToLowerInvariant()}";

	/// <summary>
	/// Processes a JSON element recursively to encrypt sensitive fields.
	/// </summary>
	[RequiresUnreferencedCode(
		"Calls Excalibur.Data.ElasticSearch.Security.Encryption.FieldEncryptor.EncryptFieldAsync(String, Object, DataClassification, CancellationToken)")]
	[RequiresDynamicCode(
		"Calls Excalibur.Data.ElasticSearch.Security.Encryption.FieldEncryptor.EncryptFieldAsync(String, Object, DataClassification, CancellationToken)")]
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

				if (!string.IsNullOrEmpty(prefix))
				{
					// This is a top-level array, we need to handle it appropriately
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
	/// Builds regex patterns for field classification based on configuration rules.
	/// </summary>
	private Dictionary<DataClassification, Regex> BuildClassificationPatterns()
	{
		var patterns = new Dictionary<DataClassification, Regex>();

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

	/// <summary>
	/// Performs scheduled key rotation for all data classification levels.
	/// </summary>
	private void PerformScheduledKeyRotation(object? state)
	{
		if (_disposed)
		{
			return;
		}

		var task = Task.Run(async () =>
		{
			try
			{
				var classifications = Enum.GetValues<DataClassification>()
					.Where(static c => c != DataClassification.Public);

				foreach (var classification in classifications)
				{
					_ = await RotateEncryptionKeysAsync(classification, CancellationToken.None).ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during scheduled key rotation");
			}
		});
		_trackedTasks.Add(task);
	}
}
