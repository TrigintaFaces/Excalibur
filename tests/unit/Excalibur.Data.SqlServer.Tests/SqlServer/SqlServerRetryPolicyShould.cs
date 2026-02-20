// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;

using RealOptions = Excalibur.Data.SqlServer.SqlServerProviderOptions;

namespace Excalibur.Data.Tests.SqlServer;

[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
public sealed class SqlServerRetryPolicyShould
{
	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new SqlServerRetryPolicy(null!, NullLogger.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		var options = new RealOptions { RetryCount = 3 };

		Should.Throw<ArgumentNullException>(() =>
			new SqlServerRetryPolicy(options, null!));
	}

	[Fact]
	public void SetMaxRetryAttemptsFromOptions()
	{
		// Arrange
		var options = new RealOptions { RetryCount = 5 };

		// Act
		var sut = new SqlServerRetryPolicy(options, NullLogger.Instance);

		// Assert
		sut.MaxRetryAttempts.ShouldBe(5);
	}

	[Fact]
	public void SetBaseRetryDelayToOneSecond()
	{
		// Arrange
		var options = new RealOptions { RetryCount = 3 };

		// Act
		var sut = new SqlServerRetryPolicy(options, NullLogger.Instance);

		// Assert
		sut.BaseRetryDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void ReturnTrueForTimeoutException()
	{
		// Arrange
		var options = new RealOptions { RetryCount = 3 };
		var sut = new SqlServerRetryPolicy(options, NullLogger.Instance);

		// Act & Assert
		sut.ShouldRetry(new TimeoutException()).ShouldBeTrue();
	}

	[Fact]
	public void ReturnTrueForTimeoutExpiredInvalidOperation()
	{
		// Arrange
		var options = new RealOptions { RetryCount = 3 };
		var sut = new SqlServerRetryPolicy(options, NullLogger.Instance);

		// Act & Assert
		sut.ShouldRetry(new InvalidOperationException("Timeout expired")).ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseForNonTransientException()
	{
		// Arrange
		var options = new RealOptions { RetryCount = 3 };
		var sut = new SqlServerRetryPolicy(options, NullLogger.Instance);

		// Act & Assert
		sut.ShouldRetry(new InvalidOperationException("Not transient")).ShouldBeFalse();
	}

	[Fact]
	public void ReturnFalseForArgumentException()
	{
		// Arrange
		var options = new RealOptions { RetryCount = 3 };
		var sut = new SqlServerRetryPolicy(options, NullLogger.Instance);

		// Act & Assert
		sut.ShouldRetry(new ArgumentException("bad arg")).ShouldBeFalse();
	}

	[Fact]
	public void ThrowNotSupportedForDocumentRequests()
	{
		// Arrange
		var options = new RealOptions { RetryCount = 3 };
		var sut = new SqlServerRetryPolicy(options, NullLogger.Instance);
		var request = A.Fake<Excalibur.Data.Abstractions.IDocumentDataRequest<object, string>>();

		// Act & Assert
		Should.Throw<NotSupportedException>(() =>
			sut.ResolveDocumentAsync(request, () => Task.FromResult(new object()), CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenRequestIsNullForResolveAsync()
	{
		// Arrange
		var options = new RealOptions { RetryCount = 3 };
		var sut = new SqlServerRetryPolicy(options, NullLogger.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			sut.ResolveAsync<object, string>(null!, () => Task.FromResult(new object()), CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenConnectionFactoryIsNullForResolveAsync()
	{
		// Arrange
		var options = new RealOptions { RetryCount = 3 };
		var sut = new SqlServerRetryPolicy(options, NullLogger.Instance);
		var request = A.Fake<Excalibur.Data.Abstractions.IDataRequest<object, string>>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			sut.ResolveAsync(request, null!, CancellationToken.None));
	}

	[Fact]
	public void ImplementIDataRequestRetryPolicy()
	{
		// Arrange
		var options = new RealOptions { RetryCount = 3 };

		// Act
		var sut = new SqlServerRetryPolicy(options, NullLogger.Instance);

		// Assert
		sut.ShouldBeAssignableTo<Excalibur.Data.Abstractions.Resilience.IDataRequestRetryPolicy>();
	}

	[Fact]
	public void UseDefaultRetryCountOf3()
	{
		// Arrange
		var options = new RealOptions();

		// Act
		var sut = new SqlServerRetryPolicy(options, NullLogger.Instance);

		// Assert
		sut.MaxRetryAttempts.ShouldBe(3);
	}
}
