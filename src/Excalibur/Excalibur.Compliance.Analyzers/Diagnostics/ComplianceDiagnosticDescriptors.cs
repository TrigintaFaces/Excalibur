// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;

namespace Excalibur.Compliance.Analyzers;

/// <summary>
/// Central diagnostic descriptors for the field-encryption annotation analyzer.
/// </summary>
/// <remarks>
/// <para>
/// Diagnostic ID range: EXCMP001-EXCMP099.
/// </para>
/// <para>
/// Each rule reports an annotation the framework cannot act on. The runtime already refuses to encrypt a
/// model carrying one, but it can only say so when that model is first encrypted -- which for a compliance
/// feature is the expensive moment to learn it. These rules move the same judgement to the build, using the
/// same criteria in the same order, so a model that builds clean is one the framework can honour.
/// </para>
/// </remarks>
internal static class ComplianceDiagnosticDescriptors
{
	/// <summary>Category for field-encryption diagnostics.</summary>
	private const string EncryptionCategory = "Excalibur.Compliance.Encryption";

	/// <summary>EXCMP001: the annotated property's type cannot carry ciphertext.</summary>
	public static readonly DiagnosticDescriptor UnsupportedPropertyType = new(
		id: "EXCMP001",
		title: "An encryption annotation is on a property whose type cannot carry ciphertext",
		messageFormat: "{0} on '{1}' cannot be honoured: its type '{2}' is not encryptable. Supported types are string and byte[].",
		category: EncryptionCategory,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "Field encryption replaces the value with its ciphertext in place, so the property must be able to hold that ciphertext. The framework refuses to encrypt a model carrying this annotation.",
		helpLinkUri: "https://docs.excalibur-dispatch.dev/docs/diagnostics/EXCMP001");

	/// <summary>EXCMP002: the annotated property lacks a getter or a setter.</summary>
	public static readonly DiagnosticDescriptor MissingAccessor = new(
		id: "EXCMP002",
		title: "An encryption annotation is on a property without both a getter and a setter",
		messageFormat: "{0} on '{1}' cannot be honoured: a getter and a setter are both required to replace the value with its ciphertext",
		category: EncryptionCategory,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "The value is read, encrypted, and written back in place. An init accessor satisfies the setter requirement; a get-only or set-only property does not.",
		helpLinkUri: "https://docs.excalibur-dispatch.dev/docs/diagnostics/EXCMP002");

	/// <summary>EXCMP003: the annotated property is one the framework never inspects.</summary>
	public static readonly DiagnosticDescriptor NotInspected = new(
		id: "EXCMP003",
		title: "An encryption annotation is on a property the framework never inspects",
		messageFormat: "{0} on '{1}' has no effect: only public instance properties are encrypted, and this property is {2}",
		category: EncryptionCategory,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "The framework selects annotated public instance properties. An annotation on a static or non-public property is never seen, so the value is stored exactly as written and nothing at run time reports it.",
		helpLinkUri: "https://docs.excalibur-dispatch.dev/docs/diagnostics/EXCMP003");

	/// <summary>EXCMP004: the property is erasable personal data and also encrypted under a surviving purpose.</summary>
	public static readonly DiagnosticDescriptor ErasableAndSurvivingPurpose = new(
		id: "EXCMP004",
		title: "A property is both erasable personal data and encrypted under a surviving purpose",
		messageFormat: "'{0}' carries both [PersonalData] and [EncryptedField] with Purpose '{1}'. Remove the Purpose to keep the value erasable, or remove [PersonalData] if the value must outlive the subject.",
		category: EncryptionCategory,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description: "[PersonalData] binds the value to a per-subject key that is destroyed when the subject is erased; an explicit purpose binds it to a key that survives erasure. Honouring either silently would make the other's guarantee false, so the framework refuses both.",
		helpLinkUri: "https://docs.excalibur-dispatch.dev/docs/diagnostics/EXCMP004");
}
