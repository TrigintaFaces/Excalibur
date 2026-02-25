// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace MessageEncryptionSample.Messages;

/// <summary>
/// Event representing a new customer registration with PII data.
/// This demonstrates field-level encryption for GDPR/PCI compliance.
/// </summary>
public sealed record CustomerCreatedEvent(
	string CustomerId,
	string Email,
	string EncryptedEmail,
	string EncryptedPhoneNumber,
	string EncryptedSocialSecurityNumber,
	DateTimeOffset CreatedAt) : IDispatchEvent;
