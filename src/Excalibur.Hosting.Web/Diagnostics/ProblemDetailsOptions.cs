namespace Excalibur.Hosting.Web.Diagnostics;

public class ProblemDetailsOptions
{
	public string StatusTypeBaseUrl { get; set; } = "https://developer.mozilla.org";

	public HashSet<string> SupportedLocales { get; } = new(StringComparer.OrdinalIgnoreCase)
	{
		"ar",
		"ca",
		"cs",
		"de",
		"el",
		"en-US",
		"es",
		"fa",
		"fr",
		"he",
		"hi",
		"hr",
		"hu",
		"id",
		"it",
		"ja",
		"ko",
		"ms",
		"nl",
		"pl",
		"pt-BR",
		"pt-PT",
		"ro",
		"ru",
		"sl",
		"sr",
		"sv",
		"th",
		"tr",
		"uk",
		"vi",
		"zh-CN",
		"zh-TW"
	};
}
