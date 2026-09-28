------------------------------- MODULE _SelfTest -------------------------------
(***************************************************************************)
(* NON-VACUITY CONTROL FOR THE MODEL CHECKER ITSELF.                       *)
(*                                                                          *)
(* This module exists to be VIOLATED. It is not a model of anything in the  *)
(* product. Its only job is to prove that the TLC invocation we use can     *)
(* actually REPORT a violation -- because a checker nobody has seen reject  *)
(* anything is indistinguishable from one that cannot, and every R4 claim   *)
(* we make afterwards rests on the checker being able to say no.            *)
(*                                                                          *)
(* EXPECTED RESULT: TLC reports "Invariant NeverThree is violated" and      *)
(* prints a 4-state trace. A PASS here is a FAILURE of the control.         *)
(***************************************************************************)
EXTENDS Naturals

VARIABLE x

Init == x = 0
Next == x' = x + 1
Spec == Init /\ [][Next]_x

\* Deliberately false: Next reaches 3 in three steps.
NeverThree == x # 3

SmallX == x =< 10
================================================================================
