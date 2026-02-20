// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// Message Encryption Sample
// =========================
// This sample demonstrates how to use Excalibur.Dispatch.Security for message encryption:
// - Payload encryption/decryption using DataProtection API
// - Field-level encryption for sensitive data (PII, PCI)
// - Key rotation patterns
//
// Prerequisites:
// 1. Run the sample: dotnet run

#pragma warning disable CA1303 // Sample code uses literal strings
#pragma warning disable CA1506 // Sample has high coupling by design

using Excalibur.Data.InMemory.Inbox;
using Excalibur.Data.InMemory.Outbox;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Compliance;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Security;
using Excalibur.Dispatch.Serialization;

using MessageEncryptionSample.Messages;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Build configuration
var builder = new HostApplicationBuilder(args);

// Add appsettings.json configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

// Configure logging for visibility
builder.Services.AddLogging(logging =>
{
	_ = logging.AddConsole();
	_ = logging.SetMinimumLevel(LogLevel.Information);
});

// ============================================================
// Configure Dispatch with security
// ============================================================
builder.Services.AddDispatch(dispatch =>
{
	_ = dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

	// Register JSON serializer for message payloads
	_ = dispatch.AddDispatchSerializer<DispatchJsonSerializer>(version: 0);
});

// ============================================================
// Configure message encryption using DataProtection API
// ============================================================
// The DataProtection API provides:
// - Automatic key management and rotation
// - Cryptographically secure encryption
// - Works without external dependencies (local key store)
builder.Services.AddDataProtection();
builder.Services.AddSingleton<IMessageEncryptionService, DataProtectionMessageEncryptionService>();

// ============================================================
// Configure outbox/inbox for reliable messaging
// ============================================================
builder.Services.AddOutbox<InMemoryOutboxStore>();
builder.Services.AddInbox<InMemoryInboxStore>();
builder.Services.AddOutboxHostedService();
builder.Services.AddInboxHostedService();

// ============================================================
// Build and start the host
// ============================================================
using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var encryptionService = host.Services.GetRequiredService<IMessageEncryptionService>();
var dispatcher = host.Services.GetRequiredService<IDispatcher>();
var dispatchContext = DispatchContextInitializer.CreateDefaultContext();

logger.LogInformation("Starting Message Encryption Sample...");
logger.LogInformation("Demonstrating field-level encryption for sensitive data");

await host.StartAsync().ConfigureAwait(false);

// ============================================================
// Create encryption contexts for different purposes
// ============================================================
// PII context for personal information
var piiContext = new EncryptionContext { Purpose = "pii-field", Classification = DataClassification.Confidential };

// PCI context for payment card data (stricter requirements)
var pciContext = new EncryptionContext
{
	Purpose = "pci-data",
	Classification = DataClassification.Restricted,
	RequireFipsCompliance = true
};

// ============================================================
// Demonstrate field-level encryption
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Field-Level Encryption Demo ===");

// Simulate sensitive customer data
var customerId = Guid.NewGuid().ToString();
var email = "customer@example.com";
var phoneNumber = "+1-555-123-4567";
var socialSecurityNumber = "123-45-6789";

logger.LogInformation("Original customer data:");
logger.LogInformation("  Email: {Email}", email);
logger.LogInformation("  Phone: {Phone}", phoneNumber);
logger.LogInformation("  SSN: {SSN}", socialSecurityNumber);

// Encrypt sensitive fields before storing/transmitting
var encryptedEmail = await encryptionService.EncryptMessageAsync(email, piiContext, CancellationToken.None).ConfigureAwait(false);
var encryptedPhone = await encryptionService.EncryptMessageAsync(phoneNumber, piiContext, CancellationToken.None).ConfigureAwait(false);
var encryptedSsn = await encryptionService.EncryptMessageAsync(socialSecurityNumber, piiContext, CancellationToken.None)
	.ConfigureAwait(false);

logger.LogInformation("");
logger.LogInformation("Encrypted fields (safe to store/transmit):");
logger.LogInformation("  Email: {EncryptedEmail}", encryptedEmail[..Math.Min(50, encryptedEmail.Length)] + "...");
logger.LogInformation("  Phone: {EncryptedPhone}", encryptedPhone[..Math.Min(50, encryptedPhone.Length)] + "...");
logger.LogInformation("  SSN: {EncryptedSSN}", encryptedSsn[..Math.Min(50, encryptedSsn.Length)] + "...");

// Create event with encrypted fields
var customerEvent = new CustomerCreatedEvent(
	CustomerId: customerId,
	Email: MaskEmail(email), // Store masked version for display
	EncryptedEmail: encryptedEmail,
	EncryptedPhoneNumber: encryptedPhone,
	EncryptedSocialSecurityNumber: encryptedSsn,
	CreatedAt: DateTimeOffset.UtcNow);

await dispatcher.DispatchAsync(customerEvent, dispatchContext, cancellationToken: default).ConfigureAwait(false);
logger.LogInformation("");
logger.LogInformation("CustomerCreatedEvent dispatched with encrypted PII");

// ============================================================
// Demonstrate payment data encryption (PCI compliance)
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Payment Data Encryption Demo (PCI) ===");

var paymentId = Guid.NewGuid().ToString();
var cardNumber = "4111111111111111"; // Test card number
var cardData = $"{{\"number\":\"{cardNumber}\",\"exp\":\"12/26\",\"cvv\":\"123\"}}";

// Encrypt full card data with PCI context
var encryptedCardData = await encryptionService.EncryptMessageAsync(cardData, pciContext, CancellationToken.None).ConfigureAwait(false);

var paymentEvent = new PaymentProcessedEvent(
	PaymentId: paymentId,
	CustomerId: customerId,
	Amount: 99.99m,
	Currency: "USD",
	MaskedCardNumber: MaskCardNumber(cardNumber), // Only last 4 digits visible
	EncryptedCardData: encryptedCardData,
	ProcessedAt: DateTimeOffset.UtcNow);

logger.LogInformation("Payment card: {MaskedCard}", paymentEvent.MaskedCardNumber);
logger.LogInformation("Encrypted card data: {EncryptedData}", encryptedCardData[..Math.Min(50, encryptedCardData.Length)] + "...");

await dispatcher.DispatchAsync(paymentEvent, dispatchContext, cancellationToken: default).ConfigureAwait(false);
logger.LogInformation("PaymentProcessedEvent dispatched with encrypted card data");

// ============================================================
// Demonstrate decryption (authorized access only)
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Decryption Demo (Authorized Access) ===");

// Decrypt for authorized operations using the same context
var decryptedEmail = await encryptionService.DecryptMessageAsync(encryptedEmail, piiContext, CancellationToken.None).ConfigureAwait(false);
var decryptedSsn = await encryptionService.DecryptMessageAsync(encryptedSsn, piiContext, CancellationToken.None).ConfigureAwait(false);
var decryptedCardData =
	await encryptionService.DecryptMessageAsync(encryptedCardData, pciContext, CancellationToken.None).ConfigureAwait(false);

logger.LogInformation("Decrypted email: {Email}", decryptedEmail);
logger.LogInformation("Decrypted SSN: {SSN}", decryptedSsn);
logger.LogInformation("Decrypted card data: {CardData}", decryptedCardData);

// ============================================================
// Demonstrate key rotation
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Key Rotation Demo ===");

// Note: DataProtection handles key rotation automatically
// Keys are rotated every 90 days by default
// Old keys are retained for decryption of existing data
logger.LogInformation("DataProtection API handles key rotation automatically:");
logger.LogInformation("  - New keys generated every 90 days");
logger.LogInformation("  - Old keys retained for decryption");
logger.LogInformation("  - No downtime during rotation");

// Manual key rotation can be triggered if needed
logger.LogInformation("");
logger.LogInformation("Triggering manual key rotation...");
var rotationResult = await encryptionService.RotateKeysAsync(CancellationToken.None).ConfigureAwait(false);
logger.LogInformation("Key rotation completed: Success={Success}, NewKeyId={NewKeyId}",
	rotationResult.Success,
	rotationResult.NewKey?.KeyId ?? "N/A");

// Verify old data can still be decrypted after rotation
var verifyDecrypt = await encryptionService.DecryptMessageAsync(encryptedEmail, piiContext, CancellationToken.None).ConfigureAwait(false);
logger.LogInformation("Verified: Old encrypted data decrypts correctly after rotation: {Email}", verifyDecrypt);

// ============================================================
// Demonstrate tenant isolation
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Tenant Isolation Demo ===");

var tenant1Context = EncryptionContext.ForTenant("tenant-001");
var tenant2Context = EncryptionContext.ForTenant("tenant-002");

var sensitiveData = "Cross-tenant data test";
var tenant1Encrypted =
	await encryptionService.EncryptMessageAsync(sensitiveData, tenant1Context, CancellationToken.None).ConfigureAwait(false);

logger.LogInformation("Data encrypted for tenant-001");
logger.LogInformation("  Original: {Data}", sensitiveData);
logger.LogInformation("  Encrypted: {Encrypted}", tenant1Encrypted[..Math.Min(50, tenant1Encrypted.Length)] + "...");

// Decrypt with correct tenant context
var tenant1Decrypted = await encryptionService.DecryptMessageAsync(tenant1Encrypted, tenant1Context, CancellationToken.None)
	.ConfigureAwait(false);
logger.LogInformation("Decrypted with tenant-001 context: {Data}", tenant1Decrypted);

// Note: In production, attempting to decrypt with wrong tenant context would fail
logger.LogInformation("Tenant isolation ensures cryptographic separation");

// ============================================================
// Summary
// ============================================================
logger.LogInformation("");
logger.LogInformation("=== Summary ===");
logger.LogInformation("This sample demonstrated:");
logger.LogInformation("  1. Field-level encryption for PII (email, phone, SSN)");
logger.LogInformation("  2. Payment data encryption for PCI compliance");
logger.LogInformation("  3. Secure decryption for authorized operations");
logger.LogInformation("  4. Automatic key rotation with DataProtection API");
logger.LogInformation("  5. Tenant isolation for multi-tenant scenarios");
logger.LogInformation("");
logger.LogInformation("Best Practices:");
logger.LogInformation("  - Encrypt sensitive fields before storage/transmission");
logger.LogInformation("  - Store masked versions for display purposes");
logger.LogInformation("  - Decrypt only when absolutely necessary");
logger.LogInformation("  - Use transport encryption (TLS) in addition to field encryption");
logger.LogInformation("  - Use tenant-specific contexts for multi-tenant isolation");
logger.LogInformation("");
logger.LogInformation("Press Ctrl+C to exit...");

// Wait for shutdown signal
await host.WaitForShutdownAsync().ConfigureAwait(false);

// ============================================================
// Helper methods
// ============================================================
static string MaskEmail(string email)
{
	var atIndex = email.IndexOf('@', StringComparison.Ordinal);
	if (atIndex <= 1)
	{
		return email;
	}

	return email[0] + new string('*', atIndex - 1) + email[atIndex..];
}

static string MaskCardNumber(string cardNumber)
{
	if (cardNumber.Length < 4)
	{
		return cardNumber;
	}

	return new string('*', cardNumber.Length - 4) + cardNumber[^4..];
}

#pragma warning restore CA1506
#pragma warning restore CA1303
