// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace RepositoryChecks;

/// <summary>Finds fixed wall-clock synchronization and short cancellation deadlines using resolved framework symbols.</summary>
internal static class TestTimingRule
{
	private static readonly HashSet<string> Infrastructure = new(StringComparer.Ordinal)
	{
		"tests/Shared/Tests.Shared/Infrastructure/WaitHelpers.cs",
		"tests/Shared/Tests.Shared/Infrastructure/TestTiming.cs",
		"tests/Shared/Tests.Shared/Infrastructure/TestTimeouts.cs",
		"tests/Shared/Tests.Shared/Fixtures/ContainerFixtureBase.cs",
	};

	/// <summary>Returns ledger keys for each resolved timing violation in one evaluated file.</summary>
	internal static string[] Find(Compilation compilation, SyntaxTree tree, string relativePath)
	{
		if (ExemptPath(relativePath))
		{
			return [];
		}

		var core = compilation.GetSpecialType(SpecialType.System_Object).ContainingAssembly;
		var task = core.GetTypeByMetadataName("System.Threading.Tasks.Task");
		var cancellation = core.GetTypeByMetadataName("System.Threading.CancellationTokenSource");
		var model = compilation.GetSemanticModel(tree);
		var hits = new HashSet<string>(StringComparer.Ordinal);
		foreach (var node in tree.GetRoot().DescendantNodes().Where(node =>
			node is InvocationExpressionSyntax or BaseObjectCreationExpressionSyntax))
		{
			var operation = model.GetOperation(node);
			var violation = operation switch
			{
				IInvocationOperation call when SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, task) &&
					string.Equals(call.TargetMethod.Name, "Delay", StringComparison.Ordinal) => !Infinite(call.Arguments.FirstOrDefault(argument => IsDuration(argument.Parameter))?.Value, core) &&
					!TimeoutRace(call, task) && !Justified(node, "delay-ok:"),
				IObjectCreationOperation create when SymbolEqualityComparer.Default.Equals(create.Type, cancellation) =>
					create.Arguments.Any(argument => IsDuration(argument.Parameter) &&
						ShortDeadline(argument.Value, core)) && !Justified(node, "deadline-ok:"),
				_ => false,
			};
			if (violation)
			{
				var line = tree.GetText().Lines.GetLineFromPosition(node.SpanStart).ToString().Trim();
				hits.Add(relativePath.Replace('\\', '/') + "|" + line);
			}
		}

		return hits.Order(StringComparer.Ordinal).ToArray();
	}

	/// <summary>Preserves the small set of timing infrastructure and performance exclusions.</summary>
	internal static bool ExemptPath(string path)
	{
		path = path.Replace('\\', '/');
		return path.StartsWith("tests/performance/", StringComparison.Ordinal) || path.StartsWith("benchmarks/", StringComparison.Ordinal) ||
			Infrastructure.Contains(path);
	}

	/// <summary>Requires a real, local comment with a nonempty justification.</summary>
	internal static bool Justified(SyntaxNode node, string marker)
	{
		var statement = node.Ancestors().OfType<StatementSyntax>().FirstOrDefault();
		var trivia = node.DescendantTrivia().Concat(statement?.GetTrailingTrivia() ?? [])
			.Concat(statement is UsingStatementSyntax resource ? resource.CloseParenToken.TrailingTrivia : []);
		return trivia.Any(item =>
		{
			if (!item.IsKind(SyntaxKind.SingleLineCommentTrivia))
			{
				return false;
			}

			var comment = item.ToString()[2..].Trim();
			return comment.StartsWith(marker, StringComparison.Ordinal) && comment[marker.Length..].Trim().Length > 0;
		});
	}

	/// <summary>Evaluates constant framework durations without interpreting source text.</summary>
	internal static double? Milliseconds(IOperation? operation, IAssemblySymbol core)
	{
		if (operation?.ConstantValue is { HasValue: true, Value: not null } constant && constant.Value is IConvertible)
		{
			return Convert.ToDouble(constant.Value, System.Globalization.CultureInfo.InvariantCulture);
		}

		if (operation is IConversionOperation conversion)
		{
			return Milliseconds(conversion.Operand, core);
		}

		if (operation is IFieldReferenceOperation zero && string.Equals(zero.Field.Name, "Zero", StringComparison.Ordinal) &&
			SymbolEqualityComparer.Default.Equals(zero.Field.ContainingType, core.GetTypeByMetadataName("System.TimeSpan")))
		{
			return 0;
		}

		var spanType = core.GetTypeByMetadataName("System.TimeSpan");
		if (operation is IObjectCreationOperation creation && SymbolEqualityComparer.Default.Equals(creation.Type, spanType))
		{
			return Sum(creation.Arguments, core, firstScale: null);
		}

		if (operation is IInvocationOperation span && SymbolEqualityComparer.Default.Equals(span.TargetMethod.ContainingType, spanType))
		{
			var scale = span.TargetMethod.Name switch
			{
				"FromDays" => 86400000d,
				"FromHours" => 3600000d,
				"FromMinutes" => 60000d,
				"FromSeconds" => 1000d,
				"FromMilliseconds" => 1d,
				"FromMicroseconds" => 0.001d,
				"FromTicks" => 1d / TimeSpan.TicksPerMillisecond,
				_ => double.NaN,
			};
			return double.IsNaN(scale) ? null : Sum(span.Arguments, core, scale);
		}

		return null;
	}

	private static bool TimeoutRace(IInvocationOperation delay, INamedTypeSymbol? task)
	{
		for (var parent = delay.Parent; parent is not null; parent = parent.Parent)
		{
			if (parent is IAnonymousFunctionOperation or ILocalFunctionOperation)
			{
				return false;
			}

			if (parent is IInvocationOperation call)
			{
				return SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, task) && string.Equals(call.TargetMethod.Name, "WhenAny", StringComparison.Ordinal);
			}
		}

		return false;
	}

	private static bool IsDuration(IParameterSymbol? parameter) => string.Equals(parameter?.Name, "delay", StringComparison.Ordinal) ||
		string.Equals(parameter?.Name, "millisecondsDelay", StringComparison.Ordinal);

	private static bool ShortDeadline(IOperation value, IAssemblySymbol core)
	{
		var duration = Milliseconds(value, core);

		// Preserve conservative treatment of unknown millisecond deadlines. A proven >=10s
		// duration satisfies the actual policy regardless of the chosen TimeSpan factory unit.
		return duration is >= 0 and < 10000 || (duration is null && value is IInvocationOperation call &&
			string.Equals(call.TargetMethod.Name, "FromMilliseconds", StringComparison.Ordinal) && SymbolEqualityComparer.Default.Equals(
				call.TargetMethod.ContainingType, core.GetTypeByMetadataName("System.TimeSpan")));
	}

	private static bool Infinite(IOperation? operation, IAssemblySymbol core) => Milliseconds(operation, core) == -1 ||
		(operation is IFieldReferenceOperation field && string.Equals(field.Field.Name, "InfiniteTimeSpan", StringComparison.Ordinal) &&
		SymbolEqualityComparer.Default.Equals(field.Field.ContainingType, core.GetTypeByMetadataName("System.Threading.Timeout")));

	private static double? Sum(IEnumerable<IArgumentOperation> arguments, IAssemblySymbol core, double? firstScale)
	{
		double total = 0;
		foreach (var argument in arguments)
		{
			if (Milliseconds(argument.Value, core) is not { } value)
			{
				return null;
			}

			var scale = argument.Parameter?.Ordinal == 0 && firstScale.HasValue ? firstScale.Value : argument.Parameter?.Name switch
			{
				"ticks" => 1d / TimeSpan.TicksPerMillisecond,
				"days" => 86400000d,
				"hours" => 3600000d,
				"minutes" => 60000d,
				"seconds" => 1000d,
				"milliseconds" => 1d,
				"microseconds" => 0.001d,
				_ => double.NaN,
			};
			total += value * scale;
		}

		return double.IsNaN(total) ? null : total;
	}
}
