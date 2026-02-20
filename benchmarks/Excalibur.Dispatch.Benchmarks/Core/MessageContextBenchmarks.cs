// -----------------------------------------------------------------------------
// MessageContext Performance Benchmarks
// Sprint 71 - Epic yvpn: MessageContext Performance Optimization
// Task miii: Create baseline performance benchmarks
//
// Purpose: Establish baseline performance metrics BEFORE optimization
// to enable meaningful before/after comparison.
// -----------------------------------------------------------------------------

#pragma warning disable CA1707 // Remove underscores from member names (benchmark naming convention)
#pragma warning disable MA0160 // Use ContainsKey instead of TryGetValue (benchmarking TryGetValue specifically)

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Benchmarks.Core;

/// <summary>
/// Benchmarks for MessageContext property and dictionary access patterns.
/// These benchmarks capture BASELINE performance before Sprint 71 optimizations.
/// </summary>
/// <remarks>
/// Key metrics to track:
/// - Direct property access: Target &lt;2ns post-optimization
/// - Items dictionary access: Current baseline ~30-50ns (expected)
/// - Memory allocations: Target zero for hot-path operations
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
[DisassemblyDiagnoser(maxDepth: 2)]
public class MessageContextBenchmarks
{
	private Messaging.MessageContext _context = null!;
	private const string TestCorrelationId = "test-correlation-id-12345";
	private const string TestUserId = "user-12345";
	private const string TestTenantId = "tenant-98765";
	private const string ItemKey = "CustomItem";
	private const string ItemValue = "CustomValue";

	[GlobalSetup]
	public void Setup()
	{
		_context = new Messaging.MessageContext
		{
			CorrelationId = TestCorrelationId,
			UserId = TestUserId,
			TenantId = TestTenantId,
			MessageId = "msg-001",
			CausationId = "cause-001",
			TraceParent = "00-trace-parent-value",
			Source = "BenchmarkSource",
			MessageType = "BenchmarkMessage",
			ContentType = "application/json",
		};

		// Pre-populate some Items for read benchmarks
		_context.Items["CorrelationId"] = TestCorrelationId;
		_context.Items["UserId"] = TestUserId;
		_context.Items["TenantId"] = TestTenantId;
		_context.Items[ItemKey] = ItemValue;
		_context.Items["Dispatch:Result"] = "SomeResult";
		_context.Items["Validation:Passed"] = true;
		_context.Items["SQS.ReceiptHandle"] = "receipt-handle-value";
		_context.Items["rabbitmq.exchange"] = "test-exchange";
	}

	// =========================================================================
	// SECTION 1: Direct Property Access (Hot Path - Current Implementation)
	// =========================================================================

	[Benchmark(Baseline = true)]
	public string? DirectProperty_CorrelationId() => _context.CorrelationId;

	[Benchmark]
	public string? DirectProperty_UserId() => _context.UserId;

	[Benchmark]
	public string? DirectProperty_TenantId() => _context.TenantId;

	[Benchmark]
	public string? DirectProperty_MessageId() => _context.MessageId;

	[Benchmark]
	public string? DirectProperty_Source() => _context.Source;

	[Benchmark]
	public string? DirectProperty_MessageType() => _context.MessageType;

	// =========================================================================
	// SECTION 2: Items Dictionary Access (Baseline - Potential Optimization)
	// =========================================================================

	[Benchmark]
	public object? ItemsDictionary_CorrelationId() => _context.Items["CorrelationId"];

	[Benchmark]
	public object? ItemsDictionary_UserId() => _context.Items["UserId"];

	[Benchmark]
	public object? ItemsDictionary_TenantId() => _context.Items["TenantId"];

	[Benchmark]
	public object? ItemsDictionary_CustomItem() => _context.Items[ItemKey];

	[Benchmark]
	public object? ItemsDictionary_TransportSpecific_SQS() => _context.Items["SQS.ReceiptHandle"];

	[Benchmark]
	public object? ItemsDictionary_TransportSpecific_RabbitMQ() => _context.Items["rabbitmq.exchange"];

	// =========================================================================
	// SECTION 3: TryGetValue Pattern (Common Safe Access Pattern)
	// =========================================================================

	[Benchmark]
	public bool ItemsDictionary_TryGetValue_Exists()
	{
		return _context.Items.TryGetValue("CorrelationId", out _);
	}

	[Benchmark]
	public bool ItemsDictionary_TryGetValue_NotExists()
	{
		return _context.Items.TryGetValue("NonExistentKey", out _);
	}

	// =========================================================================
	// SECTION 4: ContainsKey Pattern (Guard Checks)
	// =========================================================================

	[Benchmark]
	public bool ItemsDictionary_ContainsKey_Exists()
	{
		return _context.Items.ContainsKey("CorrelationId");
	}

	[Benchmark]
	public bool ItemsDictionary_ContainsKey_NotExists()
	{
		return _context.Items.ContainsKey("NonExistentKey");
	}

	// =========================================================================
	// SECTION 5: Write Operations
	// =========================================================================

	[Benchmark]
	public void DirectProperty_Write_CorrelationId()
	{
		_context.CorrelationId = TestCorrelationId;
	}

	[Benchmark]
	public void ItemsDictionary_Write_NewKey()
	{
		_context.Items["NewKey"] = "NewValue";
	}

	[Benchmark]
	public void ItemsDictionary_Write_ExistingKey()
	{
		_context.Items["CorrelationId"] = TestCorrelationId;
	}

	// =========================================================================
	// SECTION 6: Typed GetItem/SetItem (API Methods)
	// =========================================================================

	[Benchmark]
	public string? GetItem_Typed_String()
	{
		return _context.GetItem<string>(ItemKey);
	}

	[Benchmark]
	public bool GetItem_Typed_Bool()
	{
		return _context.GetItem<bool>("Validation:Passed");
	}

	[Benchmark]
	public void SetItem_Typed()
	{
		_context.SetItem("TypedKey", "TypedValue");
	}

	// =========================================================================
	// SECTION 7: ContainsItem API
	// =========================================================================

	[Benchmark]
	public bool ContainsItem_Exists()
	{
		return _context.ContainsItem(ItemKey);
	}

	[Benchmark]
	public bool ContainsItem_NotExists()
	{
		return _context.ContainsItem("NonExistent");
	}

	// =========================================================================
	// SECTION 8: Compound Operations (Realistic Middleware Patterns)
	// =========================================================================

	/// <summary>
	/// Simulates CachingMiddleware pattern: check existence + read + write result.
	/// </summary>
	[Benchmark]
	public void CompoundOperation_CachingMiddlewarePattern()
	{
		// Typical caching middleware: check, read context, write result
		if (_context.Items.TryGetValue("Dispatch:Result", out var existing))
		{
			_ = existing;
		}

		_context.Items["Dispatch:Result"] = "CachedResult";
		_context.Items["Dispatch:OriginalResult"] = "OriginalResult";
	}

	/// <summary>
	/// Simulates ValidationMiddleware pattern: read properties + write validation state.
	/// </summary>
	[Benchmark]
	public void CompoundOperation_ValidationMiddlewarePattern()
	{
		// Read message properties
		_ = _context.CorrelationId;
		_ = _context.MessageType;
		_ = _context.UserId;

		// Write validation result
		_context.Items["Validation:Passed"] = true;
		_context.Items["Validation:Timestamp"] = DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Simulates transport receiver pattern: populate context from transport metadata.
	/// </summary>
	[Benchmark]
	public void CompoundOperation_TransportReceiverPattern()
	{
		// Simulate populating context from transport-specific metadata
		_context.Items["SQS.ReceiptHandle"] = "handle-123";
		_context.Items["SQS.MessageId"] = "sqs-msg-456";
		_context.Items["SQS.MD5OfBody"] = "md5hash";
		_context.Items["SQS.PollerId"] = "1";
		_context.Items["SQS.ApproximateReceiveCount"] = "1";
	}

	/// <summary>
	/// Simulates full hot-path dispatch: create context, set properties, access in middleware.
	/// </summary>
	[Benchmark]
	public void CompoundOperation_FullHotPathAccess()
	{
		// Read all hot-path properties (simulates middleware pipeline)
		_ = _context.CorrelationId;
		_ = _context.CausationId;
		_ = _context.UserId;
		_ = _context.TenantId;
		_ = _context.MessageId;
		_ = _context.TraceParent;
		_ = _context.Source;
		_ = _context.MessageType;
		_ = _context.ContentType;
	}

	// =========================================================================
	// SECTION 9: Child Context Creation
	// =========================================================================

	[Benchmark]
	public IMessageContext CreateChildContext_Basic()
	{
		return _context.CreateChildContext();
	}
}
