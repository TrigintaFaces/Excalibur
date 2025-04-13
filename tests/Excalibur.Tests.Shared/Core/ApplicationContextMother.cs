using Excalibur.Core;

using System.Reflection;

namespace Excalibur.Tests.Shared.Core;

/// <summary>
/// Helper class for managing ApplicationContext in tests.
/// </summary>
public static class ApplicationContextMother
{
	/// <summary>
	/// Initializes the ApplicationContext with default test values.
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
	/// Initializes the ApplicationContext with specified values.
	/// </summary>
	/// <param name="values">The values to initialize with.</param>
	public static void Initialize(Dictionary<string, string?> values)
	{
		ApplicationContext.Init(values);
	}

	/// <summary>
	/// Resets the ApplicationContext state.
	/// </summary>
	public static void Reset()
	{
		var resetMethod = typeof(ApplicationContext).GetMethod("Reset",
			BindingFlags.NonPublic | BindingFlags.Static);

		if (resetMethod != null)
		{
			_ = resetMethod.Invoke(null, null);
		}
	}

	/// <summary>
	/// Sets a specific value in the ApplicationContext settings directly using reflection.
	/// This should only be used for testing purposes.
	/// </summary>
	/// <param name="key">The key to set.</param>
	/// <param name="value">The value to set.</param>
	public static void SetValue(string key, string value)
	{
		var settingsField = typeof(ApplicationContext).GetField("_settings",
			BindingFlags.Static | BindingFlags.NonPublic);

		if (settingsField?.GetValue(null) is Dictionary<string, string> settings)
		{
			settings[key] = value;
		}
	}
}
