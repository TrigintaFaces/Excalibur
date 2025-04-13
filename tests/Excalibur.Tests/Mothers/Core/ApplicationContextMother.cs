using System.Reflection;

using Excalibur.Core;

namespace Excalibur.Tests.Mothers.Core;

/// <summary>
///     Helper class for managing ApplicationContext in tests.
/// </summary>
public static class ApplicationContextMother
{
	/// <summary>
	///     Initializes the ApplicationContext with default test values.
	/// </summary>
	public static void InitializeDefault()
	{
		var context = new Dictionary<string, string?>
		{
			{ "ApplicationName", "ExcaliburTest" },
			{ "ApplicationSystemName", "excalibur-test" },
			{ "ServiceAccountName", "test-service-account" }
		};

		ApplicationContext.Init(context);
	}

	/// <summary>
	///     Initializes the ApplicationContext with specified values.
	/// </summary>
	/// <param name="values"> The values to initialize with. </param>
	public static void Initialize(Dictionary<string, string?> values)
	{
		ApplicationContext.Init(values);
	}

	/// <summary>
	///     Resets the ApplicationContext state by using reflection to clear the internal context dictionary.
	/// </summary>
	public static void Reset()
	{
		// Get the private _context field from ApplicationContext
		var contextField = typeof(ApplicationContext).GetField("_context",
			BindingFlags.NonPublic | BindingFlags.Static);

		if (contextField != null)
		{
			// Create a new empty dictionary to replace the existing one
			var emptyContext = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
			contextField.SetValue(null, emptyContext);
		}
	}

	/// <summary>
	///     Sets a value in the ApplicationContext by using reflection to access the internal context dictionary.
	/// </summary>
	/// <param name="key"> The key to set. </param>
	/// <param name="value"> The value to set. </param>
	public static void SetValue(string key, string value)
	{
		var contextField = typeof(ApplicationContext).GetField("_context", BindingFlags.NonPublic | BindingFlags.Static);

		if (contextField == null)
		{
			return;
		}

		if (contextField.GetValue(null) is not Dictionary<string, string?> context)
		{
			context = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
			contextField.SetValue(null, context);
		}

		context[key] = value;
	}
}
