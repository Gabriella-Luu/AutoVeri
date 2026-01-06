method{:test} check(){
  var call0:= prime_num(2) ;
  var call1:= prime_num(3) ;
  var call2:= prime_num(4) ;
  var call3:= prime_num(5) ;
  var call4:= prime_num(6) ;
  var call5:= prime_num(7) ;
  var call6:= prime_num(8) ;
  var call7:= prime_num(9) ;
  var call8:= prime_num(10) ;
  var call9:= prime_num(11) ;
  expect call0==None;
  expect call1==None;
  expect call2==None;
  expect call3==None;
  expect call4==false;
  expect call5==true;
  expect call6==false;
  expect call7==true;
  expect call8==false;
  expect call9==true;
}