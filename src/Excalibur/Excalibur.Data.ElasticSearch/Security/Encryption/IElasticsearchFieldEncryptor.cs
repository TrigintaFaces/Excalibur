// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines the contract for field-level encryption of sensitive data in Elasticsearch documents with support for multiple encryption
/// algorithms and key management integration.
/// </summary>
/// <remarks>
/// <para>
/// This interface composes the following focused sub-interfaces following the Interface Segregation Principle (ISP):
/// </para>
/// <list type="bullet">
///   <item><description><see cref="IElasticsearchFieldEncryption"/> -- core encrypt/decrypt operations and supported algorithms.</description></item>
///   <item><description><see cref="IElasticsearchFieldEncryptionPolicy"/> -- policy evaluation for field encryption decisions and classification.</description></item>
///   <item><description><see cref="IElasticsearchFieldEncryptionMaintenance"/> -- integrity validation and key rotation operations.</description></item>
///   <item><description><see cref="IElasticsearchFieldEncryptorEvents"/> -- events raised during encryption operations.</description></item>
/// </list>
/// <para>
/// Consumers that need only a subset of functionality should depend on the specific sub-interface instead of this aggregate interface.
/// </para>
/// </remarks>
public interface IElasticsearchFieldEncryptor :
	IElasticsearchFieldEncryption,
	IElasticsearchFieldEncryptionPolicy,
	IElasticsearchFieldEncryptionMaintenance,
	IElasticsearchFieldEncryptorEvents
{
}
