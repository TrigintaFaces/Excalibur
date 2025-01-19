namespace Excalibur.Application;

/// <summary>
///     Defines a mechanism to determine the applicable validation rule sets based on a given configuration object.
/// </summary>
/// <typeparam name="T"> The type of the configuration object that determines the rule sets. </typeparam>
public interface IDetermineValidationRuleSetByConfig<in T>
{
	/// <summary>
	///     Determines which validation rule sets apply based on the provided configuration object.
	/// </summary>
	/// <param name="config"> The configuration object used to determine the applicable rule sets. </param>
	/// <returns> An array of strings representing the names of the applicable rule sets. </returns>
	string[] WhichRuleSets(T config);
}
