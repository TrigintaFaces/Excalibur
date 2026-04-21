// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Security;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Recording capabilities for data access events, configuration changes, and security incidents
/// in Elasticsearch security auditing.
/// </summary>
/// <remarks>
/// <para>
/// This is a sub-interface of <see cref="IElasticsearchSecurityAuditor"/>.
/// For core auditing and authentication, see <see cref="IElasticsearchSecurityAuditorCore"/>.
/// For event notifications, see <see cref="IElasticsearchSecurityAuditorEvents"/>.
/// </para>
/// </remarks>
public interface IElasticsearchSecurityAuditorRecording
{
	/// <summary>
	/// Records a data access event for monitoring data usage patterns and detecting anomalies.
	/// </summary>
	/// <param name="dataAccessEvent"> The data access event details to record. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains true if the event was successfully recorded, false otherwise.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when audit recording fails due to security constraints. </exception>
	/// <exception cref="ArgumentNullException"> Thrown when the data access event is null. </exception>
	Task<bool> RecordDataAccessEventAsync(DataAccessEvent dataAccessEvent, CancellationToken cancellationToken);

	/// <summary>
	/// Records a security configuration change for compliance and change tracking purposes.
	/// </summary>
	/// <param name="configurationEvent"> The configuration change event details to record. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains true if the event was successfully recorded, false otherwise.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when audit recording fails due to security constraints. </exception>
	/// <exception cref="ArgumentNullException"> Thrown when the configuration event is null. </exception>
	Task<bool> RecordConfigurationChangeAsync(ConfigurationChangeEvent configurationEvent, CancellationToken cancellationToken);

	/// <summary>
	/// Records a security incident or violation for immediate attention and investigation.
	/// </summary>
	/// <param name="securityIncident"> The security incident details to record. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains true if the incident was successfully recorded, false otherwise.
	/// </returns>
	/// <exception cref="SecurityException"> Thrown when audit recording fails due to security constraints. </exception>
	/// <exception cref="ArgumentNullException"> Thrown when the security incident is null. </exception>
	Task<bool> RecordSecurityIncidentAsync(SecurityIncident securityIncident, CancellationToken cancellationToken);
}
