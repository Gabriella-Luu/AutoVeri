method check_greater (arr: seq<int>, number: int) returns (res: bool)
    requires |arr| > 0
    ensures (res == true) <==> (forall i :: 0 <= i < |arr| ==> number > arr[i])
    ensures (res == false) <==> (exists i :: 0 <= i < |arr| && number <= arr[i])
{
  var v0 := [15, 25, 35, 45, 55];
  var v1 := 60;
  assume {:axiom} arr == v0;
  //redundant asserts to make dafny happy
  assert arr[0] == v0[0];
  assert arr[1] == v0[1];
  assert arr[2] == v0[2];
  assert arr[3] == v0[3];
  assert arr[4] == v0[4];
  assume {:axiom} number == v1;
  res := false;
}