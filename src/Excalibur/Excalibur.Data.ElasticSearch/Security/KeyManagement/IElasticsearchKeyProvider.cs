// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Defines the contract for secure key and secret management providers that integrate with external key management systems for
/// Elasticsearch security operations.
/// </summary>
/// <remarks>
/// <para>
/// This interface composes the following focused sub-interfaces following the Interface Segregation Principle (ISP):
/// </para>
/// <list type="bullet">
///   <item><description><see cref="IElasticsearchKeyStorage"/> -- CRUD operations for secret storage and retrieval.</description></item>
///   <item><description><see cref="IElasticsearchKeyManagement"/> -- key generation, rotation, and provider capabilities.</description></item>
///   <item><description><see cref="IElasticsearchKeyProviderEvents"/> -- events and secret listing for audit and monitoring.</description></item>
/// </list>
/// <para>
/// Consumers that need only a subset of functionality should depend on the specific sub-interface instead of this aggregate interface.
/// </para>
/// </remarks>
public interface IElasticsearchKeyProvider :
	IElasticsearchKeyStorage,
	IElasticsearchKeyManagement,
	IElasticsearchKeyProviderEvents
{
}
