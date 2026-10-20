#!/usr/bin/env python3
"""Black-box controls for the census; syntax-only fixtures do not claim C# compilation."""
import pathlib
import subprocess
import sys
import tempfile
import unittest


class CensusTests(unittest.TestCase):
    def setUp(self):
        self.repo = pathlib.Path(__file__).resolve().parents[2]
        self.directory = tempfile.TemporaryDirectory(prefix="conformance-census-")
        self.root = pathlib.Path(self.directory.name).resolve()
        (self.root / "src").mkdir()
        (self.root / "tests").mkdir()
        (self.root / "kits").mkdir()
        (self.root / "src/store.cs").write_text(
            "public interface IWidgetStore {} public interface ICap {} "
            "public class SqlStore : IWidgetStore, ICap {}", encoding="utf-8")
        (self.root / "tests/SqlSuite.cs").write_text(
            "public class SqlSuite : WidgetConformanceTestKit { object Store() => new SqlStore(); }", encoding="utf-8")

    def tearDown(self):
        # The resolved cleanup target is exactly the temporary directory created for this test.
        self.assertEqual(self.root, pathlib.Path(self.directory.name).resolve())
        self.assertTrue(self.root.name.startswith("conformance-census-"))
        self.directory.cleanup()

    def census(self, body, census_path=None):
        (self.root / "kits/WidgetConformanceTestKit.cs").write_text(
            "public abstract class WidgetConformanceTestKit { "
            "protected abstract Task<IWidgetStore> CreateStoreAsync(); "
            "void Test() { " + body + " } }", encoding="utf-8")
        result = subprocess.run(
            [sys.executable, str(census_path or self.repo / "eng/ci/conformance-arm-skip-census.py"), str(self.root), "kits"],
            cwd=self.repo, capture_output=True, text=True, timeout=60)
        self.assertEqual(result.returncode, 0, result.stderr)
        return result.stdout.splitlines()

    def test_named_null_sites_remain_visible(self):
        records = self.census(" ".join(
            'SkipArm(nameof(Arm%s), capability: null, reason: "unavailable");' % n for n in range(3)))
        self.assertIn("COUNTS\t1\t3\t1", records)
        self.assertEqual(sum(r.startswith("UNNAMED\t") for r in records), 3)

    def test_named_reordered_arguments_and_comments_bind(self):
        records = self.census('SkipArm(reason: "comma, escaped \\\"quote\\\"", /* comment */ capability: typeof(ICap), arm: nameof(Arm));')
        self.assertIn("BINDS\tSqlSuite\tArm\tICap\tSqlStore", records)
        self.assertIn("COUNTS\t1\t1\t1", records)

    def test_comments_and_literal_text_do_not_invent_calls(self):
        records = self.census('/* SkipArm(nameof(Fake), typeof(ICap), "fake"); */ '
                              'var literal = "SkipArm(nameof(Fake), typeof(ICap), reason)"; '
                              'SkipArm(nameof(Arm), typeof(ICap), "real");')
        self.assertIn("COUNTS\t1\t1\t1", records)

    def test_literal_path_is_not_a_derivation(self):
        (self.root / "tests/LiteralMentionSuite.cs").write_text(
            'public class LiteralMentionSuite { const string Path = "C:/WidgetConformanceTestKit.cs"; }', encoding="utf-8")
        records = self.census('SkipArm(nameof(Arm), typeof(ICap), "real");')
        self.assertIn("COUNTS\t1\t1\t1", records)
        self.assertFalse(any("LiteralMentionSuite" in r for r in records))

    def test_partial_malformed_call_refuses_instead_of_disappearing(self):
        records = self.census('SkipArm(nameof(Arm), typeof(ICap), "real"); SkipArm(nameof(Broken), capability: );')
        self.assertTrue(any(r.startswith("UNPARSED\t") and "WidgetConformanceTestKit.cs" in r for r in records), records)

    def test_unknown_and_duplicate_labels_refuse(self):
        for args in ('arm: nameof(Arm), unknown: typeof(ICap), reason: "real"',
                     'arm: nameof(Arm), capability: typeof(ICap), capability: null'):
            with self.subTest(args=args):
                records = self.census("SkipArm(" + args + ");")
                self.assertTrue(any(r.startswith("UNPARSED\t") for r in records), records)

    def test_interpolated_text_does_not_invent_a_call(self):
        records = self.census('var literal = $"SkipArm(nameof(Fake), typeof(ICap), reason) {42}"; '
                              'SkipArm(nameof(Arm), typeof(ICap), "real");')
        self.assertIn("COUNTS\t1\t1\t1", records)

    def test_direct_qualified_primary_record_deriver_is_visible(self):
        (self.root / "tests/SqlSuite.cs").write_text(
            "public record SqlSuite() : global::Fixtures.WidgetConformanceTestKit { object Store() => new SqlStore(); }", encoding="utf-8")
        records = self.census('SkipArm(nameof(Arm), typeof(ICap), "real");')
        self.assertIn("BINDS\tSqlSuite\tArm\tICap\tSqlStore", records)

    def test_disabled_skip_candidate_is_explicitly_unparsed(self):
        records = self.census('\n#if NEVER_DEFINED\nSkipArm(nameof(Hidden), typeof(ICap), "hidden");\n#endif\n'
                              'SkipArm(nameof(Arm), typeof(ICap), "real");')
        self.assertTrue(any(r.startswith("UNPARSED\t") for r in records), records)

    def test_dynamic_arm_retains_known_capability_findings_and_unique_locations(self):
        (self.root / "src/store.cs").write_text(
            "public interface IWidgetStore {} public interface ICap {} public class SqlStore : IWidgetStore, ICap { "
            "public object GetService(Type serviceType) => null; }", encoding="utf-8")
        records = self.census('SkipArm(arm, typeof(ICap), "real"); SkipArm(otherArm, typeof(ICap), "real");')
        self.assertEqual(sum(r.startswith("LIE\t") for r in records), 2)
        provenance = [r for r in records if r.startswith("DYNAMIC_ARM\t")]
        self.assertEqual(len(set(provenance)), 2)
        self.assertIn("COUNTS\t1\t2\t1", records)

    def test_dynamic_arm_preserves_legitimate_absence_and_null_capability(self):
        (self.root / "src/store.cs").write_text(
            "public interface IWidgetStore {} public interface ICap {} public class SqlStore : IWidgetStore {}", encoding="utf-8")
        records = self.census('SkipArm(arm, typeof(ICap), "real"); SkipArm(otherArm, null, "real");')
        self.assertEqual(sum(r.startswith("TOLERABLE\t") for r in records), 1)
        self.assertEqual(sum(r.startswith("UNNAMED\t") for r in records), 1)

    def test_dynamic_arm_does_not_hide_unsupported_capability(self):
        records = self.census('SkipArm(arm, ResolveCapability(), "real");')
        self.assertTrue(any(r.startswith("UNPARSED\t") for r in records), records)

    def test_generic_and_conditional_candidates_refuse(self):
        for call in ('SkipArm<int>(nameof(Arm), typeof(ICap), "real")',
                     'other?.SkipArm(nameof(Arm), typeof(ICap), "real")'):
            with self.subTest(call=call):
                records = self.census(call + ";")
                self.assertTrue(any(r.startswith("UNPARSED\t") for r in records), records)

    def test_missing_syntax_helper_refuses(self):
        copied = self.root / "eng/ci/conformance-arm-skip-census.py"
        copied.parent.mkdir(parents=True)
        copied.write_text((self.repo / "eng/ci/conformance-arm-skip-census.py").read_text(encoding="utf-8"), encoding="utf-8")
        records = self.census('SkipArm(nameof(Arm), typeof(ICap), "real");', copied)
        self.assertTrue(any(r.startswith("UNPARSED\ttooling\t") for r in records), records)
        self.assertIn("COUNTS\t0\t0\t0", records)

    def test_composed_builder_provider_is_derived_despite_acronym_casing(self):
        # LIVENESS. A provider whose only public path is a composed builder -- no dedicated
        # Add<Provider><Contract> extension -- must still be derived, and the casing of an acronym in
        # the builder verb need not match the casing in the type it reaches. `.UseMongoDB` reaching
        # MongoDbWidgetStore is the real shape: four shipped saga providers are registered this way,
        # and requiring an exact-case match reported their oracle as underivable, which is a verdict
        # about our own naming rather than about the suite.
        (self.root / "src/mongo.cs").write_text(
            "public class MongoDbWidgetStore : IWidgetStore, ICap {}", encoding="utf-8")
        (self.root / "tests/MongoSuite.cs").write_text(
            "public class MongoSuite : WidgetConformanceTestKit { "
            "void Configure() => services.AddExcalibur(x => x.AddWidgets(w => w.UseMongoDB(m => m.Thing()))); }",
            encoding="utf-8")
        records = self.census('SkipArm(nameof(Arm), typeof(ICap), "unavailable");')
        self.assertIn("BINDS\tMongoSuite\tArm\tICap\tMongoDbWidgetStore", records)
        self.assertFalse([r for r in records if r.startswith("NOSTORE\tWidgetConformanceTestKit\tMongoSuite")], records)

    def test_composed_builder_cannot_fabricate_a_store_that_does_not_exist(self):
        # SAFETY, and it is the arm that makes the liveness arm above safe to have. The composition
        # GUESSES a type name from the builder verb, so it must only ever resolve to a type that is
        # declared in the tree and whose closure carries the contract. A verb naming nothing real must
        # leave the oracle underivable -- reported as NOSTORE -- never credited with a store.
        (self.root / "tests/GhostSuite.cs").write_text(
            "public class GhostSuite : WidgetConformanceTestKit { "
            "void Configure() => services.AddExcalibur(x => x.AddWidgets(w => w.UseGhost(g => g.Thing()))); }",
            encoding="utf-8")
        records = self.census('SkipArm(nameof(Arm), typeof(ICap), "unavailable");')
        self.assertIn("NOSTORE\tWidgetConformanceTestKit\tGhostSuite", records)
        self.assertFalse([r for r in records if "GhostWidgetStore" in r], records)


if __name__ == "__main__":
    unittest.main()
