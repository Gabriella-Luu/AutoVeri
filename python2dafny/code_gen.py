from openai import OpenAI
import os
import shutil
from pathlib import Path
import json
import configparser
import re

def get_config():
    script_dir_path = "/Users/luyihan/Desktop/AutoVeri/"
    config_path = os.path.join(script_dir_path, 'env.config')
    if not (os.path.exists(config_path)):
        print("env.config not found!!")
        return
    config = configparser.ConfigParser()
    config.read(config_path)

    api_config = dict()
    api_config["openai_api_key"] = config.get('DEFAULT', 'openai_api_key')
    api_config["openai_base_url"] = config.get('DEFAULT', 'openai_base_url')
    api_config["deepseek_api_key"] = config.get('DEFAULT', 'deepseek_api_key')
    api_config["deepseek_base_url"] = config.get('DEFAULT', 'deepseek_base_url')
    api_config["model"] = config.get('TRANS', 'model')
    api_config["temp"] = float(config.get('TRANS', 'temp'))

    env_config = dict()
    env_config["input_json_path"] = config.get('DEFAULT', 'input_json_path')
    env_config["max_concurrent_threads"] = config.get('TRANS', 'max_concurrent_threads')
    env_config["translation_path"] = config.get('TRANS', 'translation_path')

    return api_config, env_config


examples = [
    # abs, real, seq, |s|
    {
        "Python": "\\n".join(
            [
                "def abs_sum(s):",
                "    sum = 0",
                "    for x in s:",
                "        sum += abs(x)",
            ]
        ),
        "Dafny": "\\n".join(
            [
                "function abs(x:real):real { if x>=0.0 then x else -x }",
                "// give implementations of used functions if they don't exist in Dafny",
                "method abs_sum(s:seq<real>) returns (res:real) { res:=0.0; for i := 0 to |s| { res:=res+abs(s[i]); } } ",
                "// Always use seq in Dafny; don't use array unless necessary",
            ]
        ),
    },
    # Math.Min/Max, example of list comprehension expression
    {
        "Python": "\\n".join(
            [
                "def select(s, lower, upper):",
                "    return [min(upper,max(x,lower)) for x in s]",
            ]
        ),
        "Dafny": "\\n".join(
            [
                "method select(s:seq<int>,lower:int,upper:int) returns (res:seq<int>) { res:=s; for i := 0 to |s| { res:=res[i:= Math.Min(upper,Math.Max(s[i],lower))]; } }",
                "// The min/max functions between two numbers are defined in the Math library; use them directly instead of implementing a new one",
                "// Use loops to handle list comprehension expressions; don't use the function seq(n, i => f(i))",
            ]
        ),
    },
    # Method calls
    {
        "Python": "\\n".join(
            [
                "def abs_sum(s):",
                "    sum = 0",
                "    for x in s:",
                "        sum += abs(x)",
                "def test():",
                "    return abs_sum([1,-1,1])==3",
            ]
        ),
        "Dafny": "\\n".join(
            [
                "function abs(x:real):real { if x>=0.0 then x else -x }",
                "method abs_sum(s:seq<real>) returns (res:real) { res:=0.0; for i := 0 to |s| { res:=res+abs(s[i]); } } ",
                "function test():bool { var call_abs_sum := abs_sum([1,-1,1]); call_abs_sum==3}",
                "// In Dafny, method calls are not allowed in expressions; assign the value to a temporary variable instead",
            ]
        ),
    },
    # Seq.Min/Max, sum
    {
        "Python": "\\n".join(
            [
                "def MinMaxSum(s):",
                "    return min(s), max(s), sum(s)",
            ]
        ),
        "Dafny": "\\n".join(
            [
                "function sum(s:seq<int>):int { if |s|==0 then 0 else s[0]+sum(s[1..]) }",
                "// The sum function is not pre-defined in Dafny, give an inplementation before using it",
                "// In Dafny functions, loops( {for} and {while} ) are not allowed; use recursive function instead",
                "method MinMaxSum(s:seq<int>) returns (min_s:int, max_s:int, sum_s:int) { min_s:=Seq.Min(s); max_s:=Seq.Max(s); sum_s:=sum(s);}",
                "// The min/max functions in a sequence are defined in the Seq library; use them directly instead of implementing a new one",
            ]
        ),
    },
    # Pow
    {
        "Python": "\\n".join(
            [
                "def calc(a:int,b:int):",
                "    return a**b if a>0 else a*b",
            ]
        ),
        "Dafny": "\\n".join(
            [
                "function calc(a:int,b:int):int { if a>0 then Pow(a,b) else a*b } ",
                "// The exponent functions is defined as Pow(a,b) in Dafny; don't use a**b or a^b",
            ]
        ),
    },
    # sqrt
    {
        "Python": "\\n".join(
            [
                "def calc(n):",
                "    s = 0",
                "    for i in range(int(sqrt(n))+1):",
                "        s += i",
                "    return s",
            ]
        ),
        "Dafny": "\\n".join(
            [
                "function sqrt(x:nat):int { if x<=1 then x else if (sqrt(x-1)+1)*(sqrt(x-1)+1)==x then sqrt(x-1)+1 else sqrt(x-1) }",
                "// The sqrt function is not pre-defined in Dafny, give an inplementation before using it; Don't use Math.Sqrt which doesn't exist in Dafny",
                "method calc(n:int) returns (s:int) { s:=0; for i := 0 to sqrt(n)+1 { s:=s+i; } } ",
            ]
        ),
    },
    # floor and as real
    {
        "Python": "\\n".join(
            [
                "def floating_division(a:float,b:float)->float:",
                "    return floor(a/b)",
            ]
        ),
        "Dafny": "\\n".join(
            [
                "function floating_division(a:real,b:real):real {(a/b).Floor as real}",
                "// In Dafny, the floor function is a member of real, Not a function in the library; Don't use floor(s) or Math.Floor(s)",
                "// The type of s.Floor is {int}; if type convertion to real is needed, use {as real}",
            ]
        ),
    },
    # division (type must match)
    {
        "Python": "\\n".join(
            [
                "def division(a:float, b:int, c:int, d:float):",
                "    return a / b + c / d",
            ]
        ),
        "Dafny": "\\n".join(
            [
                "function division(a:real, b:int, c:int, d:real):real {a / (b as real) + (c as real) / d}",
                "// In Dafny, the types of operands must match",
            ]
        ),
    },
    # logics
    {
        "Python": "\\n".join(
            [
                "def logical(a, b, c, d):",
                "    return a and b or c and d",
            ]
        ),
        "Dafny": "\\n".join(
            [
                "function logical(a:bool, b:bool, c:bool, d:bool):bool { (a && b) || (c && d) }",
                "// In Dafny, use () to disambiguate the use of && and ||",
            ]
        ),
    },
    # set definition and conversions
    {
        "Python": "\\n".join(
            [
                "def about_set(a:int, arr:list):",
                "    s1 = set(a)",
                "    s2 = set(arr)",
                "    return len(list(s1+s2))",
            ]
        ),
        "Dafny": "\\n".join(
            [
                "function SetToSeq(s:set<int>):seq<int> { if |s|==0 then [] else var x:| x in s && forall y :: y in s ==> x <= y; [x] + SetToSeq(s-{x}) }",
                "// The type conversion function from set to seq is not pre-defined in Dafny, give an inplementation before using it",
                "function about_set(a:int, arr:seq<int>):int { var s1 := {a}; var s2 := set x | x in arr; |SetToSeq(s1+s2)| }",
            ]
        ),
    },
    # map definition
    {
        "Python": "\\n".join(
            [
                "def about_map(a:int,b:int):",
                "    s = {1:2,3:4}",
                "    s[a] = b",
                "    return s[a]",
            ]
        ),
        "Dafny": "\\n".join(
            [
                "function about_map(a:int,b:int):int {var s := map[1:=2,3:=4]; s:=s[a:=b]; s[a]}",
            ]
        ),
    },
    # range
    {
        "Python": "\\n".join(
            [
                "def sum2(n):",
                "    s = 0",
                "    for i in range(n + 1):",
                "        s += i",
                "    for i in range(0, n + 1):",
                "        s += i",
                "    return s",
            ]
        ),
        "Dafny": "\\n".join(
            [
                "method sum2(n:int) returns (s:int) { s:=0; for i := 0 to n+1 { s:=s+i; } for i := 0 to n+1 { s:=s+i; } }",
            ]
        ),
    },
    # string iteration
    {
        "Python": "\\n".join(
            [
                "def cnt(Str):",
                "    s = 0",
                "    for chr in Str:",
                '        if chr==","',
                "           s += 1",
                "    return s",
            ]
        ),
        "Dafny": "\\n".join(
            [
                "method cnt(Str:string) returns (s:int) { s:=0; for i := 0 to |Str| { if Str[i]==',' { s:=s+1; } } }",
            ]
        ),
    },
    # string list iteration
    {
        "Python": "\\n".join(
            [
                "def cnt(strings):",
                "    s = 0",
                "    for Str in strings:",
                "        s+=len(Str)",
                "    return s",
            ]
        ),
        "Dafny": "\\n".join(
            [
                "method cnt(strings:seq<string>) returns (s:int) { s:=0; for i := 0 to |strings| { s:=s+|strings[i]|; } }",
            ]
        ),
    },
    # split/reverse/join
    {
        "Python": "\\n".join(
            [
                "def String_Operations(Str):",
                '    lst = Str.split(",")',
                "    lst = [reverse(substr) for substr in lst]",
                '    res = ",".join(lst)',
                "    return res",
            ]
        ),
        "Dafny": "\\n".join(
            [
                "method String_Operations(Str:string) returns (res:string) { var lst := Split(Str,',');  for i := 0 to |lst| { lst:= lst[i:=Reverse(lst[i])]; } res := Join(lst,\",\"); }",
                "// The Split/Reverse/Join functions on {string} are defined in Dafny already; use them directly instead of implementing a new one",
                "// In Dafny, type char is wrapped in '', while type string is wrapped in \"\"",
            ]
        ),
    },
    # Startswith
    {
        "Python": "\\n".join(
            [
                "def Choose_String(str1,str2):",
                "    return str1 if str1.startswith(str2) else str2",
            ]
        ),
        "Dafny": "\\n".join(
            [
                "function startswith(str1:string,str2:string):bool { str2<=str1 }",
                "// The startswith function is not pre-defined in Dafny, give an inplementation before using it",
                "function Choose_String(str1:string,str2:string):string { if startswith(str1,str2) then str1 else str2 }",
            ]
        ),
    },
    # Contains (substr in str)
    {
        "Python": "\\n".join(
            [
                "def can_find(str1,str2):",
                "    return str1 if str1 in str2 else str2",
            ]
        ),
        "Dafny": "\\n".join(
            [
                "function contains(substr:string,str:string):bool { if forall i:int :: 0<=i<=|str|-|substr| ==> str[i..i+|substr|]!=substr then false else true }",
                "// The contains function is not pre-defined in Dafny, give an inplementation before using it",
                "function can_find(str1:string,str2:string):string { if contains(str1,str2) then str1 else str2 }",
            ]
        ),
    },
    # ToLower/ToUpper
    {
        "Python": "\\n".join(
            [
                "def String_Case_Operations(Str):",
                "    lower = Str.lower()",
                "    upper = lower.upper()",
                "    return upper",
            ]
        ),
        "Dafny": "\\n".join(
            [
                "function ToLower(str:string):string { if |str|==0 then \"\" else [if 'A'<=str[0]<='Z' then str[0]-'A'+'a' else str[0]]+ToLower(str[1..]) }",
                "function ToUpper(str:string):string { if |str|==0 then \"\" else [if 'a'<=str[0]<='z' then str[0]-'a'+'A' else str[0]]+ToUpper(str[1..]) }",
                "// The ToLower/ToUpper functions are not pre-defined in Dafny, give inplementations before using them",
                "method String_Case_Operations(Str:string) returns (res:string) { var lower := ToLower(Str); var upper := ToUpper(lower); res:=upper; }",
            ]
        ),
    },
    # ToString/ToInt
    {
        "Python": "\\n".join(
            [
                "def String_Conversions(x):",
                "    Str = str(x)",
                "    y = int(Str)",
                "    return y",
            ]
        ),
        "Dafny": "\\n".join(
            [
                "function String_Conversions(x:int):int { var Str:=OfInt(x); var y:=ToInt(Str); y }",
                "// The type convertion functions between string and int are defined in Dafny already; use them directly, don't implement a new one",
            ]
        ),
    },
    # Sort
    {
        "Python": "\\n".join(
            [
                "def Sort_by_Type(list_int:List[int],list_float:List[float],list_str:List[str]):",
                "    return sorted(list_int),sorted(list_float),sorted(list_str)",
            ]
        ),
        "Dafny": "\\n".join(
            [
                "function LessThanOrEq_String(a:string,b:string):bool { if |a|==0 then true else if |b|==0 then false else if a[0]==b[0] then LessThanOrEq_String(a[1..],b[1..]) else a[0]<b[0] }",
                "// There is no dictionary comparison of strings in dafny, implement it before using sorting functions",
                "method Sort_by_Type(list_int:seq<int>,list_float:seq<real>,list_str:seq<string>) returns (sorted_list_int:seq<int>,sorted_list_float:seq<real>,sorted_list_str:seq<string>) { sorted_list_int := MergeSortBy((a,b)=>(a<=b),list_int); sorted_list_float := MergeSortBy((a,b)=>(a<=b),list_float); sorted_list_str := MergeSortBy(LessThanOrEq_String,list_str); }",
            ]
        ),
    },
]


def template(code):
    return "\\n".join(
        [
            "You are an expert Dafny programmer.",
            "You are good at translating Python into Dafny.",
            "",
            "Please Translate the following Python function into Dafny.",
            "Given Python function:",
            "```python",
            code,
            "```",
            "",
            "You MUST return the translation in the following format:",
            "```dafny",
            "// Dafny code",
            "```",
            "Here are some examples of translating Python into Dafny:",
        ]
        + [
            "\\n".join(
                [
                    "Python example %d:" % (i),
                    "```python",
                    examples[i]["Python"],
                    "```",
                    "Dafny translation %d:" % (i),
                    "```dafny",
                    examples[i]["Dafny"],
                    "```",
                ]
            )
            for i in range(len(examples))
        ]
    )

def solve(SourceCode, model, key, base_url, temp, trans_path):
    client = OpenAI(api_key=key, base_url=base_url)
    prompt = template(SourceCode)
    response = client.beta.chat.completions.parse(
        model=model,
        messages=[{"role": "user", "content": prompt}],
        temperature=temp,
    )
    match = re.search(
        r"```dafny\n(?P<code>.+?)\n```",
        response.choices[0].message.content,
        re.DOTALL,
    )
    if match:
        code = match.group("code")

    code = code.split("\n")
    code = [
        (
            line
            if len(line.split())
            and line.split()[0]
            not in {"requires", "ensures", "invariant", "decreases", "reads"}
            else ""
        )
        for line in code
    ]
    code = "\n".join(code)
    DafnyFilePath = Path(trans_path).joinpath("trans.dfy")
    DafnyFile = open(file=DafnyFilePath, mode="w", encoding="utf-8")
    print(
        (
            "\n".join(
                [
                    "import opened Std.Collections.Seq",
                    "import opened Std.Strings",
                    "import opened Std.Math",
                    "import opened Std.Arithmetic.Power",
                    code,
                ]
            )
        ),
        file=DafnyFile,
    )
    DafnyFile.close()


def execute(_api_config, _env_config):
    with open(_env_config['input_json_path'], "r", encoding="utf-8") as CODE_JSON:
        problems = json.load(CODE_JSON)

    if os.path.exists(_env_config["translation_path"]):
        shutil.rmtree(_env_config["translation_path"])
    os.makedirs(_env_config["translation_path"], exist_ok=True)
    model = _api_config['model']
    if ("gpt" in model): 
        solve(problems["python_code"], model, _api_config["openai_api_key"], _api_config["openai_base_url"], _api_config["temp"], _env_config['translation_path'])
    if ("deepseek" in model): 
        solve(problems["python_code"], model, _api_config["deepseek_api_key"], _api_config["deepseek_base_url"], _api_config["temp"], _env_config['translation_path'])

def main():
    api_config, env_config = get_config()
    execute(api_config, env_config)
    print("Done")

if __name__ == '__main__':
    main()