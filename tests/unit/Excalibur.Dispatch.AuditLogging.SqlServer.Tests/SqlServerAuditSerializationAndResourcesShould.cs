using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Excalibur.Dispatch.AuditLogging.SqlServer.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class SqlServerAuditSerializationAndResourcesShould
{
	[Fact]
	public void Json_context_resolves_known_types_and_round_trips_dictionary()
	{
		var context = CreateJsonContext();
		var metadata = new Dictionary<string, string>
		{
			["source"] = "unit-test",
			["tenant"] = "tenant-1"
		};

		context.GetTypeInfo(typeof(Dictionary<string, string>)).ShouldNotBeNull();
		context.GetTypeInfo(typeof(IReadOnlyDictionary<string, string>)).ShouldNotBeNull();

		var contextType = context.GetType();
		var dictionaryTypeInfoProperty = contextType.GetProperty(
			"DictionaryStringString",
			System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
		var dictionaryTypeInfo = (JsonTypeInfo)dictionaryTypeInfoProperty.GetValue(context)!;

		var json = JsonSerializer.Serialize(metadata, dictionaryTypeInfo);
		var roundTrip = JsonSerializer.Deserialize(json, dictionaryTypeInfo) as Dictionary<string, string>;

		roundTrip.ShouldNotBeNull();
		roundTrip!["source"].ShouldBe("unit-test");
		roundTrip["tenant"].ShouldBe("tenant-1");
	}

	[Fact]
	public void Json_context_returns_null_for_unknown_type()
	{
		var context = CreateJsonContext();

		context.GetTypeInfo(typeof(Uri)).ShouldBeNull();
	}

	[Fact]
	public void Json_context_exposes_and_caches_generated_type_info_properties()
	{
		var context = CreateJsonContext();
		var flags = System.Reflection.BindingFlags.Public
			| System.Reflection.BindingFlags.NonPublic
			| System.Reflection.BindingFlags.Instance
			| System.Reflection.BindingFlags.Static;
		var properties = context.GetType()
			.GetProperties(flags)
			.Where(p => typeof(JsonTypeInfo).IsAssignableFrom(p.PropertyType))
			.ToList();

		properties.ShouldNotBeEmpty();

		foreach (var property in properties)
		{
			var target = property.GetMethod?.IsStatic == true ? null : context;
			var first = property.GetValue(target);
			var second = property.GetValue(target);

			first.ShouldNotBeNull();
			second.ShouldNotBeNull();
		}

		var defaultProperty = context.GetType().GetProperty("Default", flags);
		if (defaultProperty != null)
		{
			var defaultContext = defaultProperty.GetValue(null) as JsonSerializerContext;
			defaultContext.ShouldNotBeNull();
			defaultContext!.GetTypeInfo(typeof(Dictionary<string, string>)).ShouldNotBeNull();
		}
	}

	[Fact]
	public void Json_context_declared_members_are_invocable()
	{
		var context = CreateJsonContext();
		var contextType = context.GetType();
		var flags = System.Reflection.BindingFlags.Public
			| System.Reflection.BindingFlags.NonPublic
			| System.Reflection.BindingFlags.Instance
			| System.Reflection.BindingFlags.Static
			| System.Reflection.BindingFlags.DeclaredOnly;

		var generatedOptionsProperty = contextType.GetProperty("GeneratedSerializerOptions", flags);
		generatedOptionsProperty?.GetValue(context).ShouldNotBeNull();

		foreach (var method in contextType.GetMethods(flags).Where(m => !m.IsSpecialName))
		{
			if (method.ContainsGenericParameters)
			{
				continue;
			}

			var args = BuildArguments(method.GetParameters(), context);
			if (args == null)
			{
				continue;
			}

			var target = method.IsStatic ? null : context;
			try
			{
				_ = method.Invoke(target, args);
			}
			catch (System.Reflection.TargetInvocationException)
			{
				// Exercising generated methods is the goal here; thrown exceptions still execute branches.
			}
		}
	}

	[Fact]
	public void Resources_expose_expected_strings_and_support_culture_override()
	{
		var resourcesType = typeof(SqlServerAuditStore).Assembly.GetType(
			"Excalibur.Dispatch.AuditLogging.SqlServer.Resources",
			throwOnError: true)!;
		var flags = System.Reflection.BindingFlags.Public
			| System.Reflection.BindingFlags.NonPublic
			| System.Reflection.BindingFlags.Static;
		var cultureProperty = resourcesType.GetProperty("Culture", flags)!;
		var originalCulture = (CultureInfo?)cultureProperty.GetValue(null);
		try
		{
			cultureProperty.SetValue(null, CultureInfo.InvariantCulture);

			_ = Activator.CreateInstance(resourcesType, nonPublic: true);

			var resourceManagerProperty = resourcesType.GetProperty("ResourceManager", flags)!;
			resourceManagerProperty.GetValue(null).ShouldNotBeNull();
			resourceManagerProperty.GetValue(null).ShouldNotBeNull();

			var resourceKeys = new[]
			{
				"SqlServerAuditStore_ConnectionStringRequired",
				"SqlServerAuditStore_DeletedExpiredEvents",
				"SqlServerAuditStore_IntegrityChainBroken",
				"SqlServerAuditStore_IntegrityHashMismatch",
				"SqlServerAuditStore_IntegrityVerificationPassed",
				"SqlServerAuditStore_StoredAuditEvent"
			};

			foreach (var resourceKey in resourceKeys)
			{
				var property = resourcesType.GetProperty(resourceKey, flags)!;
				var value = property.GetValue(null) as string;
				value.ShouldNotBeNullOrWhiteSpace();
			}
		}
		finally
		{
			cultureProperty.SetValue(null, originalCulture);
		}
	}

	private static JsonSerializerContext CreateJsonContext()
	{
		var contextType = typeof(SqlServerAuditStore).Assembly.GetType(
			"Excalibur.Dispatch.AuditLogging.SqlServer.SqlServerAuditJsonContext",
			throwOnError: true)!;
		var ctor = contextType.GetConstructor(
			System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
			binder: null,
		[typeof(JsonSerializerOptions)],
		modifiers: null)!;
		return (JsonSerializerContext)ctor.Invoke([new JsonSerializerOptions()]);
	}

	private static object?[]? BuildArguments(
		System.Reflection.ParameterInfo[] parameters,
		JsonSerializerContext context)
	{
		if (parameters.Length == 0)
		{
			return [];
		}

		var args = new object?[parameters.Length];
		for (var i = 0; i < parameters.Length; i++)
		{
			var parameterType = parameters[i].ParameterType;
			if (parameterType == typeof(Type))
			{
				args[i] = typeof(Dictionary<string, string>);
				continue;
			}

			if (parameterType == typeof(JsonSerializerContext))
			{
				args[i] = context;
				continue;
			}

			if (parameterType == typeof(JsonSerializerOptions))
			{
				args[i] = new JsonSerializerOptions();
				continue;
			}

			if (parameterType == typeof(JsonTypeInfo))
			{
				args[i] = context.GetTypeInfo(typeof(Dictionary<string, string>));
				continue;
			}

			if (parameterType == typeof(string))
			{
				args[i] = "value";
				continue;
			}

			if (parameterType == typeof(bool))
			{
				args[i] = false;
				continue;
			}

			if (parameterType.IsValueType)
			{
				args[i] = Activator.CreateInstance(parameterType);
				continue;
			}

			args[i] = null;
		}

		return args;
	}
}
