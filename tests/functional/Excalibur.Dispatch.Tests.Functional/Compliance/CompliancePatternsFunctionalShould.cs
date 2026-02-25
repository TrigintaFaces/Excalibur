// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Excalibur.Dispatch.Tests.Functional.Compliance;

/// <summary>
/// Functional tests for compliance patterns (GDPR, PII, audit, jurisdiction).
/// </summary>
[Trait("Category", "Functional")]
[Trait("Component", "Compliance")]
[Trait("Feature", "Patterns")]
public sealed class CompliancePatternsFunctionalShould : FunctionalTestBase
{
	[Fact]
	public void DetectPiiInMessage()
	{
		// Arrange
		var detector = new TestPiiDetector();
		var message = new CustomerMessage
		{
			Name = "John Doe",
			Email = "john.doe@example.com",
			PhoneNumber = "+1-555-123-4567",
			Address = "123 Main St, City, ST 12345",
			CreditCardNumber = "4111-1111-1111-1111",
			SocialSecurityNumber = "123-45-6789",
		};

		// Act
		var detectedFields = detector.Detect(message);

		// Assert
		detectedFields.Count.ShouldBeGreaterThanOrEqualTo(4);
		detectedFields.ShouldContain("Email");
		detectedFields.ShouldContain("PhoneNumber");
		detectedFields.ShouldContain("CreditCardNumber");
		detectedFields.ShouldContain("SocialSecurityNumber");
	}

	[Fact]
	public void MaskPiiFields()
	{
		// Arrange
		var masker = new TestPiiMasker();
		var original = "john.doe@example.com";

		// Act
		var masked = masker.MaskEmail(original);

		// Assert
		masked.ShouldNotBe(original);
		masked.ShouldContain("***");
		masked.ShouldContain("@example.com");
	}

	[Fact]
	public void CreateAuditLogEntry()
	{
		// Arrange
		var auditLog = new ConcurrentQueue<AuditEntry>();
		var entry = new AuditEntry
		{
			Timestamp = DateTimeOffset.UtcNow,
			UserId = "user-123",
			Action = "MessageProcessed",
			ResourceType = "CustomerMessage",
			ResourceId = "msg-456",
			Outcome = AuditOutcome.Success,
			IpAddress = "192.168.1.1",
			UserAgent = "DispatchClient/1.0",
		};

		// Act
		auditLog.Enqueue(entry);

		// Assert
		auditLog.TryDequeue(out var logged).ShouldBeTrue();
		_ = logged.ShouldNotBeNull();
		logged.UserId.ShouldBe("user-123");
		logged.Action.ShouldBe("MessageProcessed");
		logged.Outcome.ShouldBe(AuditOutcome.Success);
	}

	[Fact]
	public void TrackDataAccessForGdprCompliance()
	{
		// Arrange
		var accessLog = new ConcurrentQueue<DataAccessRecord>();
		var record = new DataAccessRecord
		{
			DataSubjectId = "customer-789",
			AccessedAt = DateTimeOffset.UtcNow,
			AccessedBy = "service-account",
			DataCategories = ["PersonalInfo", "ContactDetails"],
			Purpose = "OrderProcessing",
			LegalBasis = "ContractExecution",
		};

		// Act
		accessLog.Enqueue(record);

		// Assert
		accessLog.TryDequeue(out var logged).ShouldBeTrue();
		_ = logged.ShouldNotBeNull();
		logged.DataSubjectId.ShouldBe("customer-789");
		logged.DataCategories.ShouldContain("PersonalInfo");
		logged.LegalBasis.ShouldBe("ContractExecution");
	}

	[Fact]
	public void EnforceDataRetentionPolicy()
	{
		// Arrange
		var retentionPolicy = new DataRetentionPolicy
		{
			DefaultRetentionDays = 365,
			CategoryRetention = new Dictionary<string, int>
			{
				["TransactionData"] = 2555, // 7 years
				["AuditLogs"] = 2555,       // 7 years
				["PersonalData"] = 365,     // 1 year
				["SessionData"] = 30,       // 30 days
			},
		};

		var now = DateTimeOffset.UtcNow;
		var data = new[]
		{
			new TestDataRecord { Category = "TransactionData", CreatedAt = now.AddDays(-3000) },
			new TestDataRecord { Category = "SessionData", CreatedAt = now.AddDays(-31) },
			new TestDataRecord { Category = "PersonalData", CreatedAt = now.AddDays(-100) },
		};

		// Act - Determine which records should be deleted
		var toDelete = data.Where(d =>
		{
			var retention = retentionPolicy.CategoryRetention.TryGetValue(d.Category, out var days)
				? days
				: retentionPolicy.DefaultRetentionDays;
			return d.CreatedAt < now.AddDays(-retention);
		}).ToList();

		// Assert
		toDelete.Count.ShouldBe(2);
		toDelete.ShouldContain(d => d.Category == "TransactionData");
		toDelete.ShouldContain(d => d.Category == "SessionData");
	}

	[Fact]
	public void HandleDataSubjectAccessRequest()
	{
		// Arrange
		var dataStore = new TestDataStore();
		dataStore.Add("customer-123", "PersonalInfo", new { Name = "John Doe", Email = "john@example.com" });
		dataStore.Add("customer-123", "OrderHistory", new { Orders = new[] { "order-1", "order-2" } });
		dataStore.Add("customer-456", "PersonalInfo", new { Name = "Jane Doe", Email = "jane@example.com" });

		// Act - Execute DSAR for customer-123
		var subjectData = dataStore.GetAllDataForSubject("customer-123");

		// Assert
		subjectData.Count.ShouldBe(2);
		subjectData.Keys.ShouldContain("PersonalInfo");
		subjectData.Keys.ShouldContain("OrderHistory");
	}

	[Fact]
	public void HandleDataDeletionRequest()
	{
		// Arrange
		var dataStore = new TestDataStore();
		dataStore.Add("customer-123", "PersonalInfo", new { Name = "John Doe" });
		dataStore.Add("customer-123", "OrderHistory", new { Orders = new[] { "order-1" } });

		// Act - Execute deletion request (right to be forgotten)
		var deleteResult = dataStore.DeleteAllDataForSubject("customer-123");

		// Assert
		deleteResult.DeletedRecords.ShouldBe(2);
		deleteResult.Categories.ShouldContain("PersonalInfo");
		deleteResult.Categories.ShouldContain("OrderHistory");
		dataStore.GetAllDataForSubject("customer-123").Count.ShouldBe(0);
	}

	[Fact]
	public void EnforceJurisdictionRules()
	{
		// Arrange
		var jurisdictionRules = new JurisdictionRules
		{
			AllowedRegions = ["EU", "US", "UK"],
			DataResidencyRequirements = new Dictionary<string, string>
			{
				["EU"] = "eu-west-1",
				["US"] = "us-east-1",
				["UK"] = "eu-west-2",
			},
		};

		// Act & Assert - Check allowed regions
		jurisdictionRules.IsAllowed("EU").ShouldBeTrue();
		jurisdictionRules.IsAllowed("CN").ShouldBeFalse();

		// Check data residency
		jurisdictionRules.GetDataResidency("EU").ShouldBe("eu-west-1");
		jurisdictionRules.GetDataResidency("US").ShouldBe("us-east-1");
	}

	[Fact]
	public void TrackConsentManagement()
	{
		// Arrange
		var consentStore = new TestConsentStore();

		// Act - Record consent
		consentStore.RecordConsent("user-123", "Marketing", granted: true, DateTimeOffset.UtcNow);
		consentStore.RecordConsent("user-123", "Analytics", granted: false, DateTimeOffset.UtcNow);

		// Assert
		consentStore.HasConsent("user-123", "Marketing").ShouldBeTrue();
		consentStore.HasConsent("user-123", "Analytics").ShouldBeFalse();
		consentStore.HasConsent("user-123", "Unknown").ShouldBeFalse();
	}

	[Fact]
	public void EnforceDataClassification()
	{
		// Arrange
		var classifier = new DataClassifier();

		// Act
		var publicData = classifier.Classify("Company news announcement");
		var internalData = classifier.Classify("Internal project status");
		var confidentialData = classifier.Classify("Customer SSN: 123-45-6789");
		var restrictedData = classifier.Classify("Trade secret formula: XYZ-123");

		// Assert
		publicData.ShouldBe(DataClassification.Public);
		internalData.ShouldBe(DataClassification.Internal);
		confidentialData.ShouldBe(DataClassification.Confidential);
		restrictedData.ShouldBe(DataClassification.Restricted);
	}

	[Fact]
	public void GenerateComplianceReport()
	{
		// Arrange
		var auditData = new ComplianceAuditData
		{
			TotalRequests = 10000,
			RequestsWithPii = 2500,
			PiiMaskedRequests = 2400,
			AuditedRequests = 10000,
			ConsentViolations = 5,
			DataAccessRequests = 50,
			DataDeletionRequests = 10,
		};

		// Act
		var report = new ComplianceReport
		{
			GeneratedAt = DateTimeOffset.UtcNow,
			Period = "2026-01",
			PiiDetectionRate = (double)auditData.RequestsWithPii / auditData.TotalRequests,
			PiiMaskingCompliance = (double)auditData.PiiMaskedRequests / auditData.RequestsWithPii,
			AuditCoverage = (double)auditData.AuditedRequests / auditData.TotalRequests,
			ConsentViolationCount = auditData.ConsentViolations,
			DsarRequestsProcessed = auditData.DataAccessRequests + auditData.DataDeletionRequests,
		};

		// Assert
		report.PiiDetectionRate.ShouldBe(0.25);
		report.PiiMaskingCompliance.ShouldBe(0.96);
		report.AuditCoverage.ShouldBe(1.0);
		report.ConsentViolationCount.ShouldBe(5);
		report.DsarRequestsProcessed.ShouldBe(60);
	}

	[Fact]
	public void EnforceDataMinimization()
	{
		// Arrange
		var fullRecord = new FullCustomerRecord
		{
			Id = "cust-123",
			Name = "John Doe",
			Email = "john@example.com",
			Phone = "+1-555-1234",
			Address = "123 Main St",
			CreditCard = "4111-1111-1111-1111",
			SSN = "123-45-6789",
			InternalNotes = "VIP customer",
		};

		// Act - Apply data minimization for external API response
		var minimized = new MinimizedCustomerRecord
		{
			Id = fullRecord.Id,
			Name = fullRecord.Name,
			// Exclude: Email, Phone, Address, CreditCard, SSN, InternalNotes
		};

		// Assert
		minimized.Id.ShouldBe(fullRecord.Id);
		minimized.Name.ShouldBe(fullRecord.Name);
	}

	private sealed class CustomerMessage
	{
		public string Name { get; init; } = string.Empty;
		public string Email { get; init; } = string.Empty;
		public string PhoneNumber { get; init; } = string.Empty;
		public string Address { get; init; } = string.Empty;
		public string CreditCardNumber { get; init; } = string.Empty;
		public string SocialSecurityNumber { get; init; } = string.Empty;
	}

	private sealed class TestPiiDetector
	{
		private static readonly Regex EmailPattern = new(@"[\w.+-]+@[\w-]+\.[\w.-]+", RegexOptions.Compiled);
		private static readonly Regex PhonePattern = new(@"\+?\d[\d\-\s]{9,}", RegexOptions.Compiled);
		private static readonly Regex CreditCardPattern = new(@"\d{4}[\s-]?\d{4}[\s-]?\d{4}[\s-]?\d{4}", RegexOptions.Compiled);
		private static readonly Regex SsnPattern = new(@"\d{3}-\d{2}-\d{4}", RegexOptions.Compiled);

		public List<string> Detect(object obj)
		{
			var detected = new List<string>();
			var properties = obj.GetType().GetProperties();

			foreach (var prop in properties)
			{
				var value = prop.GetValue(obj)?.ToString() ?? string.Empty;

				if (EmailPattern.IsMatch(value))
				{
					detected.Add(prop.Name);
				}
				else if (PhonePattern.IsMatch(value))
				{
					detected.Add(prop.Name);
				}
				else if (CreditCardPattern.IsMatch(value))
				{
					detected.Add(prop.Name);
				}
				else if (SsnPattern.IsMatch(value))
				{
					detected.Add(prop.Name);
				}
			}

			return detected;
		}
	}

	private sealed class TestPiiMasker
	{
		public string MaskEmail(string email)
		{
			var atIndex = email.IndexOf('@');
			if (atIndex <= 1)
			{
				return email;
			}

			var masked = email[0] + "***" + email[(atIndex - 1)..];
			return masked;
		}
	}

	private sealed class AuditEntry
	{
		public DateTimeOffset Timestamp { get; init; }
		public string UserId { get; init; } = string.Empty;
		public string Action { get; init; } = string.Empty;
		public string ResourceType { get; init; } = string.Empty;
		public string ResourceId { get; init; } = string.Empty;
		public AuditOutcome Outcome { get; init; }
		public string IpAddress { get; init; } = string.Empty;
		public string UserAgent { get; init; } = string.Empty;
	}

	private enum AuditOutcome
	{
		Success,
		Failure,
		Denied,
	}

	private sealed class DataAccessRecord
	{
		public string DataSubjectId { get; init; } = string.Empty;
		public DateTimeOffset AccessedAt { get; init; }
		public string AccessedBy { get; init; } = string.Empty;
		public List<string> DataCategories { get; init; } = [];
		public string Purpose { get; init; } = string.Empty;
		public string LegalBasis { get; init; } = string.Empty;
	}

	private sealed class DataRetentionPolicy
	{
		public int DefaultRetentionDays { get; init; }
		public Dictionary<string, int> CategoryRetention { get; init; } = [];
	}

	private sealed class TestDataRecord
	{
		public string Category { get; init; } = string.Empty;
		public DateTimeOffset CreatedAt { get; init; }
	}

	private sealed class TestDataStore
	{
		private readonly ConcurrentDictionary<(string subjectId, string category), object> _data = new();

		public void Add(string subjectId, string category, object data)
		{
			_data[(subjectId, category)] = data;
		}

		public Dictionary<string, object> GetAllDataForSubject(string subjectId)
		{
			return _data
				.Where(kvp => kvp.Key.subjectId == subjectId)
				.ToDictionary(kvp => kvp.Key.category, kvp => kvp.Value);
		}

		public DeletionResult DeleteAllDataForSubject(string subjectId)
		{
			var keysToDelete = _data.Keys.Where(k => k.subjectId == subjectId).ToList();
			var categories = new List<string>();

			foreach (var key in keysToDelete)
			{
				if (_data.TryRemove(key, out _))
				{
					categories.Add(key.category);
				}
			}

			return new DeletionResult
			{
				DeletedRecords = keysToDelete.Count,
				Categories = categories,
			};
		}
	}

	private sealed class DeletionResult
	{
		public int DeletedRecords { get; init; }
		public List<string> Categories { get; init; } = [];
	}

	private sealed class JurisdictionRules
	{
		public List<string> AllowedRegions { get; init; } = [];
		public Dictionary<string, string> DataResidencyRequirements { get; init; } = [];

		public bool IsAllowed(string region) => AllowedRegions.Contains(region);

		public string GetDataResidency(string region) =>
			DataResidencyRequirements.TryGetValue(region, out var residency) ? residency : string.Empty;
	}

	private sealed class TestConsentStore
	{
		private readonly ConcurrentDictionary<(string userId, string purpose), bool> _consents = new();

		public void RecordConsent(string userId, string purpose, bool granted, DateTimeOffset timestamp)
		{
			_consents[(userId, purpose)] = granted;
		}

		public bool HasConsent(string userId, string purpose)
		{
			return _consents.TryGetValue((userId, purpose), out var granted) && granted;
		}
	}

	private sealed class DataClassifier
	{
		public DataClassification Classify(string data)
		{
			if (data.Contains("SSN", StringComparison.OrdinalIgnoreCase) ||
				data.Contains("credit card", StringComparison.OrdinalIgnoreCase))
			{
				return DataClassification.Confidential;
			}

			if (data.Contains("trade secret", StringComparison.OrdinalIgnoreCase) ||
				data.Contains("formula", StringComparison.OrdinalIgnoreCase))
			{
				return DataClassification.Restricted;
			}

			if (data.Contains("internal", StringComparison.OrdinalIgnoreCase) ||
				data.Contains("project", StringComparison.OrdinalIgnoreCase))
			{
				return DataClassification.Internal;
			}

			return DataClassification.Public;
		}
	}

	private enum DataClassification
	{
		Public,
		Internal,
		Confidential,
		Restricted,
	}

	private sealed class ComplianceAuditData
	{
		public int TotalRequests { get; init; }
		public int RequestsWithPii { get; init; }
		public int PiiMaskedRequests { get; init; }
		public int AuditedRequests { get; init; }
		public int ConsentViolations { get; init; }
		public int DataAccessRequests { get; init; }
		public int DataDeletionRequests { get; init; }
	}

	private sealed class ComplianceReport
	{
		public DateTimeOffset GeneratedAt { get; init; }
		public string Period { get; init; } = string.Empty;
		public double PiiDetectionRate { get; init; }
		public double PiiMaskingCompliance { get; init; }
		public double AuditCoverage { get; init; }
		public int ConsentViolationCount { get; init; }
		public int DsarRequestsProcessed { get; init; }
	}

	private sealed class FullCustomerRecord
	{
		public string Id { get; init; } = string.Empty;
		public string Name { get; init; } = string.Empty;
		public string Email { get; init; } = string.Empty;
		public string Phone { get; init; } = string.Empty;
		public string Address { get; init; } = string.Empty;
		public string CreditCard { get; init; } = string.Empty;
		public string SSN { get; init; } = string.Empty;
		public string InternalNotes { get; init; } = string.Empty;
	}

	private sealed class MinimizedCustomerRecord
	{
		public string Id { get; init; } = string.Empty;
		public string Name { get; init; } = string.Empty;
	}
}
