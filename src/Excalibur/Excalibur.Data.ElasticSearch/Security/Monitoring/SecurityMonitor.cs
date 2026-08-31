// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Default implementation of the Elasticsearch security monitor.
/// </summary>
internal sealed class SecurityMonitor : IElasticsearchSecurityMonitor
{
	private readonly ElasticsearchClient _elasticClient;
	private readonly ILogger<SecurityMonitor> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="SecurityMonitor" /> class.
	/// </summary>
	/// <param name="options"> The security monitoring options. </param>
	/// <param name="elasticClient"> The Elasticsearch client. </param>
	/// <param name="logger"> The logger instance. </param>
	/// <exception cref="ArgumentNullException"> Thrown when any parameter is null. </exception>
	public SecurityMonitor(
		IOptions<SecurityMonitoringOptions> options,
		ElasticsearchClient elasticClient,
		ILogger<SecurityMonitor> logger)
	{
		ArgumentNullException.ThrowIfNull(options);

		Configuration = options.Value;
		_elasticClient = elasticClient ?? throw new ArgumentNullException(nameof(elasticClient));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		// Only what this monitor can actually raise. The failed-login threshold is the one detection it
		// performs; anything else reaching a stored alert came from a caller, not from detection here.
		SupportedThreatTypes = new List<ThreatType> { ThreatType.UnauthorizedAccess }.AsReadOnly();
	}

	/// <summary>
	/// Occurs when a threat is detected.
	/// </summary>
	public event EventHandler<ThreatDetectedEventArgs>? ThreatDetected;

	/// <summary>
	/// Occurs when an anomaly is detected.
	/// </summary>
	public event EventHandler<AnomalyDetectedEventArgs>? AnomalyDetected;

	/// <summary>
	/// Occurs when a security alert is generated.
	/// </summary>
	public event EventHandler<SecurityAlertGeneratedEventArgs>? SecurityAlertGenerated;

	/// <summary>
	/// Occurs when an automated response is triggered.
	/// </summary>
	public event EventHandler<AutomatedResponseTriggeredEventArgs>? AutomatedResponseTriggered;

	/// <summary>
	/// Gets the configuration settings for security monitoring.
	/// </summary>
	/// <value> The current security monitoring configuration. </value>
	public SecurityMonitoringOptions Configuration { get; }

	/// <summary>
	/// Gets the collection of threat types that this monitor can detect.
	/// </summary>
	/// <value> A read-only collection of supported threat types. </value>
	public IReadOnlyCollection<ThreatType> SupportedThreatTypes { get; }

	/// <summary>
	/// Gets a value indicating whether automated response is enabled.
	/// </summary>
	/// <value> <c> true </c> if automated response is enabled; otherwise, <c> false </c>. </value>
	public bool AutomatedResponseEnabled => Configuration.AutomatedResponseEnabled;

	/// <summary>
	/// Monitors a security event for compliance and threat detection.
	/// </summary>
	/// <param name="securityEvent"> The security event to monitor. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns> A task representing the asynchronous monitoring operation. </returns>
	public async Task MonitorSecurityEventAsync(SecurityMonitoringEvent securityEvent, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(securityEvent);

		_logger.LogDebug("Monitoring security event: {EventType}", securityEvent.EventType);

		try
		{
			// Index the security event for monitoring
			_ = await _elasticClient.IndexAsync(securityEvent, cancellationToken).ConfigureAwait(false);

			_logger.LogInformation("Successfully monitored security event {EventId}", securityEvent.EventType);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to monitor security event {EventId}", securityEvent.EventType);
			throw;
		}
	}

	/// <summary>
	/// Processes pending security alerts and triggers appropriate automated responses.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task that represents the asynchronous operation. The task result contains the number of processed alerts. </returns>
	public async Task<int> ProcessSecurityAlertsAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Starting security alert processing");
		var alertsProcessed = 0;

		try
		{
			// Query pending alerts from Elasticsearch
			var alertsResponse = await _elasticClient.SearchAsync<SecurityAlert>(
				static s => s
					.Query(static q => q
						.Bool(static b => b
							.Must(static m => m
								.Term(static t => t.Field("status").Value("pending")))))
					.Size(100),
				cancellationToken).ConfigureAwait(false);

			if (alertsResponse.IsValidResponse && alertsResponse.Documents.Count != 0)
			{
				ProcessSecurityAlerts(alertsResponse.Documents);
				alertsProcessed = alertsResponse.Documents.Count;
			}

			_logger.LogInformation("Processed {AlertsProcessed} security alerts", alertsProcessed);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to process security alerts");
			throw;
		}

		return alertsProcessed;
	}

	/// <summary>
	/// Calculates the current security risk score based on recent events and system state.
	/// </summary>
	/// <param name="riskCalculationRequest"> The risk calculation request with parameters. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the calculated risk score and contributing factors.
	/// </returns>
	public async Task<SecurityRiskScore> CalculateSecurityRiskAsync(
		RiskCalculationRequest riskCalculationRequest,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(riskCalculationRequest);

		_logger.LogDebug("Calculating security risk for time window: {TimeWindow}", riskCalculationRequest.TimeWindow);

		try
		{
			SecurityRiskScore riskScore;

			// Query recent security events
			var eventsResponse = await _elasticClient.SearchAsync<SecurityEvent>(
				static s => s
					.Query(static q => q
						.Range(r => r.DateRange(dr => dr.Field("timestamp").Gte(DateMath.Now.Subtract(TimeSpan.FromHours(24))))))
					.Size(1000),
				cancellationToken).ConfigureAwait(false);

			if (eventsResponse.IsValidResponse && eventsResponse.Documents.Count != 0)
			{
				var riskLevel = await CalculateSecurityRiskAsync(eventsResponse.Documents, null, cancellationToken)
					.ConfigureAwait(false);
				riskScore = new SecurityRiskScore(riskLevel, CalculateNumericScore(riskLevel));
			}
			else
			{
				riskScore = new SecurityRiskScore(SecurityRiskLevel.Low, 10);
			}

			_logger.LogInformation("Calculated security risk: {RiskLevel} (Score: {Score})", riskScore.Level, riskScore.Score);

			return riskScore;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to calculate security risk");
			throw;
		}
	}

	/// <summary>
	/// Generates security alerts based on detected threats and anomalies.
	/// </summary>
	/// <param name="alertRequest"> The security alert generation request with criteria. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the generated security alerts. Alerts are generated and,
	/// when enabled, stored in Elasticsearch and raised on <c>SecurityAlertGenerated</c>; this package does not deliver them to a
	/// notification channel.
	/// </returns>
	public async Task<SecurityAlertResult> GenerateSecurityAlertsAsync(
		SecurityAlertRequest alertRequest,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(alertRequest);

		_logger.LogDebug("Generating security alerts for criteria: {Criteria}", alertRequest.Criteria);

		try
		{
			var alertResult = new SecurityAlertResult { RequestId = alertRequest.RequestId, GenerationTimestamp = DateTimeOffset.UtcNow };

			// Query threats based on alert criteria
			var threatsResponse = await _elasticClient.SearchAsync<DetectedThreat>(
				s => s
					.Query(q => BuildThreatQuery(alertRequest))
					.Size(100),
				cancellationToken).ConfigureAwait(false);

			if (threatsResponse.IsValidResponse && threatsResponse.Documents.Count != 0)
			{
				var alerts = await GenerateSecurityAlertsAsync(threatsResponse.Documents, alertRequest.MinimumSeverity, cancellationToken)
					.ConfigureAwait(false);
				alertResult.GeneratedAlerts = [.. alerts];
				alertResult.AlertCount = alertResult.GeneratedAlerts.Count;
			}

			_logger.LogInformation("Generated {AlertCount} security alerts", alertResult.AlertCount);

			return alertResult;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to generate security alerts");
			throw;
		}
	}

	/// <summary>
	/// Updates threat intelligence data from external sources for enhanced detection capabilities.
	/// </summary>
	/// <param name="updateRequest"> The threat intelligence update request with source parameters. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the update result including the number of indicators
	/// updated and any errors encountered.
	/// </returns>
	public async Task<ThreatIntelligenceUpdateResult> UpdateThreatIntelligenceAsync(
		ThreatIntelligenceUpdateRequest updateRequest,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(updateRequest);

		_logger.LogInformation("Updating threat intelligence from source: {Source}", updateRequest.SourceName);

		try
		{
			var updateResult = new ThreatIntelligenceUpdateResult
			{
				RequestId = updateRequest.RequestId,
				UpdateStartTime = DateTimeOffset.UtcNow,
				SourceName = updateRequest.SourceName,
			};

			// Process threat intelligence indicators
			if (updateRequest.ThreatIndicators?.Count > 0)
			{
				var bulkResponse = await _elasticClient.BulkAsync(
					b =>
						b.IndexMany(updateRequest.ThreatIndicators),
					cancellationToken).ConfigureAwait(false);

				if (bulkResponse.IsValidResponse)
				{
					updateResult.IndicatorsUpdated = updateRequest.ThreatIndicators.Count;
					updateResult.Success = true;
				}
				else
				{
					updateResult.Errors.Add($"Failed to update indicators: {bulkResponse.DebugInformation}");
				}
			}

			updateResult.UpdateEndTime = DateTimeOffset.UtcNow;
			updateResult.Duration = updateResult.UpdateEndTime - updateResult.UpdateStartTime;

			_logger.LogInformation("Threat intelligence update completed. Updated {Count} indicators", updateResult.IndicatorsUpdated);

			return updateResult;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to update threat intelligence");
			throw;
		}
	}

	/// <summary>
	/// Analyzes an authentication event for security threats asynchronously.
	/// </summary>
	/// <param name="authenticationEvent"> The authentication event to analyze. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task that represents the asynchronous operation. The task result contains the security analysis result. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="authenticationEvent" /> is null. </exception>
	public async Task<SecurityAnalysisResult> AnalyzeAuthenticationEventAsync(
		AuthenticationEvent authenticationEvent,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(authenticationEvent);

		try
		{
			_logger.LogDebug("Analyzing authentication event for user {UserId}", authenticationEvent.UserId);

			var analysisResult = new SecurityAnalysisResult
			{
				EventId = authenticationEvent.Id,
				AnalysisTimestamp = DateTimeOffset.UtcNow,
				EventType = "Authentication",
			};

			// Analyze failed login attempts
			if (!authenticationEvent.Success)
			{
				var failedAttempts = await GetFailedLoginAttempts(authenticationEvent.UserId, cancellationToken).ConfigureAwait(false);
				if (failedAttempts >= Configuration.FailedLoginThreshold)
				{
					analysisResult.ThreatDetected = true;
					analysisResult.ThreatType = nameof(ThreatType.UnauthorizedAccess);
					analysisResult.RiskLevel = SecurityRiskLevel.High;

					OnThreatDetected(new ThreatDetectedEventArgs(
						ThreatType.UnauthorizedAccess,
						$"Multiple failed login attempts detected for user {authenticationEvent.UserId}"));
				}
			}

			return analysisResult;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error analyzing authentication event");
			return new SecurityAnalysisResult
			{
				EventId = authenticationEvent.Id,
				AnalysisTimestamp = DateTimeOffset.UtcNow,
				EventType = "Authentication",
				HasError = true,
				ErrorMessage = ex.Message,
			};
		}
	}

	/// <summary>
	/// Calculates the security risk level based on provided events and context.
	/// </summary>
	/// <param name="events"> The security events to analyze. </param>
	/// <param name="context"> The analysis context. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task that represents the asynchronous operation. The task result contains the calculated security risk level. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="events" /> is null. </exception>
	public Task<SecurityRiskLevel> CalculateSecurityRiskAsync(
		IEnumerable<SecurityEvent> events,
		SecurityAnalysisContext? context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(events);

		try
		{
			var eventList = events.ToList();
			if (eventList.Count == 0)
			{
				return Task.FromResult(SecurityRiskLevel.Low);
			}

			_logger.LogDebug("Calculating security risk for {EventCount} events", eventList.Count);

			var riskScore = 0;
			var criticalEvents = 0;
			var highRiskEvents = 0;

			foreach (var securityEvent in eventList)
			{
				switch (securityEvent.Severity?.ToUpper(System.Globalization.CultureInfo.CurrentCulture))
				{
					case "CRITICAL":
						riskScore += 10;
						criticalEvents++;
						break;

					case "HIGH":
						riskScore += 5;
						highRiskEvents++;
						break;

					case "MEDIUM":
						riskScore += 2;
						break;

					case "LOW":
						riskScore++;
						break;
					default:
						break;
				}
			}

			// Apply context-based adjustments
			if (context != null)
			{
				if (context.IsHighValueTarget)
				{
					riskScore = (int)(riskScore * 1.5);
				}

				if (context.HasRecentIncidents)
				{
					riskScore = (int)(riskScore * 1.2);
				}
			}

			// Determine final risk level
			SecurityRiskLevel finalRiskLevel;
			if (criticalEvents > 0 || riskScore >= 50)
			{
				finalRiskLevel = SecurityRiskLevel.Critical;
			}
			else if (highRiskEvents > 3 || riskScore >= 25)
			{
				finalRiskLevel = SecurityRiskLevel.High;
			}
			else if (riskScore >= 10)
			{
				finalRiskLevel = SecurityRiskLevel.Medium;
			}
			else
			{
				finalRiskLevel = SecurityRiskLevel.Low;
			}

			_logger.LogInformation("Calculated security risk level: {RiskLevel} (Score: {RiskScore})", finalRiskLevel, riskScore);

			return Task.FromResult(finalRiskLevel);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error calculating security risk");
			return Task.FromResult(SecurityRiskLevel.High); // Default to high risk on error for safety
		}
	}

	/// <summary>
	/// Generates security alerts based on detected threats and risk levels.
	/// </summary>
	/// <param name="threats"> The detected threats. </param>
	/// <param name="riskLevel"> The overall risk level. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task that represents the asynchronous operation. The task result contains the generated security alerts. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="threats" /> is null. </exception>
	public async Task<IEnumerable<SecurityAlert>> GenerateSecurityAlertsAsync(
		IEnumerable<DetectedThreat> threats,
		SecurityRiskLevel riskLevel, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(threats);

		try
		{
			var threatList = threats.ToList();
			var alerts = new List<SecurityAlert>();

			_logger.LogInformation("Generating security alerts for {ThreatCount} threats with risk level {RiskLevel}", threatList.Count,
				riskLevel);

			foreach (var threat in threatList)
			{
				var alert = new SecurityAlert
				{
					AlertId = Guid.NewGuid(),
					AlertType = threat.ThreatType,
					Severity = (SecurityEventSeverity)DetermineAlertSeverity(threat, riskLevel),
					Description = GenerateAlertDescription(threat),
					Timestamp = DateTimeOffset.UtcNow,
				};

				alerts.Add(alert);

				// Raise event for each alert generated
				OnSecurityAlertGenerated(new SecurityAlertGeneratedEventArgs(alert));
			}

			// Store alerts in Elasticsearch if configured
			if (Configuration.StoreAlertsInElasticsearch && alerts.Count > 0)
			{
				await StoreAlertsAsync(alerts, cancellationToken).ConfigureAwait(false);
			}

			_logger.LogInformation("Generated {AlertCount} security alerts", alerts.Count);

			return alerts;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error generating security alerts");
			return [];
		}
	}

	/// <summary>
	/// Processes and handles security alerts according to configured policies. Processing is in-process
	/// -- it logs, and raises the automated-response event for high-priority alerts -- so there is nothing
	/// to await here.
	/// </summary>
	/// <param name="alerts"> The security alerts to process. </param>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="alerts" /> is null. </exception>
	private void ProcessSecurityAlerts(IEnumerable<SecurityAlert> alerts)
	{
		ArgumentNullException.ThrowIfNull(alerts);

		try
		{
			var alertList = alerts.ToList();
			if (alertList.Count == 0)
			{
				return;
			}

			_logger.LogInformation("Processing {AlertCount} security alerts", alertList.Count);

			foreach (var alert in alertList)
			{
				// Process high-priority alerts immediately
				if ((int)alert.Severity >= (int)SecurityRiskLevel.High)
				{
					ProcessHighPriorityAlert(alert);
				}
				else
				{
					ProcessStandardAlert(alert);
				}
			}

			_logger.LogInformation("Completed processing security alerts");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error processing security alerts");
		}
	}

	private static SecurityRiskLevel ParseSeverityLevel(string severity) =>
		severity?.ToUpper(System.Globalization.CultureInfo.CurrentCulture) switch
		{
			"CRITICAL" => SecurityRiskLevel.Critical,
			"HIGH" => SecurityRiskLevel.High,
			"MEDIUM" => SecurityRiskLevel.Medium,
			"LOW" => SecurityRiskLevel.Low,
			_ => SecurityRiskLevel.Low,
		};

	private static ThreatType ParseThreatType(string threatType) =>
		threatType?.ToUpper(System.Globalization.CultureInfo.CurrentCulture) switch
		{
			"MALWARE" => ThreatType.Malware,
			"DATAEXFILTRATION" => ThreatType.DataExfiltration,
			"UNAUTHORIZEDACCESS" => ThreatType.UnauthorizedAccess,
			"DENIALOFSERVICE" => ThreatType.DenialOfService,
			"PRIVILEGEESCALATION" => ThreatType.PrivilegeEscalation,
			_ => ThreatType.Other,
		};

	private static string GenerateAlertDescription(DetectedThreat threat) =>
		$"Security threat detected: {threat.ThreatType} - {threat.Description}";

	private static int CalculateNumericScore(SecurityRiskLevel riskLevel) =>
		riskLevel switch
		{
			SecurityRiskLevel.Low => 10,
			SecurityRiskLevel.Medium => 35,
			SecurityRiskLevel.High => 70,
			SecurityRiskLevel.Critical => 95,
			_ => 10,
		};

	private static BoolQuery BuildThreatQuery(SecurityAlertRequest alertRequest)
	{
		var must = new List<Query>
		{
			new DateRangeQuery("timestamp")
			{
				Gte = DateMath.Now.Subtract(alertRequest.EndTime.Subtract(alertRequest.StartTime)),
			},
		};

		// The caller's criteria narrow the search. Omitting them returned every threat in the window
		// regardless of what the caller asked for, so a request scoped to one event type or one target
		// system silently received the whole estate's threats.
		if (alertRequest.Criteria.EventTypes.Count > 0)
		{
			must.Add(new TermsQuery
			{
				Field = "threatType",
				Terms = new TermsQueryField(
					[.. alertRequest.Criteria.EventTypes.Select(static type => (FieldValue)type)]),
			});
		}

		if (alertRequest.Criteria.TargetSystems.Count > 0)
		{
			must.Add(new TermsQuery
			{
				Field = "source",
				Terms = new TermsQueryField(
					[.. alertRequest.Criteria.TargetSystems.Select(static system => (FieldValue)system)]),
			});
		}

		return new BoolQuery { Must = must };
	}

	private async Task<int> GetFailedLoginAttempts(string userId, CancellationToken cancellationToken)
	{
		try
		{
			var searchResponse = await _elasticClient.SearchAsync<AuthenticationEvent>(
				s => s
					.Query(q => q
						.Bool(b => b
							.Must(
								m => m
									.Term(t => t.Field(f => f.UserId).Value(userId)),
								m => m.Term(t => t.Field(f => f.Success).Value(value: false)),
								m => m.Range(r => r.DateRange(dr =>
									dr.Field("timestamp").Gte(DateMath.Now.Subtract(TimeSpan.FromDays(30))))))))
					.Sort(so => so.Field(f => f.Field("timestamp").Order(SortOrder.Desc)))
					.Size(10000),
				cancellationToken).ConfigureAwait(false);

			return searchResponse.IsValidResponse ? searchResponse.Documents.Count : 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving failed login attempts for user {UserId}", userId);
			return 0;
		}
	}

	private SecurityRiskLevel DetermineAlertSeverity(DetectedThreat threat, SecurityRiskLevel contextRiskLevel)
	{
		// Convert string severity to SecurityRiskLevel for comparison
		var threatSeverity = ParseSeverityLevel(threat.Severity);

		// Logic to determine alert severity based on threat and context
		return threatSeverity >= SecurityRiskLevel.High || contextRiskLevel >= SecurityRiskLevel.High
			? SecurityRiskLevel.High
			: SecurityRiskLevel.Medium;
	}

	private async Task StoreAlertsAsync(IEnumerable<SecurityAlert> alerts, CancellationToken cancellationToken)
	{
		try
		{
			var bulkResponse = await _elasticClient.BulkAsync(
				b =>
					b.IndexMany(alerts),
				cancellationToken).ConfigureAwait(false);

			if (!bulkResponse.IsValidResponse)
			{
				_logger.LogWarning("Failed to store alerts in Elasticsearch: {Error}", bulkResponse.DebugInformation);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error storing alerts in Elasticsearch");
		}
	}

	private void ProcessHighPriorityAlert(SecurityAlert alert)
	{
		_logger.LogWarning("Processing high-priority security alert: {AlertType}", alert.AlertType);

		if (Configuration.AutomatedResponseEnabled)
		{
			TriggerAutomatedResponse(alert);
		}
	}

	private void ProcessStandardAlert(SecurityAlert alert) =>
		_logger.LogInformation("Processing standard security alert: {AlertType}", alert.AlertType);

	private void TriggerAutomatedResponse(SecurityAlert alert)
	{
		try
		{
			_logger.LogInformation("Triggering automated response for alert {AlertId}", alert.AlertId);

			OnAutomatedResponseTriggered(new AutomatedResponseTriggeredEventArgs(ParseThreatType(alert.AlertType)));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error triggering automated response for alert {AlertId}", alert.AlertId);
		}
	}

	private void OnThreatDetected(ThreatDetectedEventArgs e) => ThreatDetected?.Invoke(this, e);

	private void OnAnomalyDetected(AnomalyDetectedEventArgs e) => AnomalyDetected?.Invoke(this, e);

	private void OnSecurityAlertGenerated(SecurityAlertGeneratedEventArgs e) => SecurityAlertGenerated?.Invoke(this, e);

	private void OnAutomatedResponseTriggered(AutomatedResponseTriggeredEventArgs e) => AutomatedResponseTriggered?.Invoke(this, e);
}
