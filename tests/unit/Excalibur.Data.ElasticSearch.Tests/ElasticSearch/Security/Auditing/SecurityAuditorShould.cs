// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.ElasticSearch.Security.Auditing;

/// <summary>
/// Unit tests for the <see cref="SecurityAuditor"/> class.
/// </summary>
/// <remarks>
/// Sprint 512 (S512.2): Elasticsearch compliance unit tests.
/// Tests focus on constructor validation for GDPR/HIPAA/PCI-DSS compliance-critical components.
/// The SecurityAuditor is the main public class orchestrating all compliance reporting.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
[Trait("Feature", "Security")]
public sealed class SecurityAuditorShould : IDisposable
{
	private readonly ElasticsearchClient _fakeClient;
	private readonly IOptions<AuditOptions> _auditOptions;
	private readonly IOptions<SecurityMonitoringOptions> _monitoringOptions;
	private readonly ILogger<SecurityAuditor> _logger;
	private SecurityAuditor? _sut;
	private bool _disposed;

	public SecurityAuditorShould()
	{
		_fakeClient = A.Fake<ElasticsearchClient>();
		_auditOptions = Options.Create(new AuditOptions());
		_monitoringOptions = Options.Create(new SecurityMonitoringOptions());
		_logger = NullLogger<SecurityAuditor>.Instance;
	}

	#region Constructor Tests - Null Validation

	[Fact]
	public void ThrowArgumentNullException_WhenElasticsearchClientIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SecurityAuditor(null!, _auditOptions, _monitoringOptions, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenAuditOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SecurityAuditor(_fakeClient, null!, _monitoringOptions, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenMonitoringOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SecurityAuditor(_fakeClient, _auditOptions, null!, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new SecurityAuditor(_fakeClient, _auditOptions, _monitoringOptions, null!));
	}

	#endregion

	#region Constructor Tests - Valid Parameters

	[Fact]
	public void CreateInstance_WithValidParameters()
	{
		// Act
		_sut = new SecurityAuditor(_fakeClient, _auditOptions, _monitoringOptions, _logger);

		// Assert
		_ = _sut.ShouldNotBeNull();
	}

	[Fact]
	public void CreateInstance_WithDefaultAuditOptions()
	{
		// Arrange
		var defaultOptions = Options.Create(new AuditOptions());

		// Act
		_sut = new SecurityAuditor(_fakeClient, defaultOptions, _monitoringOptions, _logger);

		// Assert
		_ = _sut.ShouldNotBeNull();
		_sut.Configuration.Enabled.ShouldBeTrue();
		_sut.IntegrityProtectionEnabled.ShouldBeTrue();
	}

	[Fact]
	public void CreateInstance_WithDefaultMonitoringSettings()
	{
		// Arrange
		var defaultOptions = Options.Create(new SecurityMonitoringOptions());

		// Act
		_sut = new SecurityAuditor(_fakeClient, _auditOptions, defaultOptions, _logger);

		// Assert
		_ = _sut.ShouldNotBeNull();
	}

	#endregion

	#region Configuration Property Tests

	[Fact]
	public void ExposeConfiguration_FromAuditOptions()
	{
		// Arrange
		var auditSettings = new AuditOptions
		{
			Enabled = true,
			AuditAuthentication = true,
			AuditDataAccess = true,
			EnsureLogIntegrity = true
		};
		var options = Options.Create(auditSettings);

		// Act
		_sut = new SecurityAuditor(_fakeClient, options, _monitoringOptions, _logger);

		// Assert
		_sut.Configuration.Enabled.ShouldBeTrue();
		_sut.Configuration.AuditAuthentication.ShouldBeTrue();
		_sut.Configuration.AuditDataAccess.ShouldBeTrue();
		_sut.IntegrityProtectionEnabled.ShouldBeTrue();
	}

	[Fact]
	public void ReflectIntegrityProtection_FromConfiguration()
	{
		// Arrange
		var auditSettings = new AuditOptions { EnsureLogIntegrity = false };
		var options = Options.Create(auditSettings);

		// Act
		_sut = new SecurityAuditor(_fakeClient, options, _monitoringOptions, _logger);

		// Assert
		_sut.IntegrityProtectionEnabled.ShouldBeFalse();
	}

	#endregion

	#region Compliance Framework Tests

	[Fact]
	public void SupportNoFrameworks_WhenNotConfigured()
	{
		// Arrange
		var auditSettings = new AuditOptions { ComplianceFrameworks = [] };
		var options = Options.Create(auditSettings);

		// Act
		_sut = new SecurityAuditor(_fakeClient, options, _monitoringOptions, _logger);

		// Assert
		_sut.SupportedComplianceFrameworks.ShouldBeEmpty();
	}

	[Fact]
	public void SupportGdprFramework_WhenConfigured()
	{
		// Arrange
		var auditSettings = new AuditOptions
		{
			ComplianceFrameworks = [ComplianceFramework.Gdpr]
		};
		var options = Options.Create(auditSettings);

		// Act
		_sut = new SecurityAuditor(_fakeClient, options, _monitoringOptions, _logger);

		// Assert
		_sut.SupportedComplianceFrameworks.ShouldContain(ComplianceFramework.Gdpr);
	}

	[Fact]
	public void SupportHipaaFramework_WhenConfigured()
	{
		// Arrange
		var auditSettings = new AuditOptions
		{
			ComplianceFrameworks = [ComplianceFramework.Hipaa]
		};
		var options = Options.Create(auditSettings);

		// Act
		_sut = new SecurityAuditor(_fakeClient, options, _monitoringOptions, _logger);

		// Assert
		_sut.SupportedComplianceFrameworks.ShouldContain(ComplianceFramework.Hipaa);
	}

	[Fact]
	public void SupportPciDssFramework_WhenConfigured()
	{
		// Arrange
		var auditSettings = new AuditOptions
		{
			ComplianceFrameworks = [ComplianceFramework.PciDss]
		};
		var options = Options.Create(auditSettings);

		// Act
		_sut = new SecurityAuditor(_fakeClient, options, _monitoringOptions, _logger);

		// Assert
		_sut.SupportedComplianceFrameworks.ShouldContain(ComplianceFramework.PciDss);
	}

	[Fact]
	public void SupportSoxFramework_WhenConfigured()
	{
		// Arrange
		var auditSettings = new AuditOptions
		{
			ComplianceFrameworks = [ComplianceFramework.Sox]
		};
		var options = Options.Create(auditSettings);

		// Act
		_sut = new SecurityAuditor(_fakeClient, options, _monitoringOptions, _logger);

		// Assert
		_sut.SupportedComplianceFrameworks.ShouldContain(ComplianceFramework.Sox);
	}

	[Fact]
	public void SupportIso27001Framework_WhenConfigured()
	{
		// Arrange
		var auditSettings = new AuditOptions
		{
			ComplianceFrameworks = [ComplianceFramework.Iso27001]
		};
		var options = Options.Create(auditSettings);

		// Act
		_sut = new SecurityAuditor(_fakeClient, options, _monitoringOptions, _logger);

		// Assert
		_sut.SupportedComplianceFrameworks.ShouldContain(ComplianceFramework.Iso27001);
	}

	[Fact]
	public void SupportNistCsfFramework_WhenConfigured()
	{
		// Arrange
		var auditSettings = new AuditOptions
		{
			ComplianceFrameworks = [ComplianceFramework.NistCsf]
		};
		var options = Options.Create(auditSettings);

		// Act
		_sut = new SecurityAuditor(_fakeClient, options, _monitoringOptions, _logger);

		// Assert
		_sut.SupportedComplianceFrameworks.ShouldContain(ComplianceFramework.NistCsf);
	}

	[Fact]
	public void SupportFismaFramework_WhenConfigured()
	{
		// Arrange
		var auditSettings = new AuditOptions
		{
			ComplianceFrameworks = [ComplianceFramework.Fisma]
		};
		var options = Options.Create(auditSettings);

		// Act
		_sut = new SecurityAuditor(_fakeClient, options, _monitoringOptions, _logger);

		// Assert
		_sut.SupportedComplianceFrameworks.ShouldContain(ComplianceFramework.Fisma);
	}

	[Fact]
	public void SupportMultipleFrameworks_WhenConfigured()
	{
		// Arrange
		var auditSettings = new AuditOptions
		{
			ComplianceFrameworks =
			[
				ComplianceFramework.Gdpr,
				ComplianceFramework.Hipaa,
				ComplianceFramework.PciDss,
				ComplianceFramework.Sox
			]
		};
		var options = Options.Create(auditSettings);

		// Act
		_sut = new SecurityAuditor(_fakeClient, options, _monitoringOptions, _logger);

		// Assert
		_sut.SupportedComplianceFrameworks.Count.ShouldBe(4);
		_sut.SupportedComplianceFrameworks.ShouldContain(ComplianceFramework.Gdpr);
		_sut.SupportedComplianceFrameworks.ShouldContain(ComplianceFramework.Hipaa);
		_sut.SupportedComplianceFrameworks.ShouldContain(ComplianceFramework.PciDss);
		_sut.SupportedComplianceFrameworks.ShouldContain(ComplianceFramework.Sox);
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementIElasticsearchSecurityAuditor()
	{
		// Act
		_sut = new SecurityAuditor(_fakeClient, _auditOptions, _monitoringOptions, _logger);

		// Assert
		_ = _sut.ShouldBeAssignableTo<IElasticsearchSecurityAuditor>();
	}

	[Fact]
	public void ImplementIDisposable()
	{
		// Act
		_sut = new SecurityAuditor(_fakeClient, _auditOptions, _monitoringOptions, _logger);

		// Assert
		_ = _sut.ShouldBeAssignableTo<IDisposable>();
	}

	#endregion

	#region Event Declaration Tests

	[Fact]
	public void DeclareSecurityEventRecordedEvent()
	{
		// Arrange
		_sut = new SecurityAuditor(_fakeClient, _auditOptions, _monitoringOptions, _logger);
		var eventRaised = false;

		// Act - Subscribe to event
		_sut.SecurityEventRecorded += (_, _) => eventRaised = true;

		// Assert - Event subscription should succeed (event is declared)
		eventRaised.ShouldBeFalse(); // Not raised yet, but subscription worked
	}

	[Fact]
	public void DeclareAuditArchiveCompletedEvent()
	{
		// Arrange
		_sut = new SecurityAuditor(_fakeClient, _auditOptions, _monitoringOptions, _logger);
		var eventRaised = false;

		// Act - Subscribe to event
		_sut.AuditArchiveCompleted += (_, _) => eventRaised = true;

		// Assert - Event subscription should succeed (event is declared)
		eventRaised.ShouldBeFalse(); // Not raised yet, but subscription worked
	}

	[Fact]
	public void DeclareComplianceViolationDetectedEvent()
	{
		// Arrange
		_sut = new SecurityAuditor(_fakeClient, _auditOptions, _monitoringOptions, _logger);
		var eventRaised = false;

		// Act - Subscribe to event
		_sut.ComplianceViolationDetected += (_, _) => eventRaised = true;

		// Assert - Event subscription should succeed (event is declared)
		eventRaised.ShouldBeFalse(); // Not raised yet, but subscription worked
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public void Dispose_WithoutThrowingException()
	{
		// Arrange
		_sut = new SecurityAuditor(_fakeClient, _auditOptions, _monitoringOptions, _logger);

		// Act & Assert - Should not throw
		Should.NotThrow(() => _sut.Dispose());
	}

	[Fact]
	public void Dispose_MultipleTimesWithoutThrowingException()
	{
		// Arrange
		_sut = new SecurityAuditor(_fakeClient, _auditOptions, _monitoringOptions, _logger);

		// Act & Assert - Multiple disposes should not throw
		Should.NotThrow(() =>
		{
			_sut.Dispose();
			_sut.Dispose();
			_sut.Dispose();
		});
	}

	#endregion

	#region AuditOptions Configuration Tests

	[Fact]
	public void AcceptCustomRetentionPeriod_InConfiguration()
	{
		// Arrange
		var auditSettings = new AuditOptions
		{
			RetentionPeriod = TimeSpan.FromDays(365) // 1 year
		};
		var options = Options.Create(auditSettings);

		// Act
		_sut = new SecurityAuditor(_fakeClient, options, _monitoringOptions, _logger);

		// Assert
		_sut.Configuration.RetentionPeriod.ShouldBe(TimeSpan.FromDays(365));
	}

	[Fact]
	public void AcceptDefaultRetentionPeriod_WhenNotSpecified()
	{
		// Arrange
		var auditSettings = new AuditOptions();
		var options = Options.Create(auditSettings);

		// Act
		_sut = new SecurityAuditor(_fakeClient, options, _monitoringOptions, _logger);

		// Assert
		// Default is 7 years (2555 days)
		_sut.Configuration.RetentionPeriod.ShouldBe(TimeSpan.FromDays(2555));
	}

	[Fact]
	public void AcceptDisabledAuditConfiguration()
	{
		// Arrange
		var auditSettings = new AuditOptions
		{
			Enabled = false,
			AuditAuthentication = false,
			AuditDataAccess = false,
			AuditConfigurationChanges = false
		};
		var options = Options.Create(auditSettings);

		// Act
		_sut = new SecurityAuditor(_fakeClient, options, _monitoringOptions, _logger);

		// Assert
		_sut.Configuration.Enabled.ShouldBeFalse();
		_sut.Configuration.AuditAuthentication.ShouldBeFalse();
		_sut.Configuration.AuditDataAccess.ShouldBeFalse();
		_sut.Configuration.AuditConfigurationChanges.ShouldBeFalse();
	}

	#endregion

	#region SecurityMonitoringOptions Validation Tests

	[Fact]
	public void AcceptMonitoringSettings_WithAllFeaturesEnabled()
	{
		// Arrange
		var monitoringSettings = new SecurityMonitoringOptions
		{
			Enabled = true,
			DetectAnomalies = true,
			MonitorAuthenticationAttacks = true,
			DetectDataExfiltration = true,
			AutomatedResponseEnabled = true
		};
		var options = Options.Create(monitoringSettings);

		// Act
		_sut = new SecurityAuditor(_fakeClient, _auditOptions, options, _logger);

		// Assert
		_ = _sut.ShouldNotBeNull();
	}

	[Fact]
	public void AcceptMonitoringSettings_WithAllFeaturesDisabled()
	{
		// Arrange
		var monitoringSettings = new SecurityMonitoringOptions
		{
			Enabled = false,
			DetectAnomalies = false,
			MonitorAuthenticationAttacks = false,
			DetectDataExfiltration = false,
			AutomatedResponseEnabled = false
		};
		var options = Options.Create(monitoringSettings);

		// Act
		_sut = new SecurityAuditor(_fakeClient, _auditOptions, options, _logger);

		// Assert
		_ = _sut.ShouldNotBeNull();
	}

	[Fact]
	public void AcceptMonitoringSettings_WithCustomInterval()
	{
		// Arrange
		var monitoringSettings = new SecurityMonitoringOptions
		{
			MonitoringInterval = TimeSpan.FromMinutes(1),
			FailedLoginThreshold = 3
		};
		var options = Options.Create(monitoringSettings);

		// Act
		_sut = new SecurityAuditor(_fakeClient, _auditOptions, options, _logger);

		// Assert
		_ = _sut.ShouldNotBeNull();
	}

	#endregion

	public void Dispose()
	{
		if (!_disposed)
		{
			_sut?.Dispose();
			_disposed = true;
		}
	}
}
