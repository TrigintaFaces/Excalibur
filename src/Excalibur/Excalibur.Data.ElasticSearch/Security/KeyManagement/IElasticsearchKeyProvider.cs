// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines the contract for providers that store and retrieve Elasticsearch's own connection credentials --
/// OAuth tokens, service-account secrets, passwords, and API keys -- in a secure secret store.
/// </summary>
/// <remarks>
/// <para>
/// This interface composes the following focused sub-interfaces following the Interface Segregation Principle (ISP):
/// </para>
/// <list type="bullet">
///   <item><description><see cref="IElasticsearchKeyStorage"/> -- CRUD operations for secret storage and retrieval.</description></item>
///   <item><description><see cref="IElasticsearchKeyProviderEvents"/> -- events and secret listing for audit and monitoring.</description></item>
/// </list>
/// <para>
/// Consumers that need only a subset of functionality should depend on the specific sub-interface instead of this aggregate interface.
/// </para>
/// <para>
/// This is a connection-credential store, not encryption-key custody: it returns opaque secret strings, never key
/// material for cryptographic operations. Field-level encryption keys are resolved through
/// <see cref="Excalibur.Compliance.IKeyManagementProvider"/> and <see cref="Excalibur.Compliance.IEncryptionProviderRegistry"/>
/// instead, which structurally never expose key material to this package.
/// </para>
/// </remarks>
public interface IElasticsearchKeyProvider :
	IElasticsearchKeyStorage,
	IElasticsearchKeyProviderEvents
{
}
