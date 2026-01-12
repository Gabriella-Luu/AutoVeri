import opened Std.Collections.Seq
import opened Std.Strings
import opened Std.Math
import opened Std.Arithmetic.Power
method prime_num(num: int) returns (result: bool)
{
  if num >= 1
  {
    var i := 2;
    while i < num / 2 + 1
    {
      if num % i == 0
      {
        result := false;
        return;
      }
      i := i + 1;
    }
    result := true;
  }
  else
  {
    result := false;
  }
}

