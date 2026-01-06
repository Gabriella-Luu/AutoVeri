from openai import OpenAI
from pathlib import Path
import json
import subprocess
import re
import copy
import configparser
import os

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
    env_config["translation_path"] = config.get('TRANS', 'translation_path')
    env_config["max_fixing_iterations"] = float(config.get('TRANS', 'max_fixing_iterations'))
    env_config["test_set_json_path"] = config.get('TRANS', 'test_set_json_path')

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
                # "import opened Std.Collections.Seq",
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
                # "import opened Std.Strings",
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
                # "import opened Std.Collections.Seq",
                "function LessThanOrEq_String(a:string,b:string):bool { if |a|==0 then true else if |b|==0 then false else if a[0]==b[0] then LessThanOrEq_String(a[1..],b[1..]) else a[0]<b[0] }",
                "// There is no dictionary comparison of strings in dafny, implement it before using sorting functions",
                "method Sort_by_Type(list_int:seq<int>,list_float:seq<real>,list_str:seq<string>) returns (sorted_list_int:seq<int>,sorted_list_float:seq<real>,sorted_list_str:seq<string>) { sorted_list_int := MergeSortBy((a,b)=>(a<=b),list_int); sorted_list_float := MergeSortBy((a,b)=>(a<=b),list_float); sorted_list_str := MergeSortBy(LessThanOrEq_String,list_str); }",
            ]
        ),
    },
]


def template(code, status, error_messages):
    match status:
        case "translate":
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
        case "syntax_error":
            return "\\n".join(
                [
                    "You are an expert Dafny programmer.",
                    "You are good at fixing syntax errors in Dafny.",
                    "Please Fix the syntax errors in the following Dafny code.",
                    "Given Dafny code:",
                    "```dafny",
                    code,
                    "```",
                    "",
                    "You MUST return the fixed Dafny code in the following format:",
                    "```dafny",
                    "// Dafny code",
                    "```",
                    "Here are some error messages from Dafny resolver:",
                ]
                + [
                    "\\n".join(
                        [
                            "Error lineno %d: %d" % (i, error_messages[i]["line"]),
                            "Error pos %d: %d" % (i, error_messages[i]["position"]),
                            "Error content %d: %s" % (i, error_messages[i]["content"]),
                            "Error type %d: %s" % (i, error_messages[i]["error_type"]),
                        ]
                    )
                    for i in range(len(error_messages))
                ]
            )
        case "timeout":
            return "\\n".join(
                [
                    "You are an expert Dafny programmer.",
                    "You are good at fixing timeout errors in Dafny.",
                    "Please Fix the loop or recursion errors that caused timeout in the following Dafny code.",
                    "Given Dafny code:",
                    "```dafny",
                    code,
                    "```",
                    "",
                    "You MUST return the fixed Dafny code in the following format:",
                    "```dafny",
                    "// Dafny code",
                    "```",
                ]
            )
        case "semantic_error":
            return "\\n".join(
                [
                    "You are an expert Dafny programmer.",
                    "You are good at fixing semantic errors in Dafny.",
                    "Please Fix the semantic errors in the following Dafny code.",
                    "Given Dafny code:",
                    "```dafny",
                    code,
                    "```",
                    "",
                    "You MUST return the fixed Dafny code in the following format:",
                    "```dafny",
                    "// Dafny code",
                    "```",
                    "Here is the failed testcase/expectation:",
                    error_messages[0]["content"],
                ]
            )


def parse_errmsg(errmsg: str) -> str:
    if "invalid UnaryExpression" in errmsg:
        return "The body of a Dafny function must be an expression, loops are not allowed; use recursions or methods instead"
    if "Expected 'to' or 'downto'" in errmsg:
        return "The for-loop format in Dafny is ```for i:= a to b```"
    return errmsg


def generate(
    api_config, env_config,
    SourceCode: str,
    status: str,
    error_messages: str,
):
    prompt = template(SourceCode, status, error_messages)
    model = api_config["model"]
    if ("gpt" in model): 
        client = OpenAI(api_key=api_config["openai_api_key"], base_url=api_config["openai_base_url"])
    if ("deepseek" in model): 
        client = OpenAI(api_key=api_config["deepseek_api_key"], base_url=api_config["deepseek_base_url"])
    response = client.beta.chat.completions.parse(
        model=model,
        messages=[{"role": "user", "content": prompt}],
        temperature=api_config["temp"],
    )
    match = re.search(
        r"```dafny\n(?P<code>.+?)\n```",
        response.choices[0].message.content,
        re.DOTALL,
    )
    if match:
        code = match.group("code")
    

    code = code.split("\n")
    is_condition = False
    for lineno in range(len(code)):
        if len(code[lineno].split()) and code[lineno].split()[0] in {
            "requires",
            "ensures",
            "decreases",
            "invariant",
            "reads",
            "modifies",
        }:
            is_condition = True
        if len(code[lineno].split()) and code[lineno].split()[0] == "{":
            is_condition = False
        if is_condition:
            code[lineno] = "//" + code[lineno]
   
    code = "\n".join(code)
    DafnyFilePath = Path(env_config["translation_path"]).joinpath(".dfy")
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


def realtime_eval(
    CodePath: str,
    TestCase: str,
):
    with open(CodePath, "r", encoding="utf-8") as DafnyFile:
        Code = DafnyFile.read()
    TestCode = Code + "\n" + TestCase
    with open(CodePath, "w", encoding="utf-8") as DafnyFile:
        print(TestCode, file=DafnyFile, flush=True)
    process = subprocess.run(
        "dafny test %s --no-verify --standard-libraries" % CodePath,
        timeout=settings.timeout_per_task,
    )
    process = subprocess.run(
        "dafny test %s --no-verify --standard-libraries" % CodePath,
        timeout=settings.timeout_per_task,
        capture_output=True,
    )
    ret = process.returncode
   
    with open(CodePath, "w", encoding="utf-8") as DafnyFile:
        print(Code, file=DafnyFile, flush=True)

    match ret:
        case 0:
            status = "passed"
            error_messages = []
        case 2:
            status = "syntax_error"
            error_messages = []
            try:
                buggy_code = TestCode.split("\n")
                report = process.stdout.decode().replace("\\r", "")
                messages = re.findall(r".dfy(.+?)\\n", report)
                regex_messages = [
                    re.match(
                        r"\((?P<lineno>\d+),(?P<pos>\d+)\): (?P<error>.+)",
                        message,
                    )
                    for message in messages
                ]
                error_messages = [
                    {
                        "content": buggy_code[int(regex_message.group("lineno"))],
                        "line": int(regex_message.group("lineno")),
                        "position": int(regex_message.group("pos")),
                        "error_type": parse_errmsg(regex_message.group("error")),
                    }
                    for regex_message in regex_messages
                ]
            except:
                pass
        case -1:
            status = "timeout"
            error_messages = []
        case 3:
            status = "semantic_error"
            error_messages = []
            try:
                buggy_code = TestCode.split("\n")
                report = process.stdout.decode().replace("\\r", "")
                messages = re.findall(r".dfy(.+?)\\n", report)
                regex_messages = [
                    re.match(
                        r"\((?P<lineno>\d+),(?P<pos>\d+)\): (?P<error>.+)",
                        message,
                    )
                    for message in messages
                ]
                expect = buggy_code[int(regex_messages[0].group("lineno"))]
                assign = expect[expect.find("call") : expect.find("==")]
                assign_value = ""
                for line in buggy_code:
                    if line.find("var " + assign + ":=") != -1:
                        assign_value = line.removeprefix(
                            "var " + assign + ":="
                        ).removesuffix(";")
                        break
                error_messages = [
                    {
                        "content": expect.replace(assign, assign_value),
                        "line": int(regex_messages[0].group("lineno")),
                        "position": int(regex_messages[0].group("pos")),
                    }
                ]
            except:
                pass
    return status, error_messages


def solve(api_config, env_config, problem, testset):
    status = "translate"
    code = problem["python_code"]
    error_messages = []
    for iter in range(env_config["max_fixing_iterations"]):
        generate(api_config, env_config, code, status, error_messages)
        old_status = copy.deepcopy(status)
        old_error_messages = copy.deepcopy(error_messages)
        status, error_messages = realtime_eval(
            Path(env_config["translation_path"]).joinpath(".dfy"),
            testset["TestCase"],
        )
        if status == "passed":
            return
        if status == "syntax_error" and old_status != "syntax_error":
            status, error_messages = old_status, old_error_messages
        else:
            with open(
                Path(env_config["translation_path"]).joinpath(".dfy")
            ) as DAFNY:
                code = DAFNY.read()
            code = code.split("\n")
            code = ["" if line.startswith("import") else line for line in code]
            code = "\\n".join(code)

def main():
    api_config, env_config = get_config()

    with open(env_config["input_json_path"], "r", encoding="utf-8") as JSON:
        problem = json.load(JSON)
    with open(env_config["test_set_json_path"], "r", encoding="utf-8") as JSON:
        testset = json.load(JSON)

    solve(api_config, env_config, problem, testset)
    
if __name__ == '__main__':
    main()