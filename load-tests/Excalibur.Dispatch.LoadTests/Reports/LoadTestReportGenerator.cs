// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0 - see LICENSE files for details.

using System.Text;
using System.Text.Json;

using Serilog;

namespace Excalibur.Dispatch.LoadTests.Reports;

/// <summary>
/// Generates enhanced HTML reports from NBomber load test results.
/// </summary>
public class LoadTestReportGenerator
{
	private readonly ILogger _logger;
	private readonly SlaThresholds _slaThresholds;

	public LoadTestReportGenerator(ILogger logger, SlaThresholds? slaThresholds = null)
	{
		_logger = logger;
		_slaThresholds = slaThresholds ?? new SlaThresholds();
	}

	/// <summary>
	/// Generates an HTML report from NBomber stats JSON file.
	/// </summary>
	public async Task<string> GenerateReportAsync(string statsJsonPath, string testName, string description)
	{
		_logger.Information("Generating report from {Path}", statsJsonPath);

		if (!File.Exists(statsJsonPath))
		{
			throw new FileNotFoundException($"Stats file not found: {statsJsonPath}");
		}

		var json = await File.ReadAllTextAsync(statsJsonPath);
		var report = ParseNBomberStats(json, testName, description);

		return GenerateHtml(report);
	}

	/// <summary>
	/// Generates an HTML report from load test report data.
	/// </summary>
	public string GenerateReport(LoadTestReport report)
	{
		return GenerateHtml(report);
	}

	/// <summary>
	/// Saves the HTML report to a file.
	/// </summary>
	public async Task SaveReportAsync(string html, string outputPath)
	{
		var directory = Path.GetDirectoryName(outputPath);
		if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
		{
			_ = Directory.CreateDirectory(directory);
		}

		await File.WriteAllTextAsync(outputPath, html);
		_logger.Information("Report saved to {Path}", outputPath);
	}

	private LoadTestReport ParseNBomberStats(string json, string testName, string description)
	{
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;

		var scenarios = new List<ScenarioStats>();

		if (root.TryGetProperty("ScenarioStats", out var scenarioStats))
		{
			foreach (var scenario in scenarioStats.EnumerateArray())
			{
				scenarios.Add(ParseScenario(scenario));
			}
		}

		var startTime = DateTime.UtcNow.AddMinutes(-5); // Default if not in JSON
		var endTime = DateTime.UtcNow;

		if (root.TryGetProperty("StartTime", out var startProp))
		{
			startTime = startProp.GetDateTime();
		}

		if (root.TryGetProperty("EndTime", out var endProp))
		{
			endTime = endProp.GetDateTime();
		}

		var slaResults = ValidateSla(scenarios.ToArray());

		return new LoadTestReport
		{
			TestName = testName,
			Description = description,
			StartTime = startTime,
			EndTime = endTime,
			Duration = endTime - startTime,
			Scenarios = scenarios.ToArray(),
			SlaResults = slaResults
		};
	}

	private static ScenarioStats ParseScenario(JsonElement scenario)
	{
		var name = scenario.GetProperty("ScenarioName").GetString() ?? "Unknown";
		var requestCount = scenario.TryGetProperty("RequestCount", out var rc) ? rc.GetInt32() : 0;
		var okCount = scenario.TryGetProperty("OkCount", out var ok) ? ok.GetInt32() : 0;
		var failCount = scenario.TryGetProperty("FailCount", out var fail) ? fail.GetInt32() : 0;

		var latency = new LatencyStats
		{
			MinMs = GetDouble(scenario, "MinMs"),
			MeanMs = GetDouble(scenario, "MeanMs"),
			MaxMs = GetDouble(scenario, "MaxMs"),
			StdDev = GetDouble(scenario, "StdDev"),
			P50Ms = GetDouble(scenario, "Percent50"),
			P75Ms = GetDouble(scenario, "Percent75"),
			P95Ms = GetDouble(scenario, "Percent95"),
			P99Ms = GetDouble(scenario, "Percent99")
		};

		var rps = scenario.TryGetProperty("RPS", out var rpsProp) ? rpsProp.GetDouble() : 0;
		var allBytes = scenario.TryGetProperty("AllBytes", out var bytes) ? bytes.GetInt64() : 0;

		var statusCodes = new List<StatusCodeStats>();
		if (scenario.TryGetProperty("StatusCodes", out var codes))
		{
			foreach (var code in codes.EnumerateArray())
			{
				var statusCode = code.GetProperty("StatusCode").GetString() ?? "";
				var count = code.GetProperty("Count").GetInt32();
				var pct = requestCount > 0 ? (count * 100.0 / requestCount) : 0;

				statusCodes.Add(new StatusCodeStats
				{
					StatusCode = statusCode,
					Count = count,
					Percentage = pct
				});
			}
		}

		return new ScenarioStats
		{
			Name = name,
			RequestCount = requestCount,
			OkCount = okCount,
			FailCount = failCount,
			Rps = rps,
			Latency = latency,
			DataTransfer = new DataTransferStats { AllBytes = allBytes },
			StatusCodes = statusCodes.ToArray()
		};
	}

	private static double GetDouble(JsonElement element, string propertyName)
	{
		return element.TryGetProperty(propertyName, out var prop) ? prop.GetDouble() : 0;
	}

	private SlaValidation ValidateSla(ScenarioStats[] scenarios)
	{
		var checks = new List<SlaCheck>();

		foreach (var scenario in scenarios)
		{
			var successRate = scenario.RequestCount > 0
				? (scenario.OkCount * 100.0 / scenario.RequestCount)
				: 0;

			checks.Add(new SlaCheck
			{
				Name = $"{scenario.Name} - P95 Latency",
				Description = "95th percentile latency must be below threshold",
				Threshold = _slaThresholds.MaxP95LatencyMs,
				Actual = scenario.Latency.P95Ms,
				Passed = scenario.Latency.P95Ms <= _slaThresholds.MaxP95LatencyMs
			});

			checks.Add(new SlaCheck
			{
				Name = $"{scenario.Name} - P99 Latency",
				Description = "99th percentile latency must be below threshold",
				Threshold = _slaThresholds.MaxP99LatencyMs,
				Actual = scenario.Latency.P99Ms,
				Passed = scenario.Latency.P99Ms <= _slaThresholds.MaxP99LatencyMs
			});

			checks.Add(new SlaCheck
			{
				Name = $"{scenario.Name} - Success Rate",
				Description = "Success rate must exceed minimum threshold",
				Threshold = _slaThresholds.MinSuccessRate,
				Actual = successRate,
				Passed = successRate >= _slaThresholds.MinSuccessRate
			});

			checks.Add(new SlaCheck
			{
				Name = $"{scenario.Name} - Throughput",
				Description = "Requests per second must meet minimum",
				Threshold = _slaThresholds.MinRps,
				Actual = scenario.Rps,
				Passed = scenario.Rps >= _slaThresholds.MinRps
			});
		}

		return new SlaValidation
		{
			Passed = checks.All(c => c.Passed),
			Checks = checks.ToArray()
		};
	}

	private static string GenerateHtml(LoadTestReport report)
	{
		var sb = new StringBuilder();

		_ = sb.AppendLine("<!DOCTYPE html>");
		_ = sb.AppendLine("<html lang=\"en\">");
		_ = sb.AppendLine("<head>");
		_ = sb.AppendLine("    <meta charset=\"UTF-8\">");
		_ = sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
		_ = sb.AppendLine($"    <title>Load Test Report - {report.TestName}</title>");
		_ = sb.AppendLine("    <style>");
		_ = sb.AppendLine(GetCssStyles());
		_ = sb.AppendLine("    </style>");
		_ = sb.AppendLine("</head>");
		_ = sb.AppendLine("<body>");

		// Header
		_ = sb.AppendLine("    <header>");
		_ = sb.AppendLine($"        <h1>🚀 {report.TestName}</h1>");
		_ = sb.AppendLine($"        <p class=\"description\">{report.Description}</p>");
		_ = sb.AppendLine("    </header>");

		// Summary
		_ = sb.AppendLine("    <section class=\"summary\">");
		_ = sb.AppendLine("        <h2>Test Summary</h2>");
		_ = sb.AppendLine("        <div class=\"metrics-grid\">");
		_ = sb.AppendLine($"            <div class=\"metric\"><span class=\"label\">Start Time</span><span class=\"value\">{report.StartTime:yyyy-MM-dd HH:mm:ss} UTC</span></div>");
		_ = sb.AppendLine($"            <div class=\"metric\"><span class=\"label\">End Time</span><span class=\"value\">{report.EndTime:yyyy-MM-dd HH:mm:ss} UTC</span></div>");
		_ = sb.AppendLine($"            <div class=\"metric\"><span class=\"label\">Duration</span><span class=\"value\">{report.Duration:hh\\:mm\\:ss}</span></div>");
		_ = sb.AppendLine($"            <div class=\"metric\"><span class=\"label\">SLA Status</span><span class=\"value {(report.SlaResults.Passed ? "pass" : "fail")}\">{(report.SlaResults.Passed ? "✅ PASSED" : "❌ FAILED")}</span></div>");
		_ = sb.AppendLine("        </div>");
		_ = sb.AppendLine("    </section>");

		// Scenarios
		foreach (var scenario in report.Scenarios)
		{
			AppendScenarioSection(sb, scenario);
		}

		// SLA Results
		AppendSlaSection(sb, report.SlaResults);

		_ = sb.AppendLine("    <footer>");
		_ = sb.AppendLine($"        <p>Generated by Excalibur Load Test Report Generator on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>");
		_ = sb.AppendLine("    </footer>");

		_ = sb.AppendLine("</body>");
		_ = sb.AppendLine("</html>");

		return sb.ToString();
	}

	private static void AppendScenarioSection(StringBuilder sb, ScenarioStats scenario)
	{
		var successRate = scenario.RequestCount > 0
			? (scenario.OkCount * 100.0 / scenario.RequestCount)
			: 0;

		_ = sb.AppendLine($"    <section class=\"scenario\">");
		_ = sb.AppendLine($"        <h2>📊 Scenario: {scenario.Name}</h2>");

		// Key metrics
		_ = sb.AppendLine("        <div class=\"metrics-grid\">");
		_ = sb.AppendLine($"            <div class=\"metric\"><span class=\"label\">Total Requests</span><span class=\"value\">{scenario.RequestCount:N0}</span></div>");
		_ = sb.AppendLine($"            <div class=\"metric\"><span class=\"label\">Successful</span><span class=\"value pass\">{scenario.OkCount:N0}</span></div>");
		_ = sb.AppendLine($"            <div class=\"metric\"><span class=\"label\">Failed</span><span class=\"value {(scenario.FailCount > 0 ? "fail" : "")}\">{scenario.FailCount:N0}</span></div>");
		_ = sb.AppendLine($"            <div class=\"metric\"><span class=\"label\">Success Rate</span><span class=\"value {(successRate >= 99 ? "pass" : "warn")}\">{successRate:F2}%</span></div>");
		_ = sb.AppendLine($"            <div class=\"metric\"><span class=\"label\">RPS</span><span class=\"value\">{scenario.Rps:F1}</span></div>");
		_ = sb.AppendLine($"            <div class=\"metric\"><span class=\"label\">Data Transferred</span><span class=\"value\">{FormatBytes(scenario.DataTransfer.AllBytes)}</span></div>");
		_ = sb.AppendLine("        </div>");

		// Latency table
		_ = sb.AppendLine("        <h3>Latency Distribution</h3>");
		_ = sb.AppendLine("        <table class=\"latency-table\">");
		_ = sb.AppendLine("            <thead><tr><th>Metric</th><th>Value (ms)</th></tr></thead>");
		_ = sb.AppendLine("            <tbody>");
		_ = sb.AppendLine($"                <tr><td>Min</td><td>{scenario.Latency.MinMs:F2}</td></tr>");
		_ = sb.AppendLine($"                <tr><td>Mean</td><td>{scenario.Latency.MeanMs:F2}</td></tr>");
		_ = sb.AppendLine($"                <tr><td>P50 (Median)</td><td>{scenario.Latency.P50Ms:F2}</td></tr>");
		_ = sb.AppendLine($"                <tr><td>P75</td><td>{scenario.Latency.P75Ms:F2}</td></tr>");
		_ = sb.AppendLine($"                <tr><td>P95</td><td>{scenario.Latency.P95Ms:F2}</td></tr>");
		_ = sb.AppendLine($"                <tr><td>P99</td><td>{scenario.Latency.P99Ms:F2}</td></tr>");
		_ = sb.AppendLine($"                <tr><td>Max</td><td>{scenario.Latency.MaxMs:F2}</td></tr>");
		_ = sb.AppendLine($"                <tr><td>Std Dev</td><td>{scenario.Latency.StdDev:F2}</td></tr>");
		_ = sb.AppendLine("            </tbody>");
		_ = sb.AppendLine("        </table>");

		// Latency chart (simple bar chart)
		AppendLatencyChart(sb, scenario.Latency);

		// Status codes
		if (scenario.StatusCodes.Length > 0)
		{
			_ = sb.AppendLine("        <h3>Status Code Distribution</h3>");
			_ = sb.AppendLine("        <table class=\"status-table\">");
			_ = sb.AppendLine("            <thead><tr><th>Status</th><th>Count</th><th>Percentage</th></tr></thead>");
			_ = sb.AppendLine("            <tbody>");
			foreach (var status in scenario.StatusCodes)
			{
				var cssClass = status.StatusCode == "ok" || (status.StatusCode.Length > 0 && status.StatusCode[0] == '2') ? "pass" : "fail";
				_ = sb.AppendLine($"                <tr class=\"{cssClass}\"><td>{status.StatusCode}</td><td>{status.Count:N0}</td><td>{status.Percentage:F1}%</td></tr>");
			}
			_ = sb.AppendLine("            </tbody>");
			_ = sb.AppendLine("        </table>");
		}

		_ = sb.AppendLine("    </section>");
	}

	private static void AppendLatencyChart(StringBuilder sb, LatencyStats latency)
	{
		var maxLatency = Math.Max(latency.MaxMs, 1);
		var scale = 300.0 / maxLatency;

		_ = sb.AppendLine("        <div class=\"chart\">");
		_ = sb.AppendLine("            <svg viewBox=\"0 0 400 200\" class=\"latency-chart\">");

		var percentiles = new[]
		{
			("P50", latency.P50Ms),
			("P75", latency.P75Ms),
			("P95", latency.P95Ms),
			("P99", latency.P99Ms)
		};

		var x = 50;
		foreach (var (name, value) in percentiles)
		{
			var height = value * scale;
			var y = 180 - height;
			var color = name == "P99" ? "#e74c3c" : name == "P95" ? "#f39c12" : "#3498db";

			_ = sb.AppendLine($"                <rect x=\"{x}\" y=\"{y}\" width=\"60\" height=\"{height}\" fill=\"{color}\" />");
			_ = sb.AppendLine($"                <text x=\"{x + 30}\" y=\"195\" text-anchor=\"middle\" class=\"chart-label\">{name}</text>");
			_ = sb.AppendLine($"                <text x=\"{x + 30}\" y=\"{y - 5}\" text-anchor=\"middle\" class=\"chart-value\">{value:F0}ms</text>");

			x += 80;
		}

		_ = sb.AppendLine("            </svg>");
		_ = sb.AppendLine("        </div>");
	}

	private static void AppendSlaSection(StringBuilder sb, SlaValidation sla)
	{
		_ = sb.AppendLine("    <section class=\"sla-results\">");
		_ = sb.AppendLine("        <h2>📋 SLA Validation Results</h2>");
		_ = sb.AppendLine("        <table class=\"sla-table\">");
		_ = sb.AppendLine("            <thead><tr><th>Check</th><th>Description</th><th>Threshold</th><th>Actual</th><th>Status</th></tr></thead>");
		_ = sb.AppendLine("            <tbody>");

		foreach (var check in sla.Checks)
		{
			var status = check.Passed ? "✅ Pass" : "❌ Fail";
			var cssClass = check.Passed ? "pass" : "fail";
			_ = sb.AppendLine($"                <tr class=\"{cssClass}\">");
			_ = sb.AppendLine($"                    <td>{check.Name}</td>");
			_ = sb.AppendLine($"                    <td>{check.Description}</td>");
			_ = sb.AppendLine($"                    <td>{check.Threshold:F2}</td>");
			_ = sb.AppendLine($"                    <td>{check.Actual:F2}</td>");
			_ = sb.AppendLine($"                    <td>{status}</td>");
			_ = sb.AppendLine("                </tr>");
		}

		_ = sb.AppendLine("            </tbody>");
		_ = sb.AppendLine("        </table>");
		_ = sb.AppendLine("    </section>");
	}

	private static string FormatBytes(long bytes)
	{
		string[] sizes = ["B", "KB", "MB", "GB", "TB"];
		var order = 0;
		var size = (double)bytes;

		while (size >= 1024 && order < sizes.Length - 1)
		{
			order++;
			size /= 1024;
		}

		return $"{size:F2} {sizes[order]}";
	}

	private static string GetCssStyles()
	{
		return """
            * { box-sizing: border-box; margin: 0; padding: 0; }
            body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; line-height: 1.6; max-width: 1200px; margin: 0 auto; padding: 20px; background: #f5f5f5; }
            header { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; border-radius: 10px; margin-bottom: 20px; }
            header h1 { font-size: 2em; margin-bottom: 10px; }
            .description { opacity: 0.9; }
            section { background: white; padding: 20px; border-radius: 10px; margin-bottom: 20px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
            h2 { color: #333; margin-bottom: 15px; padding-bottom: 10px; border-bottom: 2px solid #eee; }
            h3 { color: #555; margin: 15px 0 10px; }
            .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 15px; }
            .metric { background: #f8f9fa; padding: 15px; border-radius: 8px; text-align: center; }
            .metric .label { display: block; font-size: 0.85em; color: #666; margin-bottom: 5px; }
            .metric .value { display: block; font-size: 1.3em; font-weight: 600; color: #333; }
            .pass { color: #27ae60 !important; }
            .fail { color: #e74c3c !important; }
            .warn { color: #f39c12 !important; }
            table { width: 100%; border-collapse: collapse; margin: 10px 0; }
            th, td { padding: 12px; text-align: left; border-bottom: 1px solid #eee; }
            th { background: #f8f9fa; font-weight: 600; color: #555; }
            tr:hover { background: #f8f9fa; }
            tr.pass td { background: rgba(39, 174, 96, 0.1); }
            tr.fail td { background: rgba(231, 76, 60, 0.1); }
            .chart { margin: 20px 0; text-align: center; }
            .latency-chart { max-width: 100%; height: auto; }
            .chart-label { font-size: 12px; fill: #666; }
            .chart-value { font-size: 10px; fill: #333; font-weight: 600; }
            footer { text-align: center; color: #888; font-size: 0.85em; padding: 20px; }
            @media (max-width: 600px) { .metrics-grid { grid-template-columns: 1fr 1fr; } }
        """;
	}
}
