// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Security;

/// <summary>
/// Types of security events.
/// </summary>
public enum SecurityEventType
{
	/// <summary>
	/// Represents a successful authentication event.
	/// </summary>
	AuthenticationSuccess = 0,

	/// <summary>
	/// Represents a failed authentication attempt.
	/// </summary>
	AuthenticationFailure = 1,

	/// <summary>
	/// Represents a successful authorization event.
	/// </summary>
	AuthorizationSuccess = 2,

	/// <summary>
	/// Represents a failed authorization attempt.
	/// </summary>
	AuthorizationFailure = 3,

	/// <summary>
	/// Represents a validation failure event.
	/// </summary>
	ValidationFailure = 4,

	/// <summary>
	/// Represents a validation error event.
	/// </summary>
	ValidationError = 5,

	/// <summary>
	/// Represents a detected injection attempt.
	/// </summary>
	InjectionAttempt = 6,

	/// <summary>
	/// Represents a rate limit exceeded event.
	/// </summary>
	RateLimitExceeded = 7,

	/// <summary>
	/// Represents detection of suspicious activity.
	/// </summary>
	SuspiciousActivity = 8,

	/// <summary>
	/// Represents a potential data exfiltration attempt.
	/// </summary>
	DataExfiltrationAttempt = 9,

	/// <summary>
	/// Represents a configuration change event.
	/// </summary>
	ConfigurationChange = 10,

	/// <summary>
	/// Represents a credential rotation event.
	/// </summary>
	CredentialRotation = 11,

	/// <summary>
	/// Represents access to audit logs.
	/// </summary>
	AuditLogAccess = 12,

	/// <summary>
	/// Represents a security policy violation.
	/// </summary>
	SecurityPolicyViolation = 13,

	/// <summary>
	/// Represents an encryption failure event.
	/// </summary>
	EncryptionFailure = 14,

	/// <summary>
	/// Represents a decryption failure event.
	/// </summary>
	DecryptionFailure = 15,
}
