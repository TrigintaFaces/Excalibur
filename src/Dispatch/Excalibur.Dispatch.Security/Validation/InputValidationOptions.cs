// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Configuration options for input validation.
/// </summary>
public sealed class InputValidationOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether input validation is enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if input validation is enabled; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
	/// </value>
	public bool EnableValidation { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether null properties are allowed in messages.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if null properties are allowed in messages; otherwise, <see langword="false"/>. The default is <see langword="false"/>.
	/// </value>
	public bool AllowNullProperties { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether empty strings are allowed in message properties.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if empty strings are allowed in message properties; otherwise, <see langword="false"/>. The default is <see langword="false"/>.
	/// </value>
	public bool AllowEmptyStrings { get; set; }

	/// <summary>
	/// Gets or sets the maximum allowed length for string properties.
	/// </summary>
	/// <value>
	/// The maximum allowed length for string properties. The default is 10000.
	/// </value>
	[Range(1, int.MaxValue)]
	public int MaxStringLength { get; set; } = 10000;

	/// <summary>
	/// Gets or sets the maximum allowed message size in bytes.
	/// </summary>
	/// <value>
	/// The maximum allowed message size in bytes. The default is 1048576 (1 MB).
	/// </value>
	[Range(1, int.MaxValue)]
	public int MaxMessageSizeBytes { get; set; } = 1048576; // 1MB

	/// <summary>
	/// Gets or sets the maximum allowed depth for nested objects in messages.
	/// </summary>
	/// <value>
	/// The maximum allowed depth for nested objects in messages. The default is 10.
	/// </value>
	[Range(1, int.MaxValue)]
	public int MaxObjectDepth { get; set; } = 10;

	/// <summary>
	/// Gets or sets the maximum allowed age for messages in days.
	/// </summary>
	/// <value>
	/// The maximum allowed age for messages in days. The default is 7.
	/// </value>
	[Range(1, int.MaxValue)]
	public int MaxMessageAgeDays { get; set; } = 7;

	/// <summary>
	/// Gets or sets a value indicating whether a correlation ID is required for all messages.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if a correlation ID is required for all messages; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
	/// </value>
	public bool RequireCorrelationId { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether control characters are blocked in string properties.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if control characters are blocked in string properties; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
	/// </value>
	public bool BlockControlCharacters { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether HTML content is blocked in string properties.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if HTML content is blocked in string properties; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
	/// </value>
	public bool BlockHtmlContent { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether SQL injection patterns are blocked.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if SQL injection patterns are blocked; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
	/// </value>
	public bool BlockSqlInjection { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether NoSQL injection patterns are blocked.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if NoSQL injection patterns are blocked; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
	/// </value>
	public bool BlockNoSqlInjection { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether command injection patterns are blocked.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if command injection patterns are blocked; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
	/// </value>
	public bool BlockCommandInjection { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether path traversal patterns are blocked.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if path traversal patterns are blocked; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
	/// </value>
	public bool BlockPathTraversal { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether LDAP injection patterns are blocked.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if LDAP injection patterns are blocked; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
	/// </value>
	public bool BlockLdapInjection { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether validation should fail when a custom validator throws an exception.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if validation should fail when a custom validator throws an exception; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
	/// </value>
	public bool FailOnValidatorException { get; set; } = true;
}
