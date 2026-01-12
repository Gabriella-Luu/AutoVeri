import opened Std.Collections.Seq
import opened Std.Strings
import opened Std.Math
import opened Std.Arithmetic.Power
method prime_num(num: int) returns (res: bool)
{
    res := true;
    if num >= 1
    {
        for i in 2..num/2
        {
            if (num % i) == 0
            {
                res := false;
                return;
            }
        }
    }
    else
    {
        res := false;
    }
}

method{:test} check(){
var call0:= prime_num(-5) ;
var call1:= prime_num(0) ;
var call2:= prime_num(1) ;
var call3:= prime_num(2) ;
var call4:= prime_num(3) ;
var call5:= prime_num(4) ;
var call6:= prime_num(10) ;
var call7:= prime_num(11) ;
var call8:= prime_num(20) ;
var call9:= prime_num(21) ;
expect call0==false;
expect call1==false;
expect call2==None;
expect call3==None;
expect call4==None;
expect call5==None;
expect call6==false;
expect call7==false;
expect call8==false;
expect call9==false;
}


method{:test} check(){
var call0:= prime_num(-5) ;
var call1:= prime_num(0) ;
var call2:= prime_num(1) ;
var call3:= prime_num(2) ;
var call4:= prime_num(3) ;
var call5:= prime_num(4) ;
var call6:= prime_num(10) ;
var call7:= prime_num(11) ;
var call8:= prime_num(20) ;
var call9:= prime_num(21) ;
expect call0==false;
expect call1==false;
expect call2==None;
expect call3==None;
expect call4==None;
expect call5==None;
expect call6==false;
expect call7==false;
expect call8==false;
expect call9==false;
}


method{:test} check(){
var call0:= prime_num(-5) ;
var call1:= prime_num(0) ;
var call2:= prime_num(1) ;
var call3:= prime_num(2) ;
var call4:= prime_num(3) ;
var call5:= prime_num(4) ;
var call6:= prime_num(10) ;
var call7:= prime_num(11) ;
var call8:= prime_num(20) ;
var call9:= prime_num(21) ;
expect call0==false;
expect call1==false;
expect call2==None;
expect call3==None;
expect call4==None;
expect call5==None;
expect call6==false;
expect call7==false;
expect call8==false;
expect call9==false;
}


method{:test} check(){
var call0:= prime_num(-5) ;
var call1:= prime_num(0) ;
var call2:= prime_num(1) ;
var call3:= prime_num(2) ;
var call4:= prime_num(3) ;
var call5:= prime_num(4) ;
var call6:= prime_num(10) ;
var call7:= prime_num(11) ;
var call8:= prime_num(20) ;
var call9:= prime_num(21) ;
expect call0==false;
expect call1==false;
expect call2==None;
expect call3==None;
expect call4==None;
expect call5==None;
expect call6==false;
expect call7==false;
expect call8==false;
expect call9==false;
}


method{:test} check(){
var call0:= prime_num(-5) ;
var call1:= prime_num(0) ;
var call2:= prime_num(1) ;
var call3:= prime_num(2) ;
var call4:= prime_num(3) ;
var call5:= prime_num(4) ;
var call6:= prime_num(10) ;
var call7:= prime_num(11) ;
var call8:= prime_num(20) ;
var call9:= prime_num(21) ;
expect call0==false;
expect call1==false;
expect call2==None;
expect call3==None;
expect call4==None;
expect call5==None;
expect call6==false;
expect call7==false;
expect call8==false;
expect call9==false;
}


method{:test} check(){
var call0:= prime_num(-5) ;
var call1:= prime_num(0) ;
var call2:= prime_num(1) ;
var call3:= prime_num(2) ;
var call4:= prime_num(3) ;
var call5:= prime_num(4) ;
var call6:= prime_num(10) ;
var call7:= prime_num(11) ;
var call8:= prime_num(20) ;
var call9:= prime_num(21) ;
expect call0==false;
expect call1==false;
expect call2==None;
expect call3==None;
expect call4==None;
expect call5==None;
expect call6==false;
expect call7==false;
expect call8==false;
expect call9==false;
}


method{:test} check(){
var call0:= prime_num(-5) ;
var call1:= prime_num(0) ;
var call2:= prime_num(1) ;
var call3:= prime_num(2) ;
var call4:= prime_num(3) ;
var call5:= prime_num(4) ;
var call6:= prime_num(10) ;
var call7:= prime_num(11) ;
var call8:= prime_num(20) ;
var call9:= prime_num(21) ;
expect call0==false;
expect call1==false;
expect call2==None;
expect call3==None;
expect call4==None;
expect call5==None;
expect call6==false;
expect call7==false;
expect call8==false;
expect call9==false;
}




































