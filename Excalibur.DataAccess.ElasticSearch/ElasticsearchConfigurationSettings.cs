namespace Excalibur.DataAccess.ElasticSearch;

/// <summary>
///     Represents the configuration settings required to connect to an Elasticsearch cluster.
/// </summary>
public class ElasticsearchConfigurationSettings
{
	/// <summary>
	///     Gets or sets the URL of the Elasticsearch cluster.
	/// </summary>
	/// <value> A <see cref="string" /> representing the base URL of the Elasticsearch cluster. This property is required. </value>
	public string Url { get; init; } = null!;

	/// <summary>
	///     Gets or sets the certificate fingerprint for SSL/TLS verification.
	/// </summary>
	/// <value> A <see cref="string" /> representing the certificate fingerprint for secure connections, or <c> null </c> if not required. </value>
	public string? CertificateFingerprint { get; init; }

	/// <summary>
	///     Gets or sets the username for basic authentication.
	/// </summary>
	/// <value> A <see cref="string" /> representing the username, or <c> null </c> if basic authentication is not used. </value>
	public string? Username { get; init; }

	/// <summary>
	///     Gets or sets the password for basic authentication.
	/// </summary>
	/// <value> A <see cref="string" /> representing the password, or <c> null </c> if basic authentication is not used. </value>
	public string? Password { get; init; }

	/// <summary>
	///     Gets or sets the API key for Elasticsearch authentication.
	/// </summary>
	/// <value> A <see cref="string" /> representing the API key, or <c> null </c> if API key authentication is not used. </value>
	public string? ApiKey { get; init; }
}
