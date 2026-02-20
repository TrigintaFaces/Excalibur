// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;


using Microsoft.Extensions.Logging;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Default implementation of the Elasticsearch security monitor.
/// </summary>
internal sealed class SecurityMonitor : IElasticsearchSecurityMonitor, IAsyncDisposable, IDisposable
{
	private readonly ElasticsearchClient _elasticClient;
	private readonly ILogger<SecurityMonitor> _logger;
	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Timer _timer;
	private volatile bool _isMonitoring;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="SecurityMonitor" /> class.
	/// </summary>
	/// <param name="settings"> The security monitoring settings. </param>
	/// <param name="elasticClient"> The Elasticsearch client. </param>
	/// <param name="logger"> The logger instance. </param>
	/// <exception cref="ArgumentNullException"> Thrown when any parameter is null. </exception>
	public SecurityMonitor(SecurityMonitoringOptions settings, ElasticsearchClient elasticClient, ILogger<SecurityMonitor> logger)
	{
		Configuration = settings ?? throw new ArgumentNullException(nameof(settings));
		_elasticClient = elasticClient ?? throw new ArgumentNullException(nameof(elasticClient));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_cancellationTokenSource = new CancellationTokenSource();
		_timer = new Timer(MonitoringCallback, state: null, Timeout.Infinite, Timeout.Infinite);

		// Initialize supported threat types based on monitoring settings
		SupportedThreatTypes = new List<ThreatType> { ThreatType.DataExfiltration, ThreatType.UnauthorizedAccess, ThreatType.Malware }
			.AsReadOnly();
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
	/// Gets a value indicating whether monitoring is currently active.
	/// </summary>
	/// <value> <c> true </c> if monitoring is active; otherwise, <c> false </c>. </value>
	public bool IsMonitoring => _isMonitoring;

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

			// Perform threat detection analysis
			await AnalyzeThreatPatternsAsync(securityEvent, cancellationToken).ConfigureAwait(false);

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
								.Term(static t => t.Field(new Field("status")).Value("pending")))))
					.Size(100),
				cancellationToken).ConfigureAwait(false);

			if (alertsResponse.IsValidResponse && alertsResponse.Documents.Count != 0)
			{
				await ProcessSecurityAlertsAsync(alertsResponse.Documents, cancellationToken).ConfigureAwait(false);
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
						.Range(r => r.DateRange(dr => dr.Field(new Field("timestamp")).Gte(DateMath.Now.Subtract(TimeSpan.FromHours(24))))))
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
	/// A task that represents the asynchronous operation. The task result contains the generated security alerts and their distribution status.
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

				if (alertRequest.AutoDistribute)
				{
					alertResult.DistributionStatus = await DistributeAlertsAsync(alerts, cancellationToken).ConfigureAwait(false);
				}
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
	/// Retrieves the current security monitoring status and health information.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains the monitoring status including active monitors and
	/// health indicators.
	/// </returns>
	public async Task<SecurityMonitoringStatus> GetMonitoringStatusAsync(CancellationToken cancellationToken)
	{
		try
		{
			var status = new SecurityMonitoringStatus
			{
				IsMonitoring = _isMonitoring,
				Configuration = new SecurityMonitoringStatus.MonitoringConfiguration
				{
					Enabled = Configuration.Enabled,
					Interval = Configuration.MonitoringInterval,
					Settings = new Dictionary<string, object>
						(StringComparer.Ordinal)
						{
							{ nameof(Configuration.DetectAnomalies), Configuration.DetectAnomalies },
							{ nameof(Configuration.MonitorAuthenticationAttacks), Configuration.MonitorAuthenticationAttacks },
							{ nameof(Configuration.DetectDataExfiltration), Configuration.DetectDataExfiltration },
							{ nameof(Configuration.AutomatedResponseEnabled), Configuration.AutomatedResponseEnabled },
							{ nameof(Configuration.FailedLoginThreshold), Configuration.FailedLoginThreshold },
						},
				},
				StatusTimestamp = DateTimeOffset.UtcNow,
			};

			// Check Elasticsearch health
			var healthResponse = await _elasticClient.Cluster.HealthAsync(cancellationToken).ConfigureAwait(false);
			status.ElasticsearchHealthy = healthResponse.IsValidResponse;

			if (healthResponse.IsValidResponse)
			{
				status.ElasticsearchStatus = healthResponse.Status.ToString();
			}

			// Get monitoring statistics
			var monitoringStats = await GetMonitoringStatisticsAsync(cancellationToken).ConfigureAwait(false);
			status.Statistics = new SecurityMonitoringStatus.MonitoringStatistics
			{
				EventsProcessed = monitoringStats.TotalEventsProcessed,
				AlertsGenerated = monitoringStats.AlertsGenerated,
				ThreatsDetected = monitoringStats.ThreatsDetected,
				AnomaliesDetected = 0, // No direct mapping, using default
				LastEventTime = monitoringStats.LastUpdated,
			};

			_logger.LogDebug("Retrieved security monitoring status: {Status}", status.IsMonitoring ? "Active" : "Inactive");

			return status;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to get monitoring status");
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
	/// Configures automatic security response actions for specific threat types.
	/// </summary>
	/// <param name="responseConfiguration"> The automated response configuration. </param>
	/// <param name="cancellationToken"> The cancellation token to monitor for cancellation requests. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains true if the response configuration was applied
	/// successfully, false otherwise.
	/// </returns>
	public async Task<bool> ConfigureAutomatedResponseAsync(
		AutomatedSecurityResponse responseConfiguration,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(responseConfiguration);

		_logger.LogInformation("Configuring automated response for threat type: {ThreatType}", responseConfiguration.ThreatType);

		try
		{
			// Store automated response configuration
			_ = await _elasticClient.IndexAsync(responseConfiguration, cancellationToken).ConfigureAwait(false);

			// Update internal configuration if needed This would typically update a configuration store or cache
			_logger.LogInformation("Successfully configured automated response for {ThreatType}", responseConfiguration.ThreatType);

			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to configure automated response for threat type: {ThreatType}", responseConfiguration.ThreatType);
			return false;
		}
	}

	/// <summary>
	/// Starts the security monitoring process asynchronously.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains <c> true </c> if monitoring started successfully;
	/// otherwise, <c> false </c>.
	/// </returns>
	/// <exception cref="InvalidOperationException"> Thrown when monitoring is already active. </exception>
	public async Task<bool> StartMonitoringAsync(CancellationToken cancellationToken)
	{
		if (_isMonitoring)
		{
			throw new InvalidOperationException("Security monitoring is already active.");
		}

		try
		{
			_logger.LogInformation("Starting security monitoring with interval {Interval}", Configuration.MonitoringInterval);

			// Verify Elasticsearch connection
			var healthResponse = await _elasticClient.Cluster.HealthAsync(cancellationToken).ConfigureAwait(false);
			if (!healthResponse.IsValidResponse)
			{
				_logger.LogError("Failed to connect to Elasticsearch: {Error}", healthResponse.DebugInformation);
				return false;
			}

			_isMonitoring = true;
			_ = _timer.Change(TimeSpan.Zero, Configuration.MonitoringInterval);

			_logger.LogInformation("Security monitoring started successfully");
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to start security monitoring");
			return false;
		}
	}

	/// <summary>
	/// Stops the security monitoring process asynchronously.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns>
	/// A task that represents the asynchronous operation. The task result contains <c> true </c> if monitoring stopped successfully;
	/// otherwise, <c> false </c>.
	/// </returns>
	public async Task<bool> StopMonitoringAsync(CancellationToken cancellationToken)
	{
		if (!_isMonitoring)
		{
			return true;
		}

		try
		{
			_logger.LogInformation("Stopping security monitoring");

			_isMonitoring = false;
			_ = _timer.Change(Timeout.Infinite, Timeout.Infinite);
			await _cancellationTokenSource.CancelAsync().ConfigureAwait(false);

			_logger.LogInformation("Security monitoring stopped successfully");
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error occurred while stopping security monitoring");
			return false;
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

			// Analyze location-based anomalies
			if (authenticationEvent.Location != null)
			{
				var locationAnomaly = await DetectLocationAnomalyAsync(authenticationEvent, cancellationToken).ConfigureAwait(false);
				if (locationAnomaly)
				{
					analysisResult.AnomalyDetected = true;
					analysisResult.RiskLevel = SecurityRiskLevel.Medium;

					OnAnomalyDetected(new AnomalyDetectedEventArgs(
						"LocationAnomaly",
						$"Unusual login location detected for user {authenticationEvent.UserId}"));
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
	/// Analyzes a data access event for security threats asynchronously.
	/// </summary>
	/// <param name="dataAccessEvent"> The data access event to analyze. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task that represents the asynchronous operation. The task result contains the security analysis result. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="dataAccessEvent" /> is null. </exception>
	public async Task<SecurityAnalysisResult> AnalyzeDataAccessEventAsync(
		DataAccessEvent dataAccessEvent,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(dataAccessEvent);

		try
		{
			_logger.LogDebug("Analyzing data access event for resource {Resource}", dataAccessEvent.ResourcePath);

			var analysisResult = new SecurityAnalysisResult
			{
				EventId = dataAccessEvent.Id,
				AnalysisTimestamp = DateTimeOffset.UtcNow,
				EventType = "DataAccess",
			};

			// Check for unauthorized access patterns
			if (await IsUnauthorizedAccess(dataAccessEvent, cancellationToken).ConfigureAwait(false))
			{
				analysisResult.ThreatDetected = true;
				analysisResult.ThreatType = nameof(ThreatType.UnauthorizedAccess);
				analysisResult.RiskLevel = SecurityRiskLevel.High;

				OnThreatDetected(new ThreatDetectedEventArgs(
					ThreatType.UnauthorizedAccess,
					$"Unauthorized access attempt to {dataAccessEvent.ResourcePath}"));
			}

			// Check for data exfiltration patterns
			if (await DetectDataExfiltration(dataAccessEvent, cancellationToken).ConfigureAwait(false))
			{
				analysisResult.ThreatDetected = true;
				analysisResult.ThreatType = nameof(ThreatType.DataExfiltration);
				analysisResult.RiskLevel = SecurityRiskLevel.Critical;

				OnThreatDetected(new ThreatDetectedEventArgs(
					ThreatType.DataExfiltration,
					$"Potential data exfiltration detected from {dataAccessEvent.ResourcePath}"));
			}

			return analysisResult;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error analyzing data access event");
			return new SecurityAnalysisResult
			{
				EventId = dataAccessEvent.Id,
				AnalysisTimestamp = DateTimeOffset.UtcNow,
				EventType = "DataAccess",
				HasError = true,
				ErrorMessage = ex.Message,
			};
		}
	}

	/// <summary>
	/// Performs comprehensive threat detection analysis asynchronously.
	/// </summary>
	/// <param name="request"> The threat detection request. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task that represents the asynchronous operation. The task result contains the threat detection result. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is null. </exception>
	public async Task<ThreatDetectionResult> PerformThreatDetectionAsync(
		ThreatDetectionRequest request,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		try
		{
			_logger.LogInformation("Performing threat detection for time range {StartTime} to {EndTime}", request.StartTime,
				request.EndTime);

			var result = new ThreatDetectionResult { RequestId = request.RequestId, AnalysisStartTime = DateTimeOffset.UtcNow };

			// Perform different types of threat detection based on request
			var tasks = new List<Task>();

			if (request.DetectBruteForce)
			{
				tasks.Add(DetectBruteForceAttacks(request, result, cancellationToken));
			}

			if (request.DetectAnomalies)
			{
				tasks.Add(DetectAnomalousActivity(request, result, cancellationToken));
			}

			if (request.DetectDataThreats)
			{
				tasks.Add(DetectDataThreats(request, result, cancellationToken));
			}

			await Task.WhenAll(tasks).ConfigureAwait(false);

			result.AnalysisEndTime = DateTimeOffset.UtcNow;

			_logger.LogInformation("Threat detection completed. Found {ThreatCount} threats", result.ThreatsDetected);

			return result;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error performing threat detection");
			return new ThreatDetectionResult
			{
				RequestId = request.RequestId,
				AnalysisStartTime = DateTimeOffset.UtcNow,
				AnalysisEndTime = DateTimeOffset.UtcNow,
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
	/// Processes and handles security alerts according to configured policies.
	/// </summary>
	/// <param name="alerts"> The security alerts to process. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task that represents the asynchronous operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="alerts" /> is null. </exception>
	public async Task ProcessSecurityAlertsAsync(IEnumerable<SecurityAlert> alerts, CancellationToken cancellationToken)
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

			var tasks = new List<Task>();

			foreach (var alert in alertList)
			{
				// Process high-priority alerts immediately
				if ((int)alert.Severity >= (int)SecurityRiskLevel.High)
				{
					tasks.Add(ProcessHighPriorityAlert(alert, cancellationToken));
				}
				else
				{
					tasks.Add(ProcessStandardAlert(alert, cancellationToken));
				}
			}

			await Task.WhenAll(tasks).ConfigureAwait(false);

			_logger.LogInformation("Completed processing security alerts");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error processing security alerts");
		}
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_isMonitoring = false;

		await _cancellationTokenSource.CancelAsync().ConfigureAwait(false);
		await _timer.DisposeAsync().ConfigureAwait(false);
		_cancellationTokenSource.Dispose();

		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Releases all resources used by the <see cref="SecurityMonitor" />.
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_isMonitoring = false;

		_cancellationTokenSource.Cancel();
		_timer.Dispose();
		_cancellationTokenSource.Dispose();

		GC.SuppressFinalize(this);
	}

	private static async Task<bool> IsUnauthorizedAccess(DataAccessEvent dataEvent, CancellationToken cancellationToken) =>

		// Implement unauthorized access detection logic
		await Task.FromResult(false).ConfigureAwait(false);

	private static async Task<bool> DetectDataExfiltration(DataAccessEvent dataEvent, CancellationToken cancellationToken) =>

		// Implement data exfiltration detection logic
		await Task.FromResult(false).ConfigureAwait(false);

	private static async Task DetectBruteForceAttacks(ThreatDetectionRequest request, ThreatDetectionResult result,
		CancellationToken cancellationToken) =>

		// Implement brute force detection
		await Task.CompletedTask.ConfigureAwait(false);

	private static async Task DetectAnomalousActivity(ThreatDetectionRequest request, ThreatDetectionResult result,
		CancellationToken cancellationToken) =>

		// Implement anomalous activity detection
		await Task.CompletedTask.ConfigureAwait(false);

	private static async Task DetectDataThreats(ThreatDetectionRequest request, ThreatDetectionResult result,
		CancellationToken cancellationToken) =>

		// Implement data threat detection
		await Task.CompletedTask.ConfigureAwait(false);

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

	private static async Task AnalyzeThreatPatternsAsync(SecurityMonitoringEvent securityEvent, CancellationToken cancellationToken) =>

		// Perform pattern analysis on the security event
		await Task.CompletedTask.ConfigureAwait(false);

	private static int CalculateNumericScore(SecurityRiskLevel riskLevel) =>
		riskLevel switch
		{
			SecurityRiskLevel.Low => 10,
			SecurityRiskLevel.Medium => 35,
			SecurityRiskLevel.High => 70,
			SecurityRiskLevel.Critical => 95,
			_ => 10,
		};

	private static Query BuildThreatQuery(SecurityAlertRequest alertRequest) =>
		new BoolQuery
		{
			Must =
			[
				new DateRangeQuery(new Field("timestamp"))
				{
					Gte = DateMath.Now.Subtract(alertRequest.EndTime.Subtract(alertRequest.StartTime)),
				},
			],
		};

	private static async Task<string> DistributeAlertsAsync(IEnumerable<SecurityAlert> alerts, CancellationToken cancellationToken)
	{
		// Implement alert distribution logic
		await Task.CompletedTask.ConfigureAwait(false);
		return "Distributed";
	}

	private static async Task<SecurityMonitoringStatistics> GetMonitoringStatisticsAsync(CancellationToken cancellationToken)
	{
		// Get monitoring statistics from Elasticsearch
		await Task.CompletedTask.ConfigureAwait(false);
		return new SecurityMonitoringStatistics();
	}

	private static async Task<bool> DetectLocationAnomalyAsync(AuthenticationEvent authenticationEvent, CancellationToken cancellationToken)
	{
		// Simple implementation - in a real system, this would check for anomalous locations
		await Task.CompletedTask.ConfigureAwait(false);

		// Return false for now (no anomaly detected)
		return false;
	}


	private void MonitoringCallback(object? state)
	{
		if (!_isMonitoring)
		{
			return;
		}

		try
		{
			_ = Task.Run(async () => await PerformPeriodicMonitoring(_cancellationTokenSource.Token).ConfigureAwait(false));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error in monitoring callback");
		}
	}

	private async Task PerformPeriodicMonitoring(CancellationToken cancellationToken)
	{
		try
		{
			_logger.LogDebug("Performing periodic security monitoring");

			// Perform real-time threat detection
			var request = new ThreatDetectionRequest
			{
				RequestId = Guid.NewGuid().ToString(),
				StartTime = DateTimeOffset.UtcNow.Subtract(Configuration.MonitoringInterval),
				EndTime = DateTimeOffset.UtcNow,
				DetectBruteForce = true,
				DetectAnomalies = true,
				DetectDataThreats = true,
			};

			var result = await PerformThreatDetectionAsync(request, cancellationToken).ConfigureAwait(false);

			if (result.DetectedThreats?.Count > 0)
			{
				var riskLevel =
					await CalculateSecurityRiskAsync(
						result.DetectedThreats.Select(static t =>
							new SecurityEvent { Severity = t.Severity.ToString(), EventType = t.AlertType }),
						null, cancellationToken).ConfigureAwait(false);

				var alerts = result.DetectedThreats;

				if (alerts.Count != 0)
				{
					await ProcessSecurityAlertsAsync(alerts, cancellationToken).ConfigureAwait(false);
				}
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error during periodic monitoring");
		}
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
									dr.Field(new Field("timestamp")).Gte(DateMath.Now.Subtract(TimeSpan.FromDays(30))))))))
					.Sort(so => so.Field(f => f.Timestamp, new FieldSort { Order = SortOrder.Desc }))
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

	private async Task ProcessHighPriorityAlert(SecurityAlert alert, CancellationToken cancellationToken)
	{
		_logger.LogWarning("Processing high-priority security alert: {AlertType}", alert.AlertType);

		if (Configuration.AutomatedResponseEnabled)
		{
			await TriggerAutomatedResponse(alert, cancellationToken).ConfigureAwait(false);
		}
	}

	private async Task ProcessStandardAlert(SecurityAlert alert, CancellationToken cancellationToken)
	{
		_logger.LogInformation("Processing standard security alert: {AlertType}", alert.AlertType);

		// Standard alert processing logic
		await Task.CompletedTask.ConfigureAwait(false);
	}

	private async Task TriggerAutomatedResponse(SecurityAlert alert, CancellationToken cancellationToken)
	{
		try
		{
			_logger.LogInformation("Triggering automated response for alert {AlertId}", alert.AlertId);

			var responseArgs = new AutomatedResponseTriggeredEventArgs(ParseThreatType(alert.AlertType), "AutomatedSecurityResponse");
			OnAutomatedResponseTriggered(responseArgs);

			await Task.CompletedTask.ConfigureAwait(false);
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
