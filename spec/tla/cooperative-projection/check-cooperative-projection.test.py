"""Verdict precedence controls; synthetic checker responses are not model evidence."""
import contextlib
import io
from pathlib import Path
import runpy
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch


class VerdictShould(unittest.TestCase):
    def run_checker(self, outcomes, arms=("healthy", "mixed-manifest-rejected")):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            java = root / "java.exe"
            jar = root / "tla.jar"
            java.touch()
            jar.touch()
            remaining = iter(outcomes)

            def run(command, **kwargs):
                if "-version" in command:
                    return subprocess.CompletedProcess(command, 0, "", "fixture")
                outcome = next(remaining)
                if outcome == "timeout":
                    raise subprocess.TimeoutExpired(command, 1)
                if outcome == "wrong_then_timeout":
                    raise subprocess.TimeoutExpired(command, 1, output=b"Invariant GateOwnership is violated")
                if outcome == "temporal_then_timeout":
                    raise subprocess.TimeoutExpired(command, 1, stderr=b"Temporal properties were violated.")
                if outcome == "expected_then_timeout":
                    raise subprocess.TimeoutExpired(command, 1, output=b"Invariant NeverThree is violated")
                if outcome == "expected_then_oom":
                    return subprocess.CompletedProcess(command, 255,
                        "Invariant NeverThree is violated\njava.lang.OutOfMemoryError", "")
                if outcome == "expected_then_error":
                    return subprocess.CompletedProcess(command, 1, "Invariant NeverThree is violated", "")
                if outcome == "expected_then_wrong":
                    return subprocess.CompletedProcess(command, 12,
                        "Invariant NeverThree is violated\nInvariant GateOwnership is violated", "")
                if outcome == "expected":
                    return subprocess.CompletedProcess(command, 12, "Invariant NeverThree is violated", "")
                if outcome == "wrong":
                    return subprocess.CompletedProcess(command, 12, "Invariant GateOwnership is violated", "")
                if outcome == "oom":
                    return subprocess.CompletedProcess(command, 1, "java.lang.OutOfMemoryError: Java heap space", "")
                if outcome == "wrong_then_oom":
                    return subprocess.CompletedProcess(command, 1,
                        "Invariant GateOwnership is violated\njava.lang.OutOfMemoryError", "")
                return subprocess.CompletedProcess(command, 0, "Model checking completed. No error has been found.", "")

            script = Path(__file__).with_name("check-cooperative-projection.py")
            args = [str(script), "--java", str(java), "--jar", str(jar), "--output", str(root / "evidence"),
                    "--only", *arms]
            with patch.object(sys, "argv", args), patch("subprocess.run", side_effect=run), contextlib.redirect_stdout(io.StringIO()):
                with self.assertRaises(SystemExit) as result:
                    runpy.run_path(str(script), run_name="__main__")
            return result.exception.code

    def test_preserve_failure_when_a_later_arm_times_out(self):
        self.assertEqual(1, self.run_checker(["wrong", "timeout"]))

    def test_refuse_when_no_wrong_answer_precedes_timeout(self):
        self.assertEqual(2, self.run_checker(["hold", "timeout"]))

    def test_continue_after_timeout_and_preserve_a_later_failure(self):
        self.assertEqual(1, self.run_checker(["timeout", "wrong"]))

    def test_pass_only_when_every_requested_answer_is_observed(self):
        self.assertEqual(0, self.run_checker(["hold", "hold"]))

    def test_heap_exhaustion_refuses(self):
        self.assertEqual(2, self.run_checker(["oom", "hold"]))

    def test_heap_exhaustion_does_not_mask_an_earlier_failure(self):
        self.assertEqual(1, self.run_checker(["wrong", "oom"]))

    def test_continue_after_heap_exhaustion_and_preserve_a_later_failure(self):
        self.assertEqual(1, self.run_checker(["oom", "wrong"]))

    def test_heap_exhaustion_does_not_mask_a_violation_in_the_same_arm(self):
        self.assertEqual(1, self.run_checker(["wrong_then_oom", "hold"]))

    def test_timeout_does_not_mask_an_invariant_violation_in_the_same_arm(self):
        self.assertEqual(1, self.run_checker(["wrong_then_timeout", "hold"]))

    def test_timeout_does_not_mask_a_temporal_violation_in_the_same_arm(self):
        self.assertEqual(1, self.run_checker(["temporal_then_timeout", "hold"]))

    def test_expected_mutant_text_cannot_hide_incomplete_execution(self):
        for outcome, code in [("expected_then_timeout", 2), ("expected_then_oom", 2),
                              ("expected_then_error", 1), ("expected_then_wrong", 1), ("expected", 0)]:
            with self.subTest(outcome=outcome):
                self.assertEqual(code, self.run_checker([outcome], ("checker-self-test",)))


if __name__ == "__main__":
    unittest.main()
