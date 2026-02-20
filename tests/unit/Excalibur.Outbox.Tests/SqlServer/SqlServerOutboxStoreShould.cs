// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Excalibur.Outbox.Tests.SqlServer;

/// <summary>
/// Unit tests for the <see cref="SqlServerOutboxStore"/> class focusing on dual-constructor pattern.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SqlServerOutboxStoreShould : UnitTestBase
{
	private readonly ILogger<SqlServerOutboxStore> _logger = NullLoggerFactory.CreateLogger<SqlServerOutboxStore>();
	private readonly IPayloadSerializer _serializer = A.Fake<IPayloadSerializer>();

	#region Simple Constructor Tests (Options-based)

	[Fact]
	public void SimpleConstructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerOutboxStore(
			options: null!,
			_logger));
	}

	[Fact]
	public void SimpleConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = "Server=localhost;Database=TestDb"
		});

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerOutboxStore(
			options,
			logger: null!));
	}

	[Fact]
	public void SimpleConstructor_WithValidParameters_CreatesInstance()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = "Server=localhost;Database=TestDb"
		});

		// Act
		var store = new SqlServerOutboxStore(options, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void SimpleConstructor_WithSerializer_CreatesInstance()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = "Server=localhost;Database=TestDb"
		});

		// Act
		var store = new SqlServerOutboxStore(options, _serializer, inboxOptions: null, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	#endregion

	#region Advanced Constructor Tests (Connection Factory)

	[Fact]
	public void AdvancedConstructor_WithNullConnectionFactory_ThrowsArgumentNullException()
	{
		// Arrange
		var options = new SqlServerOutboxOptions
		{
			ConnectionString = "Server=localhost;Database=TestDb"
		};

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerOutboxStore(
			connectionFactory: null!,
			options,
			_logger));
	}

	[Fact]
	public void AdvancedConstructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Arrange
		Func<SqlConnection> factory = () => new SqlConnection("Server=localhost");

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerOutboxStore(
			factory,
			options: null!,
			_logger));
	}

	[Fact]
	public void AdvancedConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange
		Func<SqlConnection> factory = () => new SqlConnection("Server=localhost");
		var options = new SqlServerOutboxOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerOutboxStore(
			factory,
			options,
			logger: null!));
	}

	[Fact]
	public void AdvancedConstructor_WithValidParameters_CreatesInstance()
	{
		// Arrange
		Func<SqlConnection> factory = () => new SqlConnection("Server=localhost;Database=TestDb");
		var options = new SqlServerOutboxOptions();

		// Act
		var store = new SqlServerOutboxStore(factory, options, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void AdvancedConstructor_WithSerializer_CreatesInstance()
	{
		// Arrange
		Func<SqlConnection> factory = () => new SqlConnection("Server=localhost;Database=TestDb");
		var options = new SqlServerOutboxOptions();

		// Act
		var store = new SqlServerOutboxStore(factory, options, _serializer, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void AdvancedConstructor_UsesProvidedFactory()
	{
		// Arrange
		var connectionString = "Server=custom;Database=CustomDb";
		var factoryCalled = false;
		Func<SqlConnection> factory = () =>
		{
			factoryCalled = true;
			return new SqlConnection(connectionString);
		};
		var options = new SqlServerOutboxOptions();

		// Act
		var store = new SqlServerOutboxStore(factory, options, _logger);

		// Assert - factory is stored but not called during construction
		_ = store.ShouldNotBeNull();
		factoryCalled.ShouldBeFalse();
	}

	#endregion

	#region Dual Constructor Pattern Consistency Tests

	[Fact]
	public void BothConstructors_CreateEquivalentInstances()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=TestDb";
		var optionsObject = new SqlServerOutboxOptions { ConnectionString = connectionString };

		// Act
		var simpleStore = new SqlServerOutboxStore(
			Microsoft.Extensions.Options.Options.Create(optionsObject),
			_logger);

		var advancedStore = new SqlServerOutboxStore(
			() => new SqlConnection(connectionString),
			optionsObject,
			_logger);

		// Assert - Both should be valid instances
		_ = simpleStore.ShouldNotBeNull();
		_ = advancedStore.ShouldNotBeNull();
	}

	[Fact]
	public void SimpleConstructor_ChainsToAdvancedConstructor()
	{
		// This test verifies the constructor chaining pattern works correctly
		// by ensuring the simple constructor produces a working instance

		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = "Server=(localdb)\\mssqllocaldb;Database=TestDb;Trusted_Connection=true"
		});

		// Act - Creating instance should not throw
		var store = new SqlServerOutboxStore(options, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	#endregion

	#region IOutboxStore Interface Tests

	[Fact]
	public async Task StageMessageAsync_WithNullMessage_ThrowsArgumentNullException()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = "Server=localhost;Database=TestDb"
		});
		var store = new SqlServerOutboxStore(options, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			async () => await store.StageMessageAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task EnqueueAsync_WithNullMessage_ThrowsArgumentNullException()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = "Server=localhost;Database=TestDb"
		});
		var store = new SqlServerOutboxStore(options, _logger);
		var context = A.Fake<Dispatch.Abstractions.IMessageContext>();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			async () => await store.EnqueueAsync(null!, context, CancellationToken.None));
	}

	[Fact]
	public async Task EnqueueAsync_WithNullContext_ThrowsArgumentNullException()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = "Server=localhost;Database=TestDb"
		});
		var store = new SqlServerOutboxStore(options, _logger);
		var message = A.Fake<Dispatch.Abstractions.IDispatchMessage>();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			async () => await store.EnqueueAsync(message, null!, CancellationToken.None));
	}

	[Fact]
	public async Task MarkSentAsync_WithNullMessageId_ThrowsArgumentException()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = "Server=localhost;Database=TestDb"
		});
		var store = new SqlServerOutboxStore(options, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			async () => await store.MarkSentAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task MarkSentAsync_WithEmptyMessageId_ThrowsArgumentException()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = "Server=localhost;Database=TestDb"
		});
		var store = new SqlServerOutboxStore(options, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			async () => await store.MarkSentAsync(string.Empty, CancellationToken.None));
	}

	[Fact]
	public async Task MarkFailedAsync_WithNullMessageId_ThrowsArgumentException()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = "Server=localhost;Database=TestDb"
		});
		var store = new SqlServerOutboxStore(options, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			async () => await store.MarkFailedAsync(null!, "Error", 1, CancellationToken.None));
	}

	[Fact]
	public async Task MarkFailedAsync_WithNullErrorMessage_ThrowsArgumentNullException()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = "Server=localhost;Database=TestDb"
		});
		var store = new SqlServerOutboxStore(options, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			async () => await store.MarkFailedAsync("msg-1", null!, 1, CancellationToken.None));
	}

	#endregion

	#region Multi-Transport Method Tests

	[Fact]
	public async Task MarkTransportSentAsync_WithNullMessageId_ThrowsArgumentException()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = "Server=localhost;Database=TestDb"
		});
		var store = new SqlServerOutboxStore(options, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => store.MarkTransportSentAsync(null!, "transport", CancellationToken.None));
	}

	[Fact]
	public async Task MarkTransportSentAsync_WithNullTransportName_ThrowsArgumentException()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = "Server=localhost;Database=TestDb"
		});
		var store = new SqlServerOutboxStore(options, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => store.MarkTransportSentAsync("msg-1", null!, CancellationToken.None));
	}

	[Fact]
	public async Task MarkTransportFailedAsync_WithNullMessageId_ThrowsArgumentException()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = "Server=localhost;Database=TestDb"
		});
		var store = new SqlServerOutboxStore(options, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => store.MarkTransportFailedAsync(null!, "transport", "error", CancellationToken.None));
	}

	[Fact]
	public async Task MarkTransportFailedAsync_WithNullErrorMessage_ThrowsArgumentNullException()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = "Server=localhost;Database=TestDb"
		});
		var store = new SqlServerOutboxStore(options, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => store.MarkTransportFailedAsync("msg-1", "transport", null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetPendingTransportDeliveriesAsync_WithNullTransportName_ThrowsArgumentException()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = "Server=localhost;Database=TestDb"
		});
		var store = new SqlServerOutboxStore(options, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => store.GetPendingTransportDeliveriesAsync(null!, 100, CancellationToken.None));
	}

	[Fact]
	public async Task StageMessageWithTransportsAsync_WithNullMessage_ThrowsArgumentNullException()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = "Server=localhost;Database=TestDb"
		});
		var store = new SqlServerOutboxStore(options, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => store.StageMessageWithTransportsAsync(null!, [], CancellationToken.None));
	}

	[Fact]
	public async Task StageMessageWithTransportsAsync_WithNullTransports_ThrowsArgumentNullException()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = "Server=localhost;Database=TestDb"
		});
		var store = new SqlServerOutboxStore(options, _logger);
		var message = new Dispatch.Abstractions.OutboundMessage("TestType", [], "dest", new Dictionary<string, object>());

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => store.StageMessageWithTransportsAsync(message, null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetTransportDeliveriesAsync_WithNullMessageId_ThrowsArgumentException()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = "Server=localhost;Database=TestDb"
		});
		var store = new SqlServerOutboxStore(options, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => store.GetTransportDeliveriesAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task MarkTransportSkippedAsync_WithNullMessageId_ThrowsArgumentException()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = "Server=localhost;Database=TestDb"
		});
		var store = new SqlServerOutboxStore(options, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => store.MarkTransportSkippedAsync(null!, "transport", null, CancellationToken.None));
	}

	[Fact]
	public async Task MarkTransportSkippedAsync_WithNullTransportName_ThrowsArgumentException()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = "Server=localhost;Database=TestDb"
		});
		var store = new SqlServerOutboxStore(options, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => store.MarkTransportSkippedAsync("msg-1", null!, null, CancellationToken.None));
	}

	[Fact]
	public async Task GetFailedTransportDeliveriesAsync_WithNullTransportName_ThrowsArgumentException()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = "Server=localhost;Database=TestDb"
		});
		var store = new SqlServerOutboxStore(options, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => store.GetFailedTransportDeliveriesAsync(null!, 3, null, 100, CancellationToken.None));
	}

	[Fact]
	public async Task UpdateAggregateStatusAsync_WithNullMessageId_ThrowsArgumentException()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new SqlServerOutboxOptions
		{
			ConnectionString = "Server=localhost;Database=TestDb"
		});
		var store = new SqlServerOutboxStore(options, _logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => store.UpdateAggregateStatusAsync(null!, CancellationToken.None));
	}

	#endregion
}
