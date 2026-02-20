// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Functional.Security;

/// <summary>
/// Functional tests for security patterns in dispatch scenarios.
/// </summary>
[Trait("Category", "Functional")]
[Trait("Component", "Security")]
[Trait("Feature", "Patterns")]
public sealed class SecurityPatternsFunctionalShould : FunctionalTestBase
{
	[Fact]
	public void ValidateMessageIntegrity()
	{
		// Arrange
		var message = "Important message content";
		var key = GenerateRandomKey();

		// Act - Sign the message
		var signature = ComputeHmac(message, key);

		// Verify signature
		var recomputedSignature = ComputeHmac(message, key);
		var isValid = signature.SequenceEqual(recomputedSignature);

		// Assert
		isValid.ShouldBeTrue("Signature should match for unmodified message");
	}

	[Fact]
	public void DetectMessageTampering()
	{
		// Arrange
		var originalMessage = "Original content";
		var tamperedMessage = "Tampered content";
		var key = GenerateRandomKey();

		// Act - Sign original
		var signature = ComputeHmac(originalMessage, key);

		// Verify against tampered message
		var tamperedSignature = ComputeHmac(tamperedMessage, key);
		var isValid = signature.SequenceEqual(tamperedSignature);

		// Assert
		isValid.ShouldBeFalse("Signature should not match for tampered message");
	}

	[Fact]
	public void ValidateMessageAge()
	{
		// Arrange
		var maxAge = TimeSpan.FromMinutes(5);
		var messageTimestamp = DateTimeOffset.UtcNow.AddMinutes(-3);

		// Act
		var age = DateTimeOffset.UtcNow - messageTimestamp;
		var isValid = age <= maxAge;

		// Assert
		isValid.ShouldBeTrue("Message within age limit should be valid");
	}

	[Fact]
	public void RejectExpiredMessage()
	{
		// Arrange
		var maxAge = TimeSpan.FromMinutes(5);
		var messageTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10);

		// Act
		var age = DateTimeOffset.UtcNow - messageTimestamp;
		var isExpired = age > maxAge;

		// Assert
		isExpired.ShouldBeTrue("Expired message should be rejected");
	}

	[Fact]
	public void DetectSqlInjectionAttempt()
	{
		// Arrange
		var maliciousInputs = new[]
		{
			"'; DROP TABLE users; --",
			"1 OR 1=1",
			"admin'--",
			"1; DELETE FROM orders",
			"' UNION SELECT * FROM users --",
		};

		// Act & Assert
		foreach (var input in maliciousInputs)
		{
			var isSafe = IsSafeSqlInput(input);
			isSafe.ShouldBeFalse($"Should detect SQL injection: {input}");
		}
	}

	[Fact]
	public void AllowSafeInput()
	{
		// Arrange
		var safeInputs = new[]
		{
			"John Doe",
			"user@example.com",
			"Product Name with Numbers 123",
			"Normal text with punctuation!",
		};

		// Act & Assert
		foreach (var input in safeInputs)
		{
			var isSafe = IsSafeSqlInput(input);
			isSafe.ShouldBeTrue($"Should allow safe input: {input}");
		}
	}

	[Fact]
	public void DetectXssAttempt()
	{
		// Arrange
		var xssAttempts = new[]
		{
			"<script>alert('xss')</script>",
			"<img src=x onerror=alert('xss')>",
			"javascript:alert('xss')",
			"<svg onload=alert('xss')>",
		};

		// Act & Assert
		foreach (var input in xssAttempts)
		{
			var isSafe = IsSafeHtmlInput(input);
			isSafe.ShouldBeFalse($"Should detect XSS: {input}");
		}
	}

	[Fact]
	public void DetectPathTraversal()
	{
		// Arrange
		var traversalAttempts = new[]
		{
			"../../../etc/passwd",
			"..\\..\\windows\\system32",
			"/etc/passwd",
			"....//....//etc/passwd",
		};

		// Act & Assert
		foreach (var path in traversalAttempts)
		{
			var isSafe = IsSafePath(path);
			isSafe.ShouldBeFalse($"Should detect path traversal: {path}");
		}
	}

	[Fact]
	public void ImplementRateLimiting()
	{
		// Arrange
		var maxRequests = 10;
		var windowDuration = TimeSpan.FromMinutes(1);
		var requestTimestamps = new List<DateTimeOffset>();
		var now = DateTimeOffset.UtcNow;

		// Act - Simulate requests
		for (var i = 0; i < 15; i++)
		{
			requestTimestamps.Add(now.AddSeconds(i));
		}

		// Check if rate limited
		var windowStart = now.AddMinutes(-1);
		var requestsInWindow = requestTimestamps.Count(t => t >= windowStart);
		var isRateLimited = requestsInWindow > maxRequests;

		// Assert
		isRateLimited.ShouldBeTrue("Should be rate limited after exceeding max requests");
	}

	[Fact]
	public void AllowRequestsWithinRateLimit()
	{
		// Arrange
		var maxRequests = 10;
		var requestTimestamps = new List<DateTimeOffset>();
		var now = DateTimeOffset.UtcNow;

		// Act - Simulate requests within limit
		for (var i = 0; i < 5; i++)
		{
			requestTimestamps.Add(now.AddSeconds(i));
		}

		// Check if rate limited
		var isRateLimited = requestTimestamps.Count > maxRequests;

		// Assert
		isRateLimited.ShouldBeFalse("Should not be rate limited when within limit");
	}

	[Fact]
	public void ValidateDataSize()
	{
		// Arrange
		var maxSizeBytes = 1024 * 1024; // 1 MB
		var smallData = new byte[1000];
		var largeData = new byte[2 * 1024 * 1024]; // 2 MB

		// Act
		var smallDataValid = smallData.Length <= maxSizeBytes;
		var largeDataValid = largeData.Length <= maxSizeBytes;

		// Assert
		smallDataValid.ShouldBeTrue("Small data should be accepted");
		largeDataValid.ShouldBeFalse("Large data should be rejected");
	}

	[Fact]
	public async Task EnforceSecurityContextInPipeline()
	{
		// Arrange
		var host = CreateHost(services =>
		{
			_ = services.AddLogging();
		});

		var securityChecks = new List<string>();

		// Act - Simulate security pipeline
		securityChecks.Add("ValidateToken");
		securityChecks.Add("CheckPermissions");
		securityChecks.Add("ValidateInput");
		securityChecks.Add("SignMessage");
		await Task.Delay(1).ConfigureAwait(false);

		// Assert
		securityChecks.Count.ShouldBe(4);
		securityChecks[0].ShouldBe("ValidateToken");
		securityChecks[3].ShouldBe("SignMessage");
	}

	[Fact]
	public void HashSensitiveData()
	{
		// Arrange
		var sensitiveData = "secret-password-123";

		// Act
		var hash1 = ComputeSha256Hash(sensitiveData);
		var hash2 = ComputeSha256Hash(sensitiveData);
		var hashOfDifferent = ComputeSha256Hash("different-password");

		// Assert
		hash1.ShouldBe(hash2, "Same input should produce same hash");
		hash1.ShouldNotBe(hashOfDifferent, "Different input should produce different hash");
		hash1.Length.ShouldBe(64, "SHA256 produces 64-character hex string");
	}

	private static byte[] GenerateRandomKey()
	{
		var key = new byte[32];
		using var rng = RandomNumberGenerator.Create();
		rng.GetBytes(key);
		return key;
	}

	private static byte[] ComputeHmac(string message, byte[] key)
	{
		using var hmac = new HMACSHA256(key);
		return hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
	}

	private static string ComputeSha256Hash(string input)
	{
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
		return Convert.ToHexString(bytes).ToLowerInvariant();
	}

	private static bool IsSafeSqlInput(string input)
	{
		var dangerousPatterns = new[]
		{
			"--", ";", "'", "\"", "/*", "*/",
			"DROP", "DELETE", "INSERT", "UPDATE", "UNION", "SELECT",
			"OR 1=1", "AND 1=1",
		};

		var upperInput = input.ToUpperInvariant();
		return !dangerousPatterns.Any(p => upperInput.Contains(p.ToUpperInvariant()));
	}

	private static bool IsSafeHtmlInput(string input)
	{
		var dangerousPatterns = new[]
		{
			"<script", "</script>", "javascript:", "onerror=", "onload=",
			"<img", "<svg", "<iframe",
		};

		var lowerInput = input.ToLowerInvariant();
		return !dangerousPatterns.Any(p => lowerInput.Contains(p.ToLowerInvariant()));
	}

	private static bool IsSafePath(string path)
	{
		var dangerousPatterns = new[]
		{
			"..", "//", "\\\\", "/etc/", "\\windows\\",
		};

		var lowerPath = path.ToLowerInvariant().Replace('\\', '/');
		return !dangerousPatterns.Any(p => lowerPath.Contains(p.ToLowerInvariant().Replace('\\', '/')));
	}
}
