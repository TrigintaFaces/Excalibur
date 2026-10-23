--------------------- MODULE ProjectionModelSelfTest ---------------------
EXTENDS Naturals
VARIABLE x
Init == x = 0
Next == x' = IF x < 3 THEN x + 1 ELSE x
Spec == Init /\ [][Next]_x
NeverThree == x # 3
==========================================================================
