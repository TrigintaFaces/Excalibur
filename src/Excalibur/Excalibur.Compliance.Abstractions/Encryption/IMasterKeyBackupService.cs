// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Compliance;

/// <summary>
/// Provides master key backup and recovery operations for disaster recovery scenarios.
/// </summary>
/// <remarks>
/// <para>
/// This service handles the secure backup and restoration of master encryption keys,
/// which are the keys used to encrypt all other keys in the system (Key Encryption Keys - KEKs).
/// </para>
/// <para>
/// Features include:
/// </para>
/// <list type="bullet">
///   <item>HSM-backed export and import of master keys</item>
///   <item>Shamir's Secret Sharing for split-knowledge recovery (e.g., 3-of-5 custodians)</item>
///   <item>Full audit logging of all backup and recovery operations</item>
///   <item>Support for multiple backup formats (encrypted blob, HSM-wrapped, etc.)</item>
/// </list>
/// <para>
/// WARNING: Master key operations are highly sensitive. All operations MUST be audited
/// and require appropriate authorization (typically multi-party approval).
/// </para>
/// <para>
/// This is a composite of the focused <see cref="IMasterKeyBackupExporter"/> (export and recovery-split
/// generation) and <see cref="IMasterKeyRestoreService"/> (import, reconstruction, verification, and
/// status inspection) contracts. New code should depend on the narrowest interface that meets its needs.
/// </para>
/// </remarks>
public interface IMasterKeyBackupService : IMasterKeyBackupExporter, IMasterKeyRestoreService
{
}
