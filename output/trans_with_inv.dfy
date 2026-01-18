import opened Std.Collections.Seq
import opened Std.Strings
import opened Std.Math
import opened Std.Arithmetic.Power
method remove_Occ(s: array<char>, ch: char) returns (res: array<char>) 
{
    var i: int;
    var arr: array<char> := s;
    for i := 0 to arr.Length
    invariant forall k :: 0 <= k < i ==> arr[k] != ch
    { 
        if (arr[i] == ch) 
        { 
            var temp: seq<char> := arr[0 .. i] + arr[i + 1 .. arr.Length]; 
            arr := array(temp);
            break;
        }
    }
    for i := arr.Length - 1 downto 0
    invariant forall k :: i < k < arr.Length ==> arr[k] != ch
    { 
        if (arr[i] == ch) 
        { 
            var temp: seq<char> := arr[0 .. i] + arr[i + 1 .. arr.Length]; 
            arr := array(temp);
            break;
        }
    }
    res := arr;
}

