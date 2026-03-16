method tuple_modulo(test_tup1:seq<int>, test_tup2:seq<int>) returns (res:seq<int>) 
//  requires |test_tup1| == |test_tup2|
{
  res := [];
  for i := 0 to |test_tup1| 
    invariant |res| == i
    invariant forall k :: 0 <= k < i ==> res[k] == test_tup1[k] / test_tup2[k]
  {
    res := res + [test_tup1[i] / test_tup2[i]];
  }
}