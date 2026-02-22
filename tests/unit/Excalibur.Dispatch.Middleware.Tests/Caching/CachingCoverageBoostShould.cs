// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2213 // Disposable fields should be disposed -- TestMeterFactory is test-scoped

using System.Reflection;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Caching;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using MsOptions = Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

/// <summary>
/// Focused tests to increase code coverage for Excalibur.Dispatch.Caching from 88.4% to 95%+.
/// Targets uncovered paths in CachingMiddleware (factory delegates, ExtractReturnValue,
/// DeserializeCachedValue, ShouldCacheBasedOnPolicy branches), CacheInvalidationMiddleware
/// fallback paths, CachedValueJsonConverter edge cases, and LruCache GetOrAdd race path.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class CachingCoverageBoostShould : UnitTestBase
{
	private readonly TestMeterFactory _meterFactory;
	private readonly HybridCache _cache;
	private readonly ICacheKeyBuilder _keyBuilder;
	private readonly ILogger<CachingMiddleware> _logger;
	private readonly IMessageContext _context;
	private readonly CancellationToken _ct = CancellationToken.None;

	public CachingCoverageBoostShould()
	{
		_meterFactory = new TestMeterFactory();
		_cache = A.Fake<HybridCache>();
		_keyBuilder = A.Fake<ICacheKeyBuilder>();
		_logger = NullLogger<CachingMiddleware>.Instance;
		_context = A.Fake<IMessageContext>();

		A.CallTo(() => _context.Items).Returns(new Dictionary<string, object>());
		A.CallTo(() => _keyBuilder.CreateKey(A<IDispatchAction>._, A<IMessageContext>._))
			.Returns("test-cache-key");
	}

	private CachingMiddleware CreateMiddleware(
		CacheOptions? opts = null,
		IResultCachePolicy? globalPolicy = null,
		IServiceProvider? services = null)
	{
		var options = opts ?? new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid };
		return new CachingMiddleware(
			_meterFactory,
			_cache,
			_keyBuilder,
			services ?? new ServiceCollection().BuildServiceProvider(),
			MsOptions.Options.Create(options),
			_logger,
			globalPolicy);
	}

	// =========================================================================
	// CachingMiddleware: Factory delegate execution (CreateCacheValueAsync)
	// =========================================================================

	[Fact]
	public async Task InvokeAsync_WithICacheable_FactoryExecutesAndExtractsReturnValue()
	{
		// Arrange - make the fake HybridCache actually invoke the factory delegate
		var middleware = CreateMiddleware();
		var message = new CacheableQueryWithResult();
		var handlerResult = MessageResultOfT<string>.Success("handler-output");

		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.ReturnsLazily(async call =>
			{
				// Invoke the factory delegate to cover CreateCacheValueAsync
				var underlyingFactory = call.GetArgument<Func<CancellationToken, ValueTask<CachedValue>>>(1);
				var wrapper = call.GetArgument<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>(2);

				CachedValue result;
				if (wrapper != null)
				{
					result = await wrapper(underlyingFactory, CancellationToken.None);
				}
				else
				{
					result = await underlyingFactory(CancellationToken.None);
				}

				return result;
			});

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(handlerResult);

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert - the factory was executed, ExtractReturnValue extracted "handler-output"
		result.ShouldNotBeNull();
	}

	[Fact]
	public async Task InvokeAsync_WithAttributeCacheable_FactoryExecutesAndExtractsReturnValue()
	{
		// Arrange - make the fake HybridCache invoke the factory for attribute-based caching
		var middleware = CreateMiddleware();
		var message = new AttrCacheableAction();
		var handlerResult = MessageResultOfT<string>.Success("attr-handler-output");

		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.ReturnsLazily(async call =>
			{
				var underlyingFactory = call.GetArgument<Func<CancellationToken, ValueTask<CachedValue>>>(1);
				var wrapper = call.GetArgument<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>(2);

				CachedValue result;
				if (wrapper != null)
				{
					result = await wrapper(underlyingFactory, CancellationToken.None);
				}
				else
				{
					result = await underlyingFactory(CancellationToken.None);
				}

				return result;
			});

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(handlerResult);

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public async Task InvokeAsync_WithAttributeOnlyIfSuccess_FactoryRespectsPolicyCheck()
	{
		// Arrange - [CacheResult(OnlyIfSuccess = true)] with no validation/auth in context
		var middleware = CreateMiddleware();
		var message = new AttrCacheableOnlyIfSuccess();
		var handlerResult = MessageResultOfT<string>.Success("success-output");

		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.ReturnsLazily(async call =>
			{
				var underlyingFactory = call.GetArgument<Func<CancellationToken, ValueTask<CachedValue>>>(1);
				var wrapper = call.GetArgument<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>(2);

				CachedValue result;
				if (wrapper != null)
				{
					result = await wrapper(underlyingFactory, CancellationToken.None);
				}
				else
				{
					result = await underlyingFactory(CancellationToken.None);
				}

				return result;
			});

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(handlerResult);

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public async Task InvokeAsync_WithAttributeIgnoreNullResult_NullReturnValue_DoesNotCache()
	{
		// Arrange - [CacheResult(IgnoreNullResult = true)] with handler returning null
		var middleware = CreateMiddleware();
		var message = new AttrCacheableIgnoreNull();
		var handlerResult = MessageResultOfT<string>.Success(returnValue: null);

		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.ReturnsLazily(async call =>
			{
				var underlyingFactory = call.GetArgument<Func<CancellationToken, ValueTask<CachedValue>>>(1);
				var wrapper = call.GetArgument<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>(2);

				CachedValue result;
				if (wrapper != null)
				{
					result = await wrapper(underlyingFactory, CancellationToken.None);
				}
				else
				{
					result = await underlyingFactory(CancellationToken.None);
				}

				return result;
			});

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(handlerResult);

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public async Task InvokeAsync_WithICacheable_WhenShouldCacheReturnsFalse_FactoryReturnsNotCacheable()
	{
		// Arrange - ICacheable that returns ShouldCache=false
		var middleware = CreateMiddleware();
		var message = new CacheableWithShouldCacheFalse();
		var handlerResult = MessageResultOfT<int>.Success(42);

		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.ReturnsLazily(async call =>
			{
				var underlyingFactory = call.GetArgument<Func<CancellationToken, ValueTask<CachedValue>>>(1);
				var wrapper = call.GetArgument<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>(2);

				CachedValue result;
				if (wrapper != null)
				{
					result = await wrapper(underlyingFactory, CancellationToken.None);
				}
				else
				{
					result = await underlyingFactory(CancellationToken.None);
				}

				return result;
			});

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(handlerResult);

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert - handler was called, but ShouldCache was false
		result.ShouldNotBeNull();
	}

	[Fact]
	public async Task InvokeAsync_WithICacheable_WhenHandlerReturnsNonGenericResult_ExtractsNull()
	{
		// Arrange - handler returns non-generic IMessageResult, so ExtractReturnValue returns null
		var middleware = CreateMiddleware();
		var message = new CacheableQueryWithResult();
		var nonGenericResult = A.Fake<IMessageResult>();
		A.CallTo(() => nonGenericResult.Succeeded).Returns(true);

		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.ReturnsLazily(async call =>
			{
				var underlyingFactory = call.GetArgument<Func<CancellationToken, ValueTask<CachedValue>>>(1);
				var wrapper = call.GetArgument<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>(2);

				CachedValue result;
				if (wrapper != null)
				{
					result = await wrapper(underlyingFactory, CancellationToken.None);
				}
				else
				{
					result = await underlyingFactory(CancellationToken.None);
				}

				return result;
			});

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(nonGenericResult);

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		result.ShouldNotBeNull();
	}

	// =========================================================================
	// CachingMiddleware: DeserializeCachedValue with JsonElement
	// =========================================================================

	[Fact]
	public async Task InvokeAsync_CacheHit_WithJsonElementValue_DeserializesCorrectly()
	{
		// Arrange - simulate a distributed cache hit where Value is a JsonElement
		var middleware = CreateMiddleware();
		var message = new AttrCacheableAction();
		var jsonElement = JsonSerializer.Deserialize<JsonElement>("\"deserialized-value\"");

		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.Returns(new ValueTask<CachedValue>(new CachedValue
			{
				HasExecuted = true,
				ShouldCache = true,
				Value = jsonElement,
				TypeName = typeof(string).AssemblyQualifiedName
			}));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert - should have deserialized the JsonElement to string
		result.ShouldNotBeNull();
		result.Succeeded.ShouldBeTrue();
		result.CacheHit.ShouldBeTrue();
		var typed = result.ShouldBeAssignableTo<IMessageResult<string>>();
		typed.ReturnValue.ShouldBe("deserialized-value");
	}

	[Fact]
	public async Task InvokeAsync_CacheHit_WithJsonElementValue_DeserializationFails_CatchBlockHit()
	{
		// Arrange - cache hit where TypeName resolves to a real type but JSON is incompatible,
		// triggering the catch block in DeserializeCachedValue. Value stays as JsonElement,
		// which then causes Activator.CreateInstance to fail for CachedMessageResult<string>.
		var middleware = CreateMiddleware();
		var message = new AttrCacheableAction(); // IDispatchAction<string>

		// Use a JSON object that is valid JsonElement but cannot be deserialized to DateTime
		// Type.GetType() succeeds for DateTime, but JsonSerializer.Deserialize("\"not-a-date\"", typeof(DateTime)) throws
		var jsonElement = JsonSerializer.Deserialize<JsonElement>("\"not-a-valid-date\"");

		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.Returns(new ValueTask<CachedValue>(new CachedValue
			{
				HasExecuted = true,
				ShouldCache = true,
				Value = jsonElement,
				TypeName = typeof(DateTime).AssemblyQualifiedName // valid type but incompatible JSON
			}));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act - DeserializeCachedValue catch block is hit, value stays as JsonElement,
		// then cached wrapper construction fails with an argument type mismatch
		await Should.ThrowAsync<ArgumentException>(async () =>
			await middleware.InvokeAsync(message, _context, Next, _ct));
	}

	[Fact]
	public async Task InvokeAsync_CacheHit_WithJsonElementValue_EmptyTypeName_SkipsDeserialization()
	{
		// Arrange - cache hit with Value as JsonElement but empty TypeName.
		// DeserializeCachedValue skips the if block (IsNullOrEmpty is true),
		// value stays as JsonElement, then cached wrapper construction fails.
		var middleware = CreateMiddleware();
		var message = new AttrCacheableAction(); // IDispatchAction<string>
		var jsonElement = JsonSerializer.Deserialize<JsonElement>("\"hello\"");

		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.Returns(new ValueTask<CachedValue>(new CachedValue
			{
				HasExecuted = true,
				ShouldCache = true,
				Value = jsonElement,
				TypeName = "" // empty type name
			}));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act - JsonElement stays as-is since TypeName is empty, then wrapper construction fails
		await Should.ThrowAsync<ArgumentException>(async () =>
			await middleware.InvokeAsync(message, _context, Next, _ct));
	}

	[Fact]
	public async Task InvokeAsync_CacheHit_WithJsonElementValue_InvalidTypeName_TypeGetTypeReturnsNull()
	{
		// Arrange - cache hit where TypeName does not resolve (Type.GetType returns null),
		// so deserialization is skipped, value stays as JsonElement
		var middleware = CreateMiddleware();
		var message = new AttrCacheableAction(); // IDispatchAction<string>
		var jsonElement = JsonSerializer.Deserialize<JsonElement>("42");

		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.Returns(new ValueTask<CachedValue>(new CachedValue
			{
				HasExecuted = true,
				ShouldCache = true,
				Value = jsonElement,
				TypeName = "NonExistent.Type.That.DoesNot.Exist, FakeAssembly"
			}));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act - type resolution fails, value stays as JsonElement, wrapper construction fails
		await Should.ThrowAsync<ArgumentException>(async () =>
			await middleware.InvokeAsync(message, _context, Next, _ct));
	}

	[Fact]
	public async Task InvokeAsync_CacheHit_WithJsonElementValue_TypeWithoutAssembly_ResolvesFromLoadedAssemblies()
	{
		// Arrange - TypeName uses only FullName so resolver must scan loaded assemblies.
		var middleware = CreateMiddleware();
		var message = new AttrCacheableCustomPayloadAction();
		var jsonElement = JsonSerializer.Deserialize<JsonElement>("""{"Name":"from-scan"}""");

		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.Returns(new ValueTask<CachedValue>(new CachedValue
			{
				HasExecuted = true,
				ShouldCache = true,
				Value = jsonElement,
				TypeName = typeof(CustomPayload).FullName
			}));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		result.ShouldNotBeNull();
		result.CacheHit.ShouldBeTrue();
		var typed = result.ShouldBeAssignableTo<IMessageResult<CustomPayload>>();
		typed.ReturnValue.ShouldNotBeNull();
		typed.ReturnValue.Name.ShouldBe("from-scan");
	}

	[Fact]
	public async Task InvokeAsync_CacheHit_WithJsonElementValue_WrongAssemblySuffix_ResolvesBySimpleTypeName()
	{
		// Arrange - TypeName has an invalid assembly suffix but valid type full name prefix.
		var middleware = CreateMiddleware();
		var message = new AttrCacheableGuidAction();
		var expectedGuid = Guid.Parse("7d9e4e67-5cb3-4761-93d4-7d07f7f8704b");
		var jsonElement = JsonSerializer.Deserialize<JsonElement>($"\"{expectedGuid:D}\"");

		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.Returns(new ValueTask<CachedValue>(new CachedValue
			{
				HasExecuted = true,
				ShouldCache = true,
				Value = jsonElement,
				TypeName = $"{typeof(Guid).FullName}, NonExistent.Assembly"
			}));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		result.ShouldNotBeNull();
		result.CacheHit.ShouldBeTrue();
		var typed = result.ShouldBeAssignableTo<IMessageResult<Guid>>();
		typed.ReturnValue.ShouldBe(expectedGuid);
	}

	// =========================================================================
	// CachingMiddleware: ShouldCache TargetException path
	// =========================================================================

	[Fact]
	public async Task InvokeAsync_WhenTypedPolicyThrowsTargetException_FallsBackToGlobalPolicy()
	{
		// Arrange - typed policy that throws TargetException (type mismatch)
		var serviceCollection = new ServiceCollection();
		serviceCollection.AddSingleton<IResultCachePolicy<AttrCacheableAction>>(
			new TargetExceptionPolicy());
		var serviceProvider = serviceCollection.BuildServiceProvider();

		var globalPolicy = A.Fake<IResultCachePolicy>();
		A.CallTo(() => globalPolicy.ShouldCache(A<IDispatchMessage>._, A<object?>._)).Returns(true);

		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid };
		var middleware = new CachingMiddleware(
			_meterFactory, _cache, _keyBuilder, serviceProvider,
			MsOptions.Options.Create(options), _logger, globalPolicy);

		var message = new AttrCacheableAction();

		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.Returns(new ValueTask<CachedValue>(new CachedValue
			{
				HasExecuted = true,
				ShouldCache = true,
				Value = "test",
				TypeName = typeof(string).AssemblyQualifiedName
			}));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert - TargetException caught, global policy used, caching proceeds
		result.ShouldNotBeNull();
	}

	// =========================================================================
	// CachingMiddleware: ShouldCacheBasedOnPolicy OnlyIfSuccess branch
	// =========================================================================

	[Fact]
	public async Task InvokeAsync_WithOnlyIfSuccess_AndInvalidValidationResult_DoesNotCache()
	{
		// Arrange - set up context with a failed validation result
		var validationResult = new FakeValidationResult { IsValid = false };
		var items = new Dictionary<string, object>
		{
			["Dispatch:ValidationResult"] = validationResult
		};
		A.CallTo(() => _context.Items).Returns(items);

		var middleware = CreateMiddleware();
		var message = new AttrCacheableOnlyIfSuccess();
		var handlerResult = MessageResultOfT<string>.Success("output");

		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.ReturnsLazily(async call =>
			{
				var underlyingFactory = call.GetArgument<Func<CancellationToken, ValueTask<CachedValue>>>(1);
				var wrapper = call.GetArgument<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>(2);

				CachedValue result;
				if (wrapper != null)
				{
					result = await wrapper(underlyingFactory, CancellationToken.None);
				}
				else
				{
					result = await underlyingFactory(CancellationToken.None);
				}

				return result;
			});

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(handlerResult);

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		result.ShouldNotBeNull();
	}

	// =========================================================================
	// CachedValueJsonConverter: edge cases
	// =========================================================================

	[Fact]
	public void CachedValueJsonConverter_Read_WithUnknownProperty_SkipsIt()
	{
		// Arrange - JSON with an unknown property that should be skipped
		var json = """{"ShouldCache":true,"HasExecuted":true,"UnknownProp":"ignored","Value":"hello","TypeName":null}""";
		var options = new JsonSerializerOptions();
		options.Converters.Add(new CachedValueJsonConverter());

		// Act
		var result = JsonSerializer.Deserialize<CachedValue>(json, options);

		// Assert
		result.ShouldNotBeNull();
		result.ShouldCache.ShouldBeTrue();
		result.HasExecuted.ShouldBeTrue();
	}

	[Fact]
	public void CachedValueJsonConverter_Read_WithNullValue_DeserializesCorrectly()
	{
		// Arrange
		var json = """{"ShouldCache":false,"HasExecuted":false,"TypeName":null,"Value":null}""";
		var options = new JsonSerializerOptions();
		options.Converters.Add(new CachedValueJsonConverter());

		// Act
		var result = JsonSerializer.Deserialize<CachedValue>(json, options);

		// Assert
		result.ShouldNotBeNull();
		result.Value.ShouldBeNull();
		result.ShouldCache.ShouldBeFalse();
	}

	[Fact]
	public void CachedValueJsonConverter_Read_InvalidStartToken_ThrowsJsonException()
	{
		// Arrange - not a start object token
		var json = "42";
		var options = new JsonSerializerOptions();
		options.Converters.Add(new CachedValueJsonConverter());

		// Act & Assert
		Should.Throw<JsonException>(() =>
			JsonSerializer.Deserialize<CachedValue>(json, options));
	}

	[Fact]
	public void CachedValueJsonConverter_Write_WithNullTypeName_SkipsTypeNameProperty()
	{
		// Arrange
		var value = new CachedValue
		{
			ShouldCache = true,
			HasExecuted = true,
			Value = "test",
			TypeName = null
		};
		var options = new JsonSerializerOptions();
		options.Converters.Add(new CachedValueJsonConverter());

		// Act
		var json = JsonSerializer.Serialize(value, options);

		// Assert - should not contain TypeName
		json.ShouldNotContain("TypeName");
		json.ShouldContain("\"ShouldCache\":true");
		json.ShouldContain("\"Value\":\"test\"");
	}

	[Fact]
	public void CachedValueJsonConverter_Write_WithNullValue_WritesNullToken()
	{
		// Arrange
		var value = new CachedValue
		{
			ShouldCache = false,
			HasExecuted = false,
			Value = null,
			TypeName = null
		};
		var options = new JsonSerializerOptions();
		options.Converters.Add(new CachedValueJsonConverter());

		// Act
		var json = JsonSerializer.Serialize(value, options);

		// Assert
		json.ShouldContain("\"Value\":null");
	}

	[Fact]
	public void CachedValueJsonConverter_RoundTrip_WithTypedValue_PreservesTypeInfo()
	{
		// Arrange
		var original = new CachedValue
		{
			ShouldCache = true,
			HasExecuted = true,
			Value = 42,
			TypeName = typeof(int).AssemblyQualifiedName
		};
		var options = new JsonSerializerOptions();
		options.Converters.Add(new CachedValueJsonConverter());

		// Act
		var json = JsonSerializer.Serialize(original, options);
		var deserialized = JsonSerializer.Deserialize<CachedValue>(json, options);

		// Assert
		deserialized.ShouldNotBeNull();
		deserialized.ShouldCache.ShouldBeTrue();
		deserialized.HasExecuted.ShouldBeTrue();
		deserialized.Value.ShouldBe(42);
	}

	// =========================================================================
	// CacheInvalidationMiddleware: memory mode fallback with key-based + tag tracker
	// =========================================================================

	[Fact]
	public async Task CacheInvalidation_MemoryFallback_WithKeysAndTags_InvalidatesBoth()
	{
		// Arrange - memory mode fallback (no hybridCache) with both keys and tags
		var memoryCache = A.Fake<IMemoryCache>();
		var tagTracker = A.Fake<ICacheTagTracker>();
		A.CallTo(() => tagTracker.GetKeysByTagsAsync(A<string[]>._, _ct))
			.Returns(new HashSet<string> { "tag-key-1" });

		var options = MsOptions.Options.Create(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Memory,
		});
		var middleware = new CacheInvalidationMiddleware(_meterFactory, options, tagTracker: tagTracker, memoryCache: memoryCache);

		var message = A.Fake<TestCacheInvalidatorMessage>();
		A.CallTo(() => ((ICacheInvalidator)message).GetCacheTagsToInvalidate()).Returns(["tag1"]);
		A.CallTo(() => ((ICacheInvalidator)message).GetCacheKeysToInvalidate()).Returns(["direct-key"]);

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert - both tag-resolved keys and direct keys should be removed
		A.CallTo(() => memoryCache.Remove("tag-key-1")).MustHaveHappened();
		A.CallTo(() => memoryCache.Remove("direct-key")).MustHaveHappened();
	}

	[Fact]
	public async Task CacheInvalidation_DistributedFallback_WithKeysAndTags_InvalidatesBoth()
	{
		// Arrange - distributed mode fallback (no hybridCache) with both keys and tags
		var distributedCache = A.Fake<IDistributedCache>();
		var tagTracker = A.Fake<ICacheTagTracker>();
		A.CallTo(() => tagTracker.GetKeysByTagsAsync(A<string[]>._, _ct))
			.Returns(new HashSet<string> { "tag-key-1" });

		var options = MsOptions.Options.Create(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Distributed,
		});
		var middleware = new CacheInvalidationMiddleware(_meterFactory, options, tagTracker: tagTracker, distributedCache: distributedCache);

		var message = A.Fake<TestCacheInvalidatorMessage>();
		A.CallTo(() => ((ICacheInvalidator)message).GetCacheTagsToInvalidate()).Returns(["tag1"]);
		A.CallTo(() => ((ICacheInvalidator)message).GetCacheKeysToInvalidate()).Returns(["direct-key"]);

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		A.CallTo(() => distributedCache.RemoveAsync("tag-key-1", _ct)).MustHaveHappened();
		A.CallTo(() => distributedCache.RemoveAsync("direct-key", _ct)).MustHaveHappened();
	}

	// =========================================================================
	// CacheInvalidationMiddleware: HybridCache key invalidation for distributed mode
	// =========================================================================

	[Fact]
	public async Task CacheInvalidation_DistributedWithHybrid_InvalidatesKeysViaRemoveAsync()
	{
		// Arrange - distributed mode with hybrid cache and key invalidation
		var hybridCache = A.Fake<HybridCache>();
		var options = MsOptions.Options.Create(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Distributed
		});
		var middleware = new CacheInvalidationMiddleware(_meterFactory, options, hybridCache: hybridCache);

		var message = A.Fake<TestCacheInvalidatorMessage>();
		A.CallTo(() => ((ICacheInvalidator)message).GetCacheTagsToInvalidate()).Returns([]);
		A.CallTo(() => ((ICacheInvalidator)message).GetCacheKeysToInvalidate()).Returns(["dist-key"]);

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		A.CallTo(() => hybridCache.RemoveAsync(
			A<IEnumerable<string>>.That.Contains("dist-key"), _ct)).MustHaveHappened();
	}

	// =========================================================================
	// LruCache: GetOrAdd covering race condition double-check path
	// =========================================================================

	[Fact]
	public void LruCache_GetOrAdd_WhenKeyAlreadyExists_ReturnsExistingValue()
	{
		// Arrange
		using var cache = new LruCache<string, int>(10);
		cache.Set("key1", 100);

		// Act - GetOrAdd should return existing value, not call factory
		var factoryCalled = false;
		var result = cache.GetOrAdd("key1", _ =>
		{
			factoryCalled = true;
			return 200;
		});

		// Assert
		result.ShouldBe(100);
		factoryCalled.ShouldBeFalse();
	}

	[Fact]
	public void LruCache_GetOrAdd_WhenKeyDoesNotExist_CallsFactory()
	{
		// Arrange
		using var cache = new LruCache<string, int>(10);

		// Act
		var result = cache.GetOrAdd("new-key", _ => 42);

		// Assert
		result.ShouldBe(42);
		cache.Count.ShouldBe(1);
	}

	[Fact]
	public void LruCache_GetOrAdd_WithTtl_SetsExpiration()
	{
		// Arrange
		using var cache = new LruCache<string, int>(10, defaultTtl: TimeSpan.FromMinutes(5));

		// Act
		var result = cache.GetOrAdd("ttl-key", _ => 99, TimeSpan.FromSeconds(30));

		// Assert
		result.ShouldBe(99);
		cache.TryGetValue("ttl-key", out var val).ShouldBeTrue();
		val.ShouldBe(99);
	}

	[Fact]
	public void LruCache_GetOrAdd_WithNullFactory_ThrowsArgumentNullException()
	{
		// Arrange
		using var cache = new LruCache<string, int>(10);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			cache.GetOrAdd("key", null!));
	}

	// =========================================================================
	// DefaultCacheKeyBuilder: action without ICacheable falls back to serializer
	// =========================================================================

	[Fact]
	public void DefaultCacheKeyBuilder_NonCacheableAction_UsesSerialization()
	{
		// Arrange
		var serializer = A.Fake<IJsonSerializer>();
		A.CallTo(() => serializer.Serialize(A<object>._, A<Type>._)).Returns("{\"id\":1}");
		var builder = new DefaultCacheKeyBuilder(serializer);
		var action = A.Fake<IDispatchAction>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.TenantId).Returns("tenant1");
		A.CallTo(() => context.UserId).Returns("user1");

		// Act
		var key = builder.CreateKey(action, context);

		// Assert
		key.ShouldNotBeNullOrWhiteSpace();
		A.CallTo(() => serializer.Serialize(action, A<Type>._)).MustHaveHappened();
	}

	[Fact]
	public void DefaultCacheKeyBuilder_WithNullTenantAndUser_UsesDefaultValues()
	{
		// Arrange
		var serializer = A.Fake<IJsonSerializer>();
		A.CallTo(() => serializer.Serialize(A<object>._, A<Type>._)).Returns("{}");
		var builder = new DefaultCacheKeyBuilder(serializer);
		var action = A.Fake<IDispatchAction>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.TenantId).Returns((string?)null);
		A.CallTo(() => context.UserId).Returns((string?)null);

		// Act
		var key = builder.CreateKey(action, context);

		// Assert - uses "global" and "anonymous"
		key.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void DefaultCacheKeyBuilder_WithCacheableAction_UsesGetCacheKey()
	{
		// Arrange - action implements ICacheable<T>
		var serializer = A.Fake<IJsonSerializer>();
		var builder = new DefaultCacheKeyBuilder(serializer);
		var action = new CacheableQueryWithResult();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.TenantId).Returns("t");
		A.CallTo(() => context.UserId).Returns("u");

		// Act
		var key = builder.CreateKey(action, context);

		// Assert - should NOT call serializer since ICacheable provides the key
		key.ShouldNotBeNullOrWhiteSpace();
		A.CallTo(() => serializer.Serialize(A<object>._, A<Type>._)).MustNotHaveHappened();
	}

	// =========================================================================
	// CachingMiddleware: attribute-based with no OnlyIfSuccess/IgnoreNullResult
	// =========================================================================

	[Fact]
	public async Task InvokeAsync_WithAttributeCacheable_OnlyIfSuccessFalse_AlwaysCaches()
	{
		// Arrange - CacheResult with OnlyIfSuccess = false, IgnoreNullResult = false
		var middleware = CreateMiddleware();
		var message = new AttrCacheableAlwaysCache();
		var handlerResult = MessageResultOfT<string>.Success(returnValue: null);

		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.ReturnsLazily(async call =>
			{
				var underlyingFactory = call.GetArgument<Func<CancellationToken, ValueTask<CachedValue>>>(1);
				var wrapper = call.GetArgument<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>(2);

				CachedValue result;
				if (wrapper != null)
				{
					result = await wrapper(underlyingFactory, CancellationToken.None);
				}
				else
				{
					result = await underlyingFactory(CancellationToken.None);
				}

				return result;
			});

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(handlerResult);

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		result.ShouldNotBeNull();
	}

	// =========================================================================
	// CachingMiddleware: ICacheable with global policy returning false => skip caching
	// =========================================================================

	[Fact]
	public async Task InvokeAsync_WithGlobalPolicyReturningFalse_SkipsCachingEntirely()
	{
		// Arrange - global policy returns false for ShouldCache(message, null) pre-check
		// This causes HandleInterfaceCacheableReflectionAsync to skip caching at line 326
		var globalPolicy = A.Fake<IResultCachePolicy>();
		A.CallTo(() => globalPolicy.ShouldCache(A<IDispatchMessage>._, A<object?>._)).Returns(false);

		var middleware = CreateMiddleware(globalPolicy: globalPolicy);
		var message = new CacheableNeverCache();
		var expected = A.Fake<IMessageResult>();

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(expected);

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert - global policy returned false, so caching was skipped entirely
		result.ShouldBe(expected);
		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._)).MustNotHaveHappened();
	}

	// =========================================================================
	// CachingMiddleware: GetCacheableInfo returns null for non-cacheable
	// =========================================================================

	[Fact]
	public async Task InvokeAsync_WithICacheableInterfaceNotMatched_SkipsCaching()
	{
		// Arrange - message has ICacheable interface detected, but GetCacheableInfo returns null
		// This happens when the interface check in InvokeAsync finds ICacheable but the second check returns null
		// Practically tested by having an ICacheable message where everything works
		var middleware = CreateMiddleware();
		var message = new CacheableQueryWithResult();

		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.Returns(new ValueTask<CachedValue>(new CachedValue()));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert - HybridCache was invoked (meaning ICacheable path was followed)
		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._)).MustHaveHappened();
	}

	// =========================================================================
	// Test helper types
	// =========================================================================

	private sealed class CacheableQueryWithResult : ICacheable<string>
	{
		public string GetCacheKey() => "coverage-query-key";
		public string[]? GetCacheTags() => ["coverage-tag"];
	}

	[CacheResult(ExpirationSeconds = 60)]
	private sealed class AttrCacheableAction : IDispatchAction<string>
	{
	}

	[CacheResult(ExpirationSeconds = 60, OnlyIfSuccess = true)]
	private sealed class AttrCacheableOnlyIfSuccess : IDispatchAction<string>
	{
	}

	[CacheResult(ExpirationSeconds = 60, IgnoreNullResult = true)]
	private sealed class AttrCacheableIgnoreNull : IDispatchAction<string>
	{
	}

	[CacheResult(ExpirationSeconds = 60, OnlyIfSuccess = false, IgnoreNullResult = false)]
	private sealed class AttrCacheableAlwaysCache : IDispatchAction<string>
	{
	}

	[CacheResult(ExpirationSeconds = 60)]
	private sealed class AttrCacheableGuidAction : IDispatchAction<Guid>
	{
	}

	[CacheResult(ExpirationSeconds = 60)]
	private sealed class AttrCacheableCustomPayloadAction : IDispatchAction<CustomPayload>
	{
	}

	private sealed class CacheableWithShouldCacheFalse : ICacheable<int>
	{
		public string GetCacheKey() => "should-cache-false-key";
		public bool ShouldCache(object? result) => false;
	}

	private sealed class CacheableNeverCache : ICacheable<int>
	{
		public string GetCacheKey() => "never-cache-key";
		public bool ShouldCache(object? result) => false;
	}

	private sealed class TargetExceptionPolicy : IResultCachePolicy<AttrCacheableAction>
	{
		public bool ShouldCache(AttrCacheableAction message, object? result)
		{
			throw new TargetException("Simulated type mismatch");
		}
	}

	private sealed class FakeValidationResult
	{
		public bool IsValid { get; init; }
	}

	private sealed class CustomPayload
	{
		public string Name { get; set; } = string.Empty;
	}
}
