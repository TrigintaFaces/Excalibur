// Copyright (c) 2025 The Excalibur Project Authors
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)
//
// You may not use this file except in compliance with the License terms above. You may obtain copies of the licenses in
// the project root or online.
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on
// an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

namespace Excalibur.DataAccess.ElasticSearch;

/// <summary>
///   Represents the configuration settings required to connect to an Elasticsearch cluster.
/// </summary>
public class ElasticsearchConfigurationSettings
{
	/// <summary>
	///   Gets or sets the URL of the Elasticsearch cluster.
	/// </summary>
	/// <value>
	///   A <see cref="string" /> representing the base URL of the Elasticsearch cluster. This property is optional when
	///   <see cref="Urls" /> is provided.
	/// </value>
	public string? Url { get; init; }

	/// <summary>
	///   Gets or sets the URLs of the Elasticsearch cluster nodes.
	/// </summary>
	/// <value>
	///   An array of <see cref="string" /> representing the base URLs of the Elasticsearch cluster nodes. If set, these
	///   values override <see cref="Url" />.
	/// </value>
	public string[]? Urls { get; init; }

	/// <summary>
	///   Gets or sets the certificate fingerprint for SSL/TLS verification.
	/// </summary>
	/// <value>
	///   A <see cref="string" /> representing the certificate fingerprint for secure connections, or <c> null </c> if
	///   not required.
	/// </value>
	public string? CertificateFingerprint { get; init; }

	/// <summary>
	///   Gets or sets the username for basic authentication.
	/// </summary>
	/// <value>
	///   A <see cref="string" /> representing the username, or <c> null </c> if basic authentication is not used.
	/// </value>
	public string? Username { get; init; }

	/// <summary>
	///   Gets or sets the password for basic authentication.
	/// </summary>
	/// <value>
	///   A <see cref="string" /> representing the password, or <c> null </c> if basic authentication is not used.
	/// </value>
	public string? Password { get; init; }

	/// <summary>
	///   Gets or sets the API key for Elasticsearch authentication.
	/// </summary>
	/// <value>
	///   A <see cref="string" /> representing the API key, or <c> null </c> if API key authentication is not used.
	/// </value>
	public string? ApiKey { get; init; }
}
