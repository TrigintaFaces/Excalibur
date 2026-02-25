// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Linq.Expressions;

using Excalibur.Saga.Correlation;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Tests.Core.Correlation;

internal sealed class CorrelatorTestMessage
{
	public string OrderId { get; set; } = "";
	public string CustomerId { get; set; } = "";
}

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DefaultMultiPropertyCorrelatorShould
{
	private readonly MultiPropertyCorrelationOptions _options;
	private readonly DefaultMultiPropertyCorrelator<CorrelatorTestMessage> _sut;

	public DefaultMultiPropertyCorrelatorShould()
	{
		_options = new MultiPropertyCorrelationOptions
		{
			UseCompositeKey = true,
			RequireAllProperties = true
		};
		_sut = new DefaultMultiPropertyCorrelator<CorrelatorTestMessage>(
			Microsoft.Extensions.Options.Options.Create(_options),
			NullLogger<DefaultMultiPropertyCorrelator<CorrelatorTestMessage>>.Instance);
	}

	[Fact]
	public async Task CorrelateByAsync_GenerateCompositeKeyFromExpressions()
	{
		// Arrange
		Expression<Func<CorrelatorTestMessage, object>>[] expressions =
		[
			m => m.OrderId,
			m => m.CustomerId
		];

		// Act
		var key = await _sut.CorrelateByAsync(expressions, CancellationToken.None);

		// Assert
		key.ShouldBe("OrderId|CustomerId");
	}

	[Fact]
	public async Task CorrelateByAsync_UseSingleKey_WhenCompositeDisabled()
	{
		// Arrange
		_options.UseCompositeKey = false;

		Expression<Func<CorrelatorTestMessage, object>>[] expressions =
		[
			m => m.OrderId,
			m => m.CustomerId
		];

		// Act
		var key = await _sut.CorrelateByAsync(expressions, CancellationToken.None);

		// Assert
		key.ShouldBe("OrderId");
	}

	[Fact]
	public async Task CorrelateByAsync_ThrowOnEmptyExpressions()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.CorrelateByAsync([], CancellationToken.None));
	}

	[Fact]
	public async Task CorrelateByAsync_ThrowOnNullExpressions()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.CorrelateByAsync(null!, CancellationToken.None));
	}

	[Fact]
	public void CorrelateMessage_ExtractValuesFromMessage()
	{
		// Arrange
		var message = new CorrelatorTestMessage { OrderId = "ORD-123", CustomerId = "CUST-456" };
		Expression<Func<CorrelatorTestMessage, object>>[] expressions =
		[
			m => m.OrderId,
			m => m.CustomerId
		];

		// Act
		var key = _sut.CorrelateMessage(message, expressions);

		// Assert
		key.ShouldBe("ORD-123|CUST-456");
	}

	[Fact]
	public void CorrelateMessage_UseSingleValue_WhenCompositeDisabled()
	{
		// Arrange
		_options.UseCompositeKey = false;
		var message = new CorrelatorTestMessage { OrderId = "ORD-123", CustomerId = "CUST-456" };
		Expression<Func<CorrelatorTestMessage, object>>[] expressions =
		[
			m => m.OrderId,
			m => m.CustomerId
		];

		// Act
		var key = _sut.CorrelateMessage(message, expressions);

		// Assert
		key.ShouldBe("ORD-123");
	}

	[Fact]
	public void CorrelateMessage_ThrowOnNullValue_WhenRequireAllProperties()
	{
		// Arrange
		var message = new CorrelatorTestMessage { OrderId = null!, CustomerId = "CUST-456" };
		Expression<Func<CorrelatorTestMessage, object>>[] expressions =
		[
			m => m.OrderId,
		];

		// Act & Assert
		Should.Throw<InvalidOperationException>(
			() => _sut.CorrelateMessage(message, expressions));
	}

	[Fact]
	public void CorrelateMessage_HandleNullValue_WhenRequireAllPropertiesDisabled()
	{
		// Arrange
		_options.RequireAllProperties = false;
		var message = new CorrelatorTestMessage { OrderId = null!, CustomerId = "CUST-456" };
		Expression<Func<CorrelatorTestMessage, object>>[] expressions =
		[
			m => m.OrderId,
		];

		// Act
		var key = _sut.CorrelateMessage(message, expressions);

		// Assert
		key.ShouldBe(string.Empty);
	}

	[Fact]
	public void CorrelateMessage_ThrowOnNull()
	{
		Expression<Func<CorrelatorTestMessage, object>>[] expressions = [m => m.OrderId];

		Should.Throw<ArgumentNullException>(() => _sut.CorrelateMessage(null!, expressions));
		Should.Throw<ArgumentNullException>(() => _sut.CorrelateMessage(new CorrelatorTestMessage(), null!));
	}

	[Fact]
	public void CorrelateMessage_ThrowOnEmptyExpressions()
	{
		Should.Throw<ArgumentException>(
			() => _sut.CorrelateMessage(new CorrelatorTestMessage(), []));
	}

	[Fact]
	public void ThrowOnNullConstructorArgs()
	{
		var opts = Microsoft.Extensions.Options.Options.Create(new MultiPropertyCorrelationOptions());
		var logger = NullLogger<DefaultMultiPropertyCorrelator<CorrelatorTestMessage>>.Instance;

		Should.Throw<ArgumentNullException>(() =>
			new DefaultMultiPropertyCorrelator<CorrelatorTestMessage>(null!, logger));
		Should.Throw<ArgumentNullException>(() =>
			new DefaultMultiPropertyCorrelator<CorrelatorTestMessage>(opts, null!));
	}
}
