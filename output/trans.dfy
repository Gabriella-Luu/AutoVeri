import opened Std.Collections.Seq
import opened Std.Strings
import opened Std.Math
import opened Std.Arithmetic.Power
// Dafny code
method check_greater(arr: seq<int>, number: int) returns (res: bool)
{
  var sortedArr := arr; // You need to replace this with your own sorting function
  res := number > sortedArr[|sortedArr|-1];
}

