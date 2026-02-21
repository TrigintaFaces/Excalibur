// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.SourceGenerators.Analyzers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace Excalibur.Dispatch.SourceGenerators.Tests.Analyzers;

/// <summary>
/// Unit tests for <see cref="AotCompatibilityAnalyzer"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AotCompatibilityAnalyzerTests
{
	/// <summary>
	/// Verifies DISP003 is reported when Type.GetType is used without AOT annotation.
	/// </summary>
	[Fact]
	public async Task TypeGetType_WithoutAnnotation_ReportsDISP003()
	{
		const string testCode = """
			using System;

			namespace TestNamespace
			{
			    public class TestClass
			    {
			        public void DoSomething()
			        {
			            var type = {|#0:Type.GetType("System.String")|};
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<AotCompatibilityAnalyzer, DefaultVerifier>
		{
			TestCode = testCode,
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.ExpectedDiagnostics.Add(
			new DiagnosticResult(DiagnosticDescriptors.ReflectionWithoutAotAnnotation)
				.WithLocation(0)
				.WithArguments("DoSomething", "GetType"));

		await test.RunAsync();
	}

	/// <summary>
	/// Verifies no diagnostics when method has [RequiresDynamicCode].
	/// </summary>
	[Fact]
	public async Task TypeGetType_WithRequiresDynamicCode_NoDiagnostics()
	{
		const string testCode = """
			using System;
			using System.Diagnostics.CodeAnalysis;

			namespace TestNamespace
			{
			    public class TestClass
			    {
			        [RequiresDynamicCode("Uses reflection")]
			        public void DoSomething()
			        {
			            var type = Type.GetType("System.String");
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<AotCompatibilityAnalyzer, DefaultVerifier>
		{
			TestCode = testCode,
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		// No diagnostics expected
		await test.RunAsync();
	}

	/// <summary>
	/// Verifies no diagnostics when method has [RequiresUnreferencedCode].
	/// </summary>
	[Fact]
	public async Task TypeGetType_WithRequiresUnreferencedCode_NoDiagnostics()
	{
		const string testCode = """
			using System;
			using System.Diagnostics.CodeAnalysis;

			namespace TestNamespace
			{
			    public class TestClass
			    {
			        [RequiresUnreferencedCode("Uses reflection")]
			        public void DoSomething()
			        {
			            var type = Type.GetType("System.String");
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<AotCompatibilityAnalyzer, DefaultVerifier>
		{
			TestCode = testCode,
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		// No diagnostics expected
		await test.RunAsync();
	}

	/// <summary>
	/// Verifies DISP003 is reported for Activator.CreateInstance without annotation.
	/// </summary>
	[Fact]
	public async Task ActivatorCreateInstance_WithoutAnnotation_ReportsDISP003()
	{
		const string testCode = """
			using System;

			namespace TestNamespace
			{
			    public class TestClass
			    {
			        public object Create(Type type)
			        {
			            return {|#0:Activator.CreateInstance(type)|}!;
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<AotCompatibilityAnalyzer, DefaultVerifier>
		{
			TestCode = testCode,
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.ExpectedDiagnostics.Add(
			new DiagnosticResult(DiagnosticDescriptors.ReflectionWithoutAotAnnotation)
				.WithLocation(0)
				.WithArguments("Create", "CreateInstance"));

		await test.RunAsync();
	}

	/// <summary>
	/// Verifies DISP003 is reported for MakeGenericType without annotation.
	/// </summary>
	[Fact]
	public async Task MakeGenericType_WithoutAnnotation_ReportsDISP003()
	{
		const string testCode = """
			using System;
			using System.Collections.Generic;

			namespace TestNamespace
			{
			    public class TestClass
			    {
			        public Type MakeList(Type elementType)
			        {
			            return {|#0:typeof(List<>).MakeGenericType(elementType)|};
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<AotCompatibilityAnalyzer, DefaultVerifier>
		{
			TestCode = testCode,
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.ExpectedDiagnostics.Add(
			new DiagnosticResult(DiagnosticDescriptors.ReflectionWithoutAotAnnotation)
				.WithLocation(0)
				.WithArguments("MakeList", "MakeGenericType"));

		await test.RunAsync();
	}

	/// <summary>
	/// Verifies no diagnostics when class has [RequiresDynamicCode].
	/// </summary>
	[Fact]
	public async Task TypeGetType_WithClassLevelAnnotation_NoDiagnostics()
	{
		const string testCode = """
			using System;
			using System.Diagnostics.CodeAnalysis;

			namespace TestNamespace
			{
			    [RequiresDynamicCode("This class uses reflection")]
			    public class TestClass
			    {
			        public void DoSomething()
			        {
			            var type = Type.GetType("System.String");
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<AotCompatibilityAnalyzer, DefaultVerifier>
		{
			TestCode = testCode,
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		// No diagnostics expected
		await test.RunAsync();
	}

	/// <summary>
	/// Verifies DISP003 is reported for MethodInfo.MakeGenericMethod without annotation.
	/// </summary>
	[Fact]
	public async Task MakeGenericMethod_WithoutAnnotation_ReportsDISP003()
	{
		const string testCode = """
			using System;
			using System.Reflection;

			namespace TestNamespace
			{
			    public class TestClass
			    {
			        public MethodInfo MakeGeneric(MethodInfo method, Type typeArg)
			        {
			            return {|#0:method.MakeGenericMethod(typeArg)|};
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<AotCompatibilityAnalyzer, DefaultVerifier>
		{
			TestCode = testCode,
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.ExpectedDiagnostics.Add(
			new DiagnosticResult(DiagnosticDescriptors.ReflectionWithoutAotAnnotation)
				.WithLocation(0)
				.WithArguments("MakeGeneric", "MakeGenericMethod"));

		await test.RunAsync();
	}

	/// <summary>
	/// Verifies DISP003 is reported for Assembly.GetType without annotation.
	/// </summary>
	[Fact]
	public async Task AssemblyGetType_WithoutAnnotation_ReportsDISP003()
	{
		const string testCode = """
			using System;
			using System.Reflection;

			namespace TestNamespace
			{
			    public class TestClass
			    {
			        public Type? FindType(Assembly assembly)
			        {
			            return {|#0:assembly.GetType("System.String")|};
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<AotCompatibilityAnalyzer, DefaultVerifier>
		{
			TestCode = testCode,
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.ExpectedDiagnostics.Add(
			new DiagnosticResult(DiagnosticDescriptors.ReflectionWithoutAotAnnotation)
				.WithLocation(0)
				.WithArguments("FindType", "GetType"));

		await test.RunAsync();
	}

	/// <summary>
	/// Verifies no diagnostics when method has [UnconditionalSuppressMessage].
	/// </summary>
	[Fact]
	public async Task TypeGetType_WithUnconditionalSuppressMessage_NoDiagnostics()
	{
		const string testCode = """
			using System;
			using System.Diagnostics.CodeAnalysis;

			namespace TestNamespace
			{
			    public class TestClass
			    {
			        [UnconditionalSuppressMessage("AOT", "IL2057")]
			        public void DoSomething()
			        {
			            var type = Type.GetType("System.String");
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<AotCompatibilityAnalyzer, DefaultVerifier>
		{
			TestCode = testCode,
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		// No diagnostics expected
		await test.RunAsync();
	}

	/// <summary>
	/// Verifies no diagnostics when class has [RequiresUnreferencedCode] at class level.
	/// </summary>
	[Fact]
	public async Task TypeGetType_WithClassLevelRequiresUnreferencedCode_NoDiagnostics()
	{
		const string testCode = """
			using System;
			using System.Diagnostics.CodeAnalysis;

			namespace TestNamespace
			{
			    [RequiresUnreferencedCode("This class uses reflection")]
			    public class TestClass
			    {
			        public void DoSomething()
			        {
			            var type = Type.GetType("System.String");
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<AotCompatibilityAnalyzer, DefaultVerifier>
		{
			TestCode = testCode,
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		// No diagnostics expected
		await test.RunAsync();
	}

	/// <summary>
	/// Verifies no diagnostics for non-reflection method invocations.
	/// </summary>
	[Fact]
	public async Task NonReflectionMethod_NoDiagnostics()
	{
		const string testCode = """
			using System;

			namespace TestNamespace
			{
			    public class TestClass
			    {
			        public string DoSomething()
			        {
			            return "hello".ToString();
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<AotCompatibilityAnalyzer, DefaultVerifier>
		{
			TestCode = testCode,
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		// No diagnostics expected for non-reflection methods
		await test.RunAsync();
	}

	/// <summary>
	/// Verifies DISP003 is reported for reflection inside a property getter.
	/// </summary>
	[Fact]
	public async Task ReflectionInPropertyGetter_ReportsDISP003()
	{
		const string testCode = """
			using System;

			namespace TestNamespace
			{
			    public class TestClass
			    {
			        public Type? MyType
			        {
			            get
			            {
			                return {|#0:Type.GetType("System.String")|};
			            }
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<AotCompatibilityAnalyzer, DefaultVerifier>
		{
			TestCode = testCode,
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.ExpectedDiagnostics.Add(
			new DiagnosticResult(DiagnosticDescriptors.ReflectionWithoutAotAnnotation)
				.WithLocation(0)
				.WithArguments("get_MyType", "GetType"));

		await test.RunAsync();
	}

	/// <summary>
	/// Verifies no diagnostics when method has [DynamicallyAccessedMembers] attribute.
	/// Uses the attribute on a parameter since that is the typical usage pattern,
	/// but the analyzer checks the containing method's attributes.
	/// </summary>
	[Fact]
	public async Task TypeGetType_WithDynamicallyAccessedMembers_OnMethod_NoDiagnostics()
	{
		// DynamicallyAccessedMembersAttribute is typically on parameters/return values,
		// but the analyzer checks for it on the method itself.
		// We need to create a custom attribute class to test this branch.
		const string testCode = """
			using System;

			namespace System.Diagnostics.CodeAnalysis
			{
			    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter)]
			    public class DynamicallyAccessedMembersAttribute : Attribute
			    {
			        public DynamicallyAccessedMembersAttribute(int memberTypes) { }
			    }
			}

			namespace TestNamespace
			{
			    public class TestClass
			    {
			        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(0)]
			        public void DoSomething()
			        {
			            var type = Type.GetType("System.String");
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<AotCompatibilityAnalyzer, DefaultVerifier>
		{
			TestCode = testCode,
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		// No diagnostics expected - DynamicallyAccessedMembersAttribute suppresses the warning
		await test.RunAsync();
	}

	/// <summary>
	/// Verifies DISP003 is reported for reflection in a lambda/local function
	/// where the containing method does not have AOT annotations.
	/// </summary>
	[Fact]
	public async Task ReflectionInLocalFunction_ReportsDISP003()
	{
		const string testCode = """
			using System;

			namespace TestNamespace
			{
			    public class TestClass
			    {
			        public void DoSomething()
			        {
			            Type? LocalFunc()
			            {
			                return {|#0:Type.GetType("System.String")|};
			            }

			            var result = LocalFunc();
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<AotCompatibilityAnalyzer, DefaultVerifier>
		{
			TestCode = testCode,
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.ExpectedDiagnostics.Add(
			new DiagnosticResult(DiagnosticDescriptors.ReflectionWithoutAotAnnotation)
				.WithLocation(0)
				.WithArguments("DoSomething", "GetType"));

		await test.RunAsync();
	}

	/// <summary>
	/// Verifies DISP003 is reported for multiple reflection calls in the same method.
	/// </summary>
	[Fact]
	public async Task MultipleReflectionCalls_ReportsMultipleDISP003()
	{
		const string testCode = """
			using System;

			namespace TestNamespace
			{
			    public class TestClass
			    {
			        public void DoSomething(Type type)
			        {
			            var t1 = {|#0:Type.GetType("System.String")|};
			            var obj = {|#1:Activator.CreateInstance(type)|}!;
			        }
			    }
			}
			""";

		var test = new CSharpAnalyzerTest<AotCompatibilityAnalyzer, DefaultVerifier>
		{
			TestCode = testCode,
			ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
		};

		test.ExpectedDiagnostics.Add(
			new DiagnosticResult(DiagnosticDescriptors.ReflectionWithoutAotAnnotation)
				.WithLocation(0)
				.WithArguments("DoSomething", "GetType"));

		test.ExpectedDiagnostics.Add(
			new DiagnosticResult(DiagnosticDescriptors.ReflectionWithoutAotAnnotation)
				.WithLocation(1)
				.WithArguments("DoSomething", "CreateInstance"));

		await test.RunAsync();
	}
}
