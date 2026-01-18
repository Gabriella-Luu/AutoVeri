method check_greater(arr: seq<int>, number: int) returns (res: bool)
    requires |arr| > 0
    ensures (res == true) <==> (forall i :: 0 <= i < |arr| ==> number > arr[i])
    ensures (res == false) <==> (exists i :: 0 <= i < |arr| && number <= arr[i])
{
}