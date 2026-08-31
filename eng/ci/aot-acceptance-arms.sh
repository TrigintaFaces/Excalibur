#!/usr/bin/env bash
# Both acceptance arms for the ahead-of-time annotation placement.
#
# A green on the safety arm alone proves nothing: it is equally consistent with the annotations
# sitting on the wrong members, where they warn nobody. The two arms together pin the placement.
#
#   safety    a source-generated composition must build with ZERO IL2026/IL3050 and no
#             consumer-side suppression. If it warns, the framework is crying wolf at the one
#             consumer who did everything right.
#   liveness  an assembly-scanning composition MUST warn with BOTH IL2026 and IL3050. If it does
#             not, the annotation is missing or is somewhere the consumer's compiler never reads.
#   failfast  where the runtime supports no dynamic code there is no default event serializer, so a
#             composition that does not register one MUST be rejected at startup, naming the call that
#             fixes it. Otherwise a build warning was traded for a later runtime failure, which is
#             worse. The probe refuses unless it can first prove it is on the no-dynamic-code branch:
#             a green from a probe that silently ran the ordinary branch would measure nothing.
#
# Exit: 0 both arms pass | 1 an arm failed | 2 REFUSE (nothing was measured; NOT a pass)
set -u

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# MSBuild reads the ProjectReference path, and it does not understand a Git-Bash /d/... prefix.
REPO_ROOT_WIN="$(cd "$REPO_ROOT" && pwd -W 2>/dev/null || echo "$REPO_ROOT")"
ARM="${1:-both}"
WORK="${TMPDIR:-/tmp}/aot-arms-$$"
SAFETY_PROJECT="$REPO_ROOT/samples/10-aot/Excalibur.Dispatch.Aot.Sample/Excalibur.Dispatch.Aot.Sample.csproj"

# The verdict lives in a function so --self-test can exercise it without a build.
# codes: 0 pass, 1 fail, 2 refuse
verdict_liveness() {
	local build_exit="$1" log="$2"
	[ -s "$log" ] || { echo "REFUSE liveness: empty build log; nothing measured"; return 2; }
	grep -q "Excalibur.Dispatch.Aot.LivenessArm" "$log" || {
		echo "REFUSE liveness: build log never names the probe project; it did not compile"; return 2; }
	local il2026 il3050
	il2026=$(grep -cE "(warning|error) IL2026" "$log")
	il3050=$(grep -cE "(warning|error) IL3050" "$log")
	if [ "$il2026" -gt 0 ] && [ "$il3050" -gt 0 ]; then
		echo "PASS liveness: assembly-scanning composition warns (IL2026 x$il2026, IL3050 x$il3050)"
		return 0
	fi
	echo "FAIL liveness: assembly-scanning composition did NOT warn (IL2026=$il2026 IL3050=$il3050, build exit $build_exit)"
	echo "                the annotation is missing, or it is not where a consumer's compiler reads it"
	return 1
}

verdict_safety() {
	local build_exit="$1" log="$2"
	[ -s "$log" ] || { echo "REFUSE safety: empty build log; nothing measured"; return 2; }
	grep -q "Aot.Sample" "$log" || { echo "REFUSE safety: build log never names the sample"; return 2; }
	if [ "$build_exit" -ne 0 ] && ! grep -qE "(warning|error) IL(2026|3050)" "$log"; then
		echo "REFUSE safety: build failed for a reason other than an IL diagnostic (exit $build_exit)"; return 2
	fi
	local sites
	sites=$(grep -oE "\.cs\([0-9]+,[0-9]+\): (warning|error) IL(2026|3050)" "$log" | sort -u | wc -l)
	if [ "$sites" -eq 0 ]; then
		echo "PASS safety: source-generated composition builds with zero IL2026/IL3050"
		return 0
	fi
	echo "FAIL safety: source-generated composition reports $sites IL2026/IL3050 site(s)"
	grep -oE "Program\.cs\([0-9]+,[0-9]+\): (warning|error) IL[0-9]+: Using member '[^']+'" "$log" | sort -u | sed 's/^/                /'
	return 1
}

verdict_failfast() {
	local run_exit="$1" log="$2"
	[ -s "$log" ] || { echo "REFUSE failfast: empty probe output; nothing measured"; return 2; }
	grep -q "PROBE-AOT-BRANCH-ACTIVE" "$log" || {
		echo "REFUSE failfast: the probe never reached the no-dynamic-code branch, so it measured nothing"
		echo "                 (it did not build, or the runtime feature switch did not take)"; return 2; }
	if grep -q "PROBE-REJECTED-NAMING-REMEDY" "$log" && grep -q "PROBE-ACCEPTED-WITH-REMEDY" "$log" 		&& [ "$run_exit" -eq 0 ]; then
		echo "PASS failfast: a composition missing the event serializer is rejected at startup naming the remedy,"
		echo "               and the same composition with it is accepted"
		return 0
	fi
	echo "FAIL failfast: (probe exit $run_exit)"
	grep -E "^PROBE-" "$log" | sed 's/^/                /'
	return 1
}

if [ "$ARM" = "--self-test" ]; then
	fails=0
	t() { # name expected_code fn log_content build_exit
		local name="$1" want="$2" fn="$3" body="$4" ex="$5" f="$WORK/selftest.log" got
		mkdir -p "$WORK"; printf '%s' "$body" > "$f"
		"$fn" "$ex" "$f" >/dev/null 2>&1; got=$?
		if [ "$got" != "$want" ]; then echo "  self-test FAILED: $name (want $want got $got)"; fails=1
		else echo "  ok: $name -> $want"; fi
	}
	echo "self-test: the gate must be able to report each of PASS / FAIL / REFUSE"
	t "liveness: warns on both codes -> PASS" 0 verdict_liveness \
		'Excalibur.Dispatch.Aot.LivenessArm
P.cs(1,1): warning IL2026: x
P.cs(1,1): warning IL3050: x
' 0
	t "liveness: silent -> FAIL" 1 verdict_liveness 'Excalibur.Dispatch.Aot.LivenessArm
built ok
' 0
	t "liveness: only IL2026 -> FAIL" 1 verdict_liveness 'Excalibur.Dispatch.Aot.LivenessArm
P.cs(1,1): warning IL2026: x
' 0
	t "liveness: probe never compiled -> REFUSE" 2 verdict_liveness 'some other project
' 0
	t "liveness: empty log -> REFUSE" 2 verdict_liveness '' 0
	t "safety: clean -> PASS" 0 verdict_safety 'Aot.Sample built
' 0
	t "safety: one warning site -> FAIL" 1 verdict_safety 'Aot.Sample
Program.cs(61,1): warning IL3050: Using member '"'"'X'"'"'
' 0
	t "safety: unrelated build break -> REFUSE" 2 verdict_safety 'Aot.Sample
Program.cs(9,9): error CS0103: nope
' 1
	t "failfast: both directions -> PASS" 0 verdict_failfast 'PROBE-AOT-BRANCH-ACTIVE
PROBE-REJECTED-NAMING-REMEDY
PROBE-ACCEPTED-WITH-REMEDY
' 0
	t "failfast: missing composition accepted -> FAIL" 1 verdict_failfast 'PROBE-AOT-BRANCH-ACTIVE
PROBE-FAIL: composition without the serializer was accepted
' 1
	t "failfast: rejected but remedy composition also rejected -> FAIL" 1 verdict_failfast 'PROBE-AOT-BRANCH-ACTIVE
PROBE-REJECTED-NAMING-REMEDY
' 1
	t "failfast: switch did not take -> REFUSE" 2 verdict_failfast 'PROBE-REFUSE: dynamic code is still supported
' 2
	t "failfast: empty output -> REFUSE" 2 verdict_failfast '' 0
	[ "$fails" -eq 0 ] && echo "self-test: all arms of the gate are reachable" || echo "self-test: BROKEN"
	rm -rf "$WORK"
	exit "$fails"
fi

# Keep the worst verdict any arm reported. Written as a function because the obvious inline form
# -- `verdict_x ... || { [ "$rc" -eq 0 ] && rc=$?; }` -- captures the TEST's exit, not the verdict's,
# so a REFUSE (2) was reported to the caller as 0. A gate that cannot report REFUSE reports a pass
# it did not earn.
record_verdict() {
	local v="$1"
	[ "$v" -gt "$rc" ] && rc="$v"
	return 0
}

mkdir -p "$WORK" || { echo "REFUSE: cannot create $WORK"; exit 2; }
trap 'rm -rf "$WORK"' EXIT
rc=0

if [ "$ARM" = "both" ] || [ "$ARM" = "safety" ]; then
	echo "== safety arm: source-generated composition must not warn =="
	dotnet build "$SAFETY_PROJECT" -c Release --no-incremental \
		-p:EnableAotAnalyzer=true -p:EnableTrimAnalyzer=true -v:m > "$WORK/safety.log" 2>&1
	verdict_safety "$?" "$WORK/safety.log"; record_verdict $?
fi

if [ "$ARM" = "both" ] || [ "$ARM" = "liveness" ]; then
	echo "== liveness arm: assembly-scanning composition must warn =="
	PROBE="$WORK/probe"
	mkdir -p "$PROBE"
	cat > "$PROBE/Excalibur.Dispatch.Aot.LivenessArm.csproj" <<PROJ
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsAotCompatible>true</IsAotCompatible>
    <EnableAotAnalyzer>true</EnableAotAnalyzer>
    <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$REPO_ROOT_WIN/src/Dispatch/Excalibur.Dispatch/Excalibur.Dispatch.csproj" />
  </ItemGroup>
</Project>
PROJ
	cat > "$PROBE/Program.cs" <<'PROG'
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// The reflective composition. A consumer writing this line is asking the framework to find
// handlers by scanning, which trimming and native compilation cannot follow. Their compiler
// must say so -- that is the whole point of the annotation.
services.AddDispatch(Assembly.GetExecutingAssembly());
PROG
	dotnet build "$PROBE/Excalibur.Dispatch.Aot.LivenessArm.csproj" -c Release --no-incremental -v:m \
		> "$WORK/liveness.log" 2>&1
	verdict_liveness "$?" "$WORK/liveness.log"; record_verdict $?
fi

if [ "$ARM" = "both" ] || [ "$ARM" = "failfast" ]; then
	echo "== failfast arm: a no-dynamic-code composition without an event serializer must be rejected =="
	FF="$WORK/failfast"
	mkdir -p "$FF"
	cat > "$FF/Excalibur.Dispatch.Aot.FailFastArm.csproj" <<PROJ
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <NoWarn>\$(NoWarn);IL2026;IL3050</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <!-- Run the no-dynamic-code branch under an ordinary runtime. This is the documented feature
         switch a Native AOT publish sets; setting it here exercises the same branch without a
         publish, and the probe refuses if it finds the switch did not take. -->
    <RuntimeHostConfigurationOption
      Include="System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported"
      Value="false" Trim="true" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="$REPO_ROOT_WIN/src/Dispatch/Excalibur.Dispatch/Excalibur.Dispatch.csproj" />
  </ItemGroup>
</Project>
PROJ
	cat > "$FF/Program.cs" <<'PROG'
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

// Positive control. Everything below is a claim about the no-dynamic-code branch, and an ordinary
// run takes the other one. If the switch did not take, this probe has measured nothing and says so
// rather than reporting a green it did not earn.
if (RuntimeFeature.IsDynamicCodeSupported)
{
	Console.WriteLine("PROBE-REFUSE: dynamic code is still supported; the feature switch did not take");
	return 2;
}

Console.WriteLine("PROBE-AOT-BRANCH-ACTIVE");

var missing = new ServiceCollection();
missing.AddLogging();
missing.AddDispatchPipeline();
using (var sp = missing.BuildServiceProvider())
{
	try
	{
		sp.GetRequiredService<IStartupValidator>().Validate();
		Console.WriteLine("PROBE-FAIL: a composition with no event serializer was accepted at startup");
		return 1;
	}
	catch (OptionsValidationException ex) when (ex.Message.Contains("AddAotEventSerializer", StringComparison.Ordinal))
	{
		Console.WriteLine("PROBE-REJECTED-NAMING-REMEDY");
	}
}

var supplied = new ServiceCollection();
supplied.AddLogging();
supplied.AddAotEventSerializer(ProbeContext.Default);
supplied.AddDispatchPipeline();
using (var sp = supplied.BuildServiceProvider())
{
	sp.GetRequiredService<IStartupValidator>().Validate();
	_ = sp.GetRequiredService<IEventSerializer>();
	Console.WriteLine("PROBE-ACCEPTED-WITH-REMEDY");
}

return 0;

[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	UseStringEnumConverter = true,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(string))]
internal sealed partial class ProbeContext : JsonSerializerContext;
PROG
	dotnet run --project "$FF/Excalibur.Dispatch.Aot.FailFastArm.csproj" -c Release \
		> "$WORK/failfast.log" 2>&1
	verdict_failfast "$?" "$WORK/failfast.log"; record_verdict $?
fi

exit "$rc"
