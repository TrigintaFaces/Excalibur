// Copyright (c) 2026 Garnet Labs
// This software is released under the MIT License.
// https://opensource.org/licenses/MIT

using Excalibur.Dispatch.CloudNative.WASM.Crypto;
using Excalibur.Dispatch.CloudNative.WASM.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace examples.CloudNative.WASM.Crypto;

/// <summary>
/// Comprehensive example demonstrating browser crypto API integration.
/// </summary>
public class BrowserCryptoExample
{
	/// <summary>
	/// Runs the browser crypto example.
	/// </summary>
	public static async Task RunAsync()
	{
		Console.WriteLine("=== Browser Crypto API Integration Example ===\n");

		var services = new ServiceCollection();
		ConfigureServices(services);

		var provider = services.BuildServiceProvider();

		// Run examples
		await DemonstrateKeyManagement(provider);
		await DemonstrateMessageEncryption(provider);
		await DemonstrateEndToEndEncryption(provider);
		await DemonstrateKeyPersistence(provider);
		await DemonstrateSecureMessaging(provider);
	}

	private static void ConfigureServices(IServiceCollection services)
	{
		// Add logging
		services.AddLogging(builder => builder
		.SetMinimumLevel(LogLevel.Debug)
		.AddConsole());

		// Add WASM serialization
		services.AddWasmSerialization(options =>
		{
			options.EnableCompression = true;
			options.DefaultFormat = MessageFormat.Json;
		});

		// Add browser crypto with comprehensive configuration
		services.AddWasmCrypto(options =>
		{
			options.DefaultEncryptionAlgorithm = EncryptionAlgorithm.AesGcm;
			options.DefaultSigningAlgorithm = SigningAlgorithm.Ecdsa;
			options.DefaultKeySize = 256;
			options.EnableKeyPersistence = true;
			options.AutoGenerateKeys = true;

			// Configure security policy
			options.SecurityPolicy = new CryptoSecurityPolicy
			{
				MinimumKeySize = 256,
				RequireAuthenticatedEncryption = true,
				EnforceKeyExpiry = true,
				MaximumKeyAge = TimeSpan.FromDays(90)
			};

			// Configure key rotation
			options.KeyRotationPolicy = new KeyRotationPolicy
			{
				Enabled = true,
				RotationInterval = TimeSpan.FromDays(30),
				GracePeriod = TimeSpan.FromDays(7),
				AutoRotateOnExpiry = true
			};
		});

		// Add secure message dispatcher
		services.AddSecureWasmDispatcher(options =>
		{
			options.RequireEncryption = true;
			options.RequireSignatures = true;
			options.DefaultEncryptionKeyAlias = "app-encryption-key";
			options.DefaultSigningKeyAlias = "app-signing-key";

			// Configure message types requiring security
			options.MessageTypesRequiringEncryption.Add("UserCredentials");
			options.MessageTypesRequiringEncryption.Add("PaymentInfo");
			options.MessageTypesRequiringSignatures.Add("TransactionRequest");
			options.MessageTypesRequiringSignatures.Add("AuthorizationGrant");
		});

		// Add automatic key generation
		services.AddAutoKeyGeneration(options =>
		{
			options.Keys.Add(new AutoKeyDefinition
			{
				Alias = "app-encryption-key",
				Type = KeyGenerationType.Symmetric,
				Algorithm = "AES-GCM",
				KeySize = 256,
				Purpose = "Application message encryption",
				Usages = new[] { KeyUsage.Encrypt, KeyUsage.Decrypt }
			});

			options.Keys.Add(new AutoKeyDefinition
			{
				Alias = "app-signing-key",
				Type = KeyGenerationType.AsymmetricPair,
				Algorithm = "ECDSA",
				Purpose = "Application message signing",
				Usages = new[] { KeyUsage.Sign, KeyUsage.Verify }
			});

			options.Keys.Add(new AutoKeyDefinition
			{
				Alias = "user-data-key",
				Type = KeyGenerationType.Symmetric,
				Algorithm = "AES-GCM",
				KeySize = 256,
				Purpose = "User data encryption",
				Extractable = true,
				Usages = new[] { KeyUsage.Encrypt, KeyUsage.Decrypt }
			});
		});

		// Add encrypted message serialization
		services.AddEncryptedMessageSerialization(SerializerType.Json);

		// Add base message dispatcher
		services.AddSingleton<IWasmMessageDispatcher, WasmMessageDispatcher>();
	}

	private static async Task DemonstrateKeyManagement(IServiceProvider provider)
	{
		Console.WriteLine("\n1. Key Management Demo");
		Console.WriteLine("======================");

		var cryptoProvider = provider.GetRequiredService<IWasmCryptoProvider>();
		var keyManager = provider.GetRequiredService<IWasmKeyManager>();
		var logger = provider.GetRequiredService<ILogger<BrowserCryptoExample>>();

		// Generate a symmetric key
		Console.WriteLine("\nGenerating AES-256-GCM key...");
		var symmetricKey = await cryptoProvider.GenerateKeyAsync(
		CryptoAlgorithm.AesGcm(256),
		new[] { KeyUsage.Encrypt, KeyUsage.Decrypt },
		true);

		await keyManager.StoreKeyAsync(symmetricKey.Id, symmetricKey, new KeyMetadata
		{
			KeyId = symmetricKey.Id,
			Alias = "demo-symmetric-key",
			Purpose = "Demo encryption key",
			Algorithm = "AES-GCM",
			Tags = new Dictionary<string, string>
			{
				["demo"] = "true",
				["type"] = "symmetric"
			}
		});

		Console.WriteLine($"✓ Generated symmetric key: {symmetricKey.Id}");

		// Generate an asymmetric key pair
		Console.WriteLine("\nGenerating ECDSA P-256 key pair...");
		var keyPair = await cryptoProvider.GenerateKeyPairAsync(
		CryptoAlgorithm.Ecdsa("P-256"),
		new[] { KeyUsage.Sign, KeyUsage.Verify },
		true);

		await keyManager.StoreKeyAsync(keyPair.PublicKey.Id, keyPair.PublicKey, new KeyMetadata
		{
			KeyId = keyPair.PublicKey.Id,
			Alias = "demo-public-key",
			Purpose = "Demo verification key",
			Algorithm = "ECDSA",
			Tags = new Dictionary<string, string>
			{
				["demo"] = "true",
				["type"] = "public"
			}
		});

		await keyManager.StoreKeyAsync(keyPair.PrivateKey.Id, keyPair.PrivateKey, new KeyMetadata
		{
			KeyId = keyPair.PrivateKey.Id,
			Alias = "demo-private-key",
			Purpose = "Demo signing key",
			Algorithm = "ECDSA",
			Tags = new Dictionary<string, string>
			{
				["demo"] = "true",
				["type"] = "private"
			}
		});

		Console.WriteLine($"✓ Generated key pair:");
		Console.WriteLine($" Public: {keyPair.PublicKey.Id}");
		Console.WriteLine($" Private: {keyPair.PrivateKey.Id}");

		// List all keys
		Console.WriteLine("\nListing all keys:");
		var keyIds = await keyManager.ListKeyIdsAsync();
		foreach (var keyId in keyIds)
		{
			var metadata = await keyManager.GetKeyMetadataAsync(keyId);
			Console.WriteLine($" - {metadata?.Alias ?? keyId}: {metadata?.Purpose}");
		}

		// Demonstrate key derivation
		Console.WriteLine("\nDeriving key using PBKDF2...");
		var password = "DemoPassword123!";
		var salt = cryptoProvider.GetRandomValues(16);

		var derivedKey = await cryptoProvider.DeriveKeyAsync(
		CryptoAlgorithm.Pbkdf2(100000, salt),
		await cryptoProvider.ImportKeyAsync(
		KeyFormat.Raw,
		System.Text.Encoding.UTF8.GetBytes(password),
		new CryptoAlgorithm { Name = "PBKDF2" },
		false,
		new[] { KeyUsage.DeriveKey }),
		CryptoAlgorithm.AesGcm(256),
		false,
		new[] { KeyUsage.Encrypt, KeyUsage.Decrypt });

		Console.WriteLine($"✓ Derived AES key from password");
	}

	private static async Task DemonstrateMessageEncryption(IServiceProvider provider)
	{
		Console.WriteLine("\n2. Message Encryption Demo");
		Console.WriteLine("==========================");

		var encryptedSerializer = provider.GetRequiredService<IEncryptedMessageSerializer>();
		var keyManager = provider.GetRequiredService<IWasmKeyManager>();

		// Get the encryption key
		var encryptionKey = await keyManager.GetKeyByAliasAsync("demo-symmetric-key");
		if (encryptionKey == null)
		{
			Console.WriteLine("! Encryption key not found");
			return;
		}

		// Create a sample message
		var message = new SampleMessage
		{
			Id = Guid.NewGuid(),
			Content = "This is a secret message!",
			Timestamp = DateTimeOffset.UtcNow,
			Metadata = new Dictionary<string, string>
			{
				["sender"] = "Alice",
				["recipient"] = "Bob"
			}
		};

		Console.WriteLine($"\nOriginal message:");
		Console.WriteLine($" ID: {message.Id}");
		Console.WriteLine($" Content: {message.Content}");

		// Encrypt the message
		Console.WriteLine("\nEncrypting message...");
		var encrypted = await encryptedSerializer.EncryptAndSerializeAsync(
		message,
		encryptionKey.Id,
		new EncryptionOptions
		{
			Algorithm = EncryptionAlgorithm.AesGcm,
			KeySize = 256,
			AdditionalData = System.Text.Encoding.UTF8.GetBytes("demo-context")
		});

		Console.WriteLine($"✓ Encrypted size: {encrypted.Length} bytes");
		Console.WriteLine($" Base64: {Convert.ToBase64String(encrypted.Take(32).ToArray())}...");

		// Decrypt the message
		Console.WriteLine("\nDecrypting message...");
		var decrypted = await encryptedSerializer.DeserializeAndDecryptAsync<SampleMessage>(
		encrypted,
		encryptionKey.Id,
		new DecryptionOptions
		{
			AdditionalData = System.Text.Encoding.UTF8.GetBytes("demo-context")
		});

		Console.WriteLine($"✓ Decrypted message:");
		Console.WriteLine($" ID: {decrypted.Id}");
		Console.WriteLine($" Content: {decrypted.Content}");
		Console.WriteLine($" Match: {message.Id == decrypted.Id && message.Content == decrypted.Content}");
	}

	private static async Task DemonstrateEndToEndEncryption(IServiceProvider provider)
	{
		Console.WriteLine("\n3. End-to-End Encryption Demo");
		Console.WriteLine("==============================");

		var messageCrypto = provider.GetRequiredService<IWasmMessageCrypto>();
		var keyManager = provider.GetRequiredService<IWasmKeyManager>();
		var cryptoProvider = provider.GetRequiredService<IWasmCryptoProvider>();

		// Generate sender and recipient key pairs
		Console.WriteLine("\nGenerating sender key pair...");
		var senderKeyPair = await cryptoProvider.GenerateKeyPairAsync(
		CryptoAlgorithm.RsaOaep(),
		new[] { KeyUsage.Encrypt, KeyUsage.Decrypt },
		true);

		Console.WriteLine("Generating recipient key pair...");
		var recipientKeyPair = await cryptoProvider.GenerateKeyPairAsync(
		CryptoAlgorithm.RsaOaep(),
		new[] { KeyUsage.Encrypt, KeyUsage.Decrypt },
		true);

		// Store keys
		await keyManager.StoreKeyAsync("sender-private", senderKeyPair.PrivateKey);
		await keyManager.StoreKeyAsync("sender-public", senderKeyPair.PublicKey);
		await keyManager.StoreKeyAsync("recipient-private", recipientKeyPair.PrivateKey);
		await keyManager.StoreKeyAsync("recipient-public", recipientKeyPair.PublicKey);

		// Create a message envelope
		var envelope = new WasmMessageEnvelope
		{
			MessageId = Guid.NewGuid().ToString(),
			MessageType = "SecureMessage",
			Timestamp = DateTimeOffset.UtcNow,
			Headers = new Dictionary<string, string>
			{
				["sender"] = "Alice",
				["recipient"] = "Bob"
			},
			Payload = new
			{
				message = "This is an end-to-end encrypted message",
				data = new byte[] { 1, 2, 3, 4, 5 }
			}
		};

		Console.WriteLine($"\nOriginal envelope:");
		Console.WriteLine($" ID: {envelope.MessageId}");
		Console.WriteLine($" Type: {envelope.MessageType}");

		// Sign with sender's private key
		Console.WriteLine("\nSigning message...");
		var signingKey = await keyManager.GetKeyByAliasAsync("demo-private-key") ?? senderKeyPair.PrivateKey;
		var signedEnvelope = await messageCrypto.SignMessageAsync(envelope, signingKey.Id);
		Console.WriteLine("✓ Message signed");

		// Encrypt with recipient's public key
		Console.WriteLine("\nEncrypting message...");
		var encryptedEnvelope = await messageCrypto.EncryptMessageAsync(
		signedEnvelope,
		recipientKeyPair.PublicKey.Id,
		new MessageCryptoOptions
		{
			Algorithm = CryptoAlgorithmType.RsaOaep,
			CompressBeforeEncrypt = true
		});
		Console.WriteLine("✓ Message encrypted");

		// Decrypt with recipient's private key
		Console.WriteLine("\nDecrypting message...");
		var decryptedEnvelope = await messageCrypto.DecryptMessageAsync(
		encryptedEnvelope,
		recipientKeyPair.PrivateKey.Id);
		Console.WriteLine("✓ Message decrypted");

		// Verify with sender's public key
		Console.WriteLine("\nVerifying signature...");
		var verificationKey = await keyManager.GetKeyByAliasAsync("demo-public-key") ?? senderKeyPair.PublicKey;
		var isValid = await messageCrypto.VerifyMessageAsync(decryptedEnvelope, verificationKey.Id);
		Console.WriteLine($"✓ Signature valid: {isValid}");
	}

	private static async Task DemonstrateKeyPersistence(IServiceProvider provider)
	{
		Console.WriteLine("\n4. Key Persistence Demo");
		Console.WriteLine("========================");

		var keyStore = provider.GetRequiredService<ICryptoKeyStore>();
		var keyManager = provider.GetRequiredService<IWasmKeyManager>();

		// Store key metadata
		Console.WriteLine("\nStoring key metadata in IndexedDB...");
		await keyStore.SetMetadataAsync("demo-config", new
		{
			version = "1.0",
			createdAt = DateTimeOffset.UtcNow,
			settings = new
			{
				encryptionEnabled = true,
				algorithm = "AES-GCM-256"
			}
		});
		Console.WriteLine("✓ Metadata stored");

		// List keys with filtering
		Console.WriteLine("\nQuerying keys by type...");
		var symmetricKeys = await keyStore.ListKeysAsync(new KeyFilter
		{
			Type = "secret",
			Algorithm = "AES-GCM"
		});

		Console.WriteLine($"Found {symmetricKeys.Count} symmetric keys:");
		foreach (var key in symmetricKeys)
		{
			Console.WriteLine($" - {key.Metadata.Alias}: Last used {key.LastUsed:g}");
		}

		// Export keys for backup
		Console.WriteLine("\nExporting keys for backup...");
		var backup = await keyManager.ExportKeysAsync("DemoBackupPassword123!");
		Console.WriteLine($"✓ Exported {backup.Keys.Count} keys");
		Console.WriteLine($" Encrypted: {backup.Encrypted}");
		Console.WriteLine($" Version: {backup.Version}");

		// Simulate key import (in real scenario, this would be on a different session)
		Console.WriteLine("\nSimulating key import...");
		await keyManager.ImportKeysAsync(backup, "DemoBackupPassword123!");
		Console.WriteLine("✓ Keys imported successfully");
	}

	private static async Task DemonstrateSecureMessaging(IServiceProvider provider)
	{
		Console.WriteLine("\n5. Secure Messaging Demo");
		Console.WriteLine("=========================");

		var dispatcher = provider.GetRequiredService<IWasmMessageDispatcher>();

		// Register secure message handler
		dispatcher.RegisterHandler<SecurePayment>(async (payment, ct) =>
		{
			Console.WriteLine($"\nReceived secure payment:");
			Console.WriteLine($" Amount: ${payment.Amount}");
			Console.WriteLine($" From: {payment.From}");
			Console.WriteLine($" To: {payment.To}");
			await Task.CompletedTask;
		});

		// Send a secure message
		var securePayment = new SecurePayment
		{
			Id = Guid.NewGuid(),
			Amount = 100.50m,
			Currency = "USD",
			From = "Alice",
			To = "Bob",
			Timestamp = DateTimeOffset.UtcNow
		};

		Console.WriteLine("\nSending secure payment message...");
		var envelope = await dispatcher.SendAsync(securePayment, new Dictionary<string, string>
		{
			["RequireEncryption"] = "true",
			["RequireSignature"] = "true"
		});

		Console.WriteLine($"✓ Sent message: {envelope.MessageId}");

		// Dispatch the message
		Console.WriteLine("\nDispatching message through secure pipeline...");
		var result = await dispatcher.DispatchAsync(envelope);
		Console.WriteLine($"✓ Dispatch result: {(result.Success ? "Success" : $"Failed - {result.Error}")}");

		// Demonstrate request/response pattern
		Console.WriteLine("\nTesting secure request/response...");
		dispatcher.RegisterHandler<BalanceRequest, BalanceResponse>(async (request, ct) =>
		{
			await Task.Delay(100, ct); // Simulate processing
			return new BalanceResponse
			{
				AccountId = request.AccountId,
				Balance = 1234.56m,
				Currency = "USD",
				AsOf = DateTimeOffset.UtcNow
			};
		});

		try
		{
			var response = await dispatcher.RequestAsync<BalanceResponse>(
			new BalanceRequest { AccountId = "ACC-12345" },
			TimeSpan.FromSeconds(5));

			Console.WriteLine($"✓ Received response:");
			Console.WriteLine($" Balance: ${response?.Balance}");
			Console.WriteLine($" As of: {response?.AsOf:g}");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"✗ Request failed: {ex.Message}");
		}
	}
}

// Sample message types
public class SampleMessage
{
	public Guid Id { get; set; }
	public string Content { get; set; } = string.Empty;
	public DateTimeOffset Timestamp { get; set; }
	public Dictionary<string, string> Metadata { get; set; } = new();
}

public class SecurePayment
{
	public Guid Id { get; set; }
	public decimal Amount { get; set; }
	public string Currency { get; set; } = "USD";
	public string From { get; set; } = string.Empty;
	public string To { get; set; } = string.Empty;
	public DateTimeOffset Timestamp { get; set; }
}

public class BalanceRequest
{
	public string AccountId { get; set; } = string.Empty;
}

public class BalanceResponse
{
	public string AccountId { get; set; } = string.Empty;
	public decimal Balance { get; set; }
	public string Currency { get; set; } = "USD";
	public DateTimeOffset AsOf { get; set; }
}