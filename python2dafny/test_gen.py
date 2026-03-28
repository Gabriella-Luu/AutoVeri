from openai import OpenAI
import os
import json
import re
import ast
from typing import Tuple
import configparser

def get_config():
    script_dir_path = os.getcwd()
    config_path = os.path.join(script_dir_path, 'env.config')
    if not (os.path.exists(config_path)):
        print("env.config not found!!")
        return
    config = configparser.ConfigParser()
    config.read(config_path)

    api_config = dict()
    api_config["openai_api_key"] = config.get('DEFAULT', 'openai_api_key')
    api_config["openai_base_url"] = config.get('DEFAULT', 'openai_base_url')
    api_config["model"] = config.get('TRANS', 'model')
    api_config["temp"] = float(config.get('TRANS', 'temp'))

    env_config = dict()
    env_config["input_json_path"] = os.path.join(os.getcwd(), "input/input.json")
    env_config["test_set_json_path"] = os.path.join(os.getcwd(), "output")
    env_config["max_test_gen_iterations"] = int(config.get('TRANS', 'max_test_gen_iterations'))
    env_config["test_cases_num"] = int(config.get('TRANS', 'test_cases_num'))
    return api_config, env_config

def template(code):
    return "\\n".join(
        [
            "You are an expert Python programmer.",
            "You are good at generating test inputs for Python functions.",
            "",
            "Please generate ten groups of differentiated valid inputs for the following Python function.",
            "Pay attention to the data types of input and output. When the data type is string, the data should be enclosed in quotation marks.",
            "Keep all input types strictly consistent, and avoid using floating-point numbers unless necessary.",
            "",
            "Given Python function:",
            "```python",
            code,
            "```",
            "",
            "You MUST return the inputs in the following format without any comments:",
            "```python",
            "input1=(parameter1,parameter2,...)",
            "input2=(parameter1,parameter2,...)",
            "...",
            "input10=(parameter1,parameter2,...)",
            "```",
        ]
    )

def format(value) -> str:
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, str):
        return '"' + value.replace("\n", "\\n") + '"'
    if isinstance(value, float):
        return f"{value:.8f}"
    return str(value)

def parse_op(op: ast.operator) -> str:
    match op.__class__:
        case ast.Add | ast.UAdd:
            return "+"
        case ast.Sub | ast.USub:
            return "-"
        case ast.Mult:
            return "*"
        case ast.Div:
            return "/"
        case ast.Mod:
            return "%"
        case ast.Eq | ast.Is:
            return "=="
        case ast.NotEq:
            return "!="
        case ast.Lt:
            return "<"
        case ast.LtE:
            return "<="
        case ast.Gt:
            return ">"
        case ast.GtE:
            return ">="
        case ast.Not:
            return "!"
        case _:
            print(op.__class__)
            assert False

def parse_expr(src: ast.expr) -> Tuple[set[str], list[str], str]:
    libs: set[str] = set()
    calls: list[str] = []
    target: str = ""
    match src:
        case ast.Call(func, args, keywords):
            for arg in args:
                _libs, _calls, _target = parse_expr(arg)
                libs = set.union(libs, _libs)
                calls += _calls
                target += _target
                target += ","
            target = "(%s)" % (target.strip(","))
            match func:
                case ast.Name(id, ctx):
                    global func_calls
                    target = id + target
                    caller = "call%d" % func_calls
                    func_calls += 1
                    calls.append("var %s:= %s ;" % (caller, target))
                    target = caller
                case _:
                    assert False
            pass
        case ast.Compare(left, ops, comparators):
            libs, calls, target = parse_expr(left)
            assert len(ops) == len(comparators)
            for index in range(len(ops)):
                target += parse_op(ops[index])
                _libs, _calls, _target = parse_expr(comparators[index])
                libs = set.union(libs, _libs)
                calls += _calls
                target += _target
        case ast.Constant(value, kind):
            target = format(value)
        case ast.List(elts, ctx):
            for index, expr in enumerate(elts):
                _libs, _calls, _target = parse_expr(expr)
                libs = set.union(libs, _libs)
                calls += _calls
                target += _target
                target += ","
            target = "[%s]" % (target.strip(","))
        case ast.Tuple(elts, ctx):
            for index, expr in enumerate(elts):
                _libs, _calls, _target = parse_expr(expr)
                libs = set.union(libs, _libs)
                calls += _calls
                target += _target
                target += ","
            target = "(%s)" % (target.strip(","))
        case ast.Dict(keys, values):
            assert len(keys) == len(values)
            for index in range(len(keys)):
                _libs, _calls, _target = parse_expr(keys[index])
                libs = set.union(libs, _libs)
                calls += _calls
                target += _target
                _libs, _calls, _target = parse_expr(values[index])
                libs = set.union(libs, _libs)
                calls += _calls
                target += ":=" + _target + ","
            target = "map[%s]" % (target.strip(","))
        case ast.BinOp(left, op, right):
            if left.__class__ == ast.Constant and right.__class__ == ast.Constant:
                target = format(eval(ast.unparse(src)))
            else:
                left_libs, left_calls, left_target = parse_expr(left)
                right_libs, right_calls, right_target = parse_expr(right)
                libs = set.union(left_libs, right_libs)
                calls = left_calls + right_calls
                target = left_target + parse_op(op) + right_target
        case ast.UnaryOp(op, operand):
            libs, calls, target = parse_expr(operand)
            target = parse_op(op) + target
        case ast.Name(id, ctx):
            assert id in TmpVars
            target = format(TmpVars[id])
        case _:
            print(ast.dump(src))
            assert False
    return libs, calls, target

def parse_cases(testcases: list[ast.stmt]) -> str | None:
    libs: set[str] = set()
    calls: list[str] = []
    tests: list[str] = []
    global func_calls
    global TmpVars
    func_calls = 0
    TmpVars = {}
    output: str = ""
    for index, testcase in enumerate(testcases):
        match testcase:
            case ast.Assert(test, msg):
                _libs, _calls, target = parse_expr(test)
                libs = set.union(libs, _libs)
                calls += _calls
                tests.append("expect %s;" % target)
            case ast.Assign(targets, value, type_comment):
                result = eval(ast.unparse(value), TmpVars)
                for target in targets:
                    match target:
                        case ast.Name(id, ctx):
                            TmpVars[id] = result
                        case _:
                            print(ast.dump(target))
                            assert False
            case ast.Call(func, args, keywords):
                match func:
                    case ast.Name(id, ctx):
                        if id == "print":
                            continue
                return None
            case ast.Expr(value):
                continue
            case _:
                return None
    body: str = ""
    for call in calls:
        body += call + "\n"
    for test in tests:
        body += test + "\n"
    output = "%smethod{:test} check(){\n%s}\n" % (output, body)
    return output

def solve(
    api_config, env_config,
    SourceCode: str,
):
    for iterations in range(env_config["max_test_gen_iterations"]):
        testset = {"TranslationTestCase": ""}

        prompt = template(SourceCode)
        client = OpenAI(
            api_key=api_config["openai_api_key"],
            base_url=api_config["openai_base_url"],
        )

        response = client.beta.chat.completions.parse(
            model=api_config["model"],
            messages=[{"role": "user", "content": prompt}],
            temperature=api_config["temp"],
        )
        match = re.search(
            r"```python\n(?P<code>.+?)\n```",
            response.choices[0].message.content,
            re.DOTALL,
        )
        if match:
            inputs = match.group("code")

        # inputs = 'input1=([1, 2, 3, 4, 5], 6)\ninput2=([10, 20, 30, 40, 50], 25)\ninput3=([100, 200, 300, 400, 500], 600)\ninput4=([-10, -20, -30, -40, -50], -5)\ninput5=([0, 0, 0, 0, 0], 1)\ninput6=([1, 1, 1, 1, 1], 0)\ninput7=([10, 9, 8, 7, 6], 5)\ninput8=([-1, -2, -3, -4, -5], -6)\ninput9=([100, 99, 98, 97, 96], 101)\ninput10=([0, 0, 0, 0, 0], 0)'
        LocalNameSpace = {}
        try:
            exec(SourceCode, LocalNameSpace)
            exec(inputs, LocalNameSpace)
        except:
            continue
        pattern = r'def\s+(\w+)\s*\('
        match = re.search(pattern, SourceCode, re.MULTILINE)
        if match:
            method_name = match.group(1)
        assertions = ""
        for i in range(env_config["test_cases_num"]):
            tmp = eval("str(input%d)" % (i + 1), LocalNameSpace)
            temp_res = eval(method_name + tmp, LocalNameSpace)
            res = str(temp_res)
            if type(temp_res) == str:
                res = "'" + res + "'"
            if res != "None":
                assertions += (
                    "assert %s%s == "% (method_name, tmp)
                    + res
                    + "\n"
                )
        
        global func_calls, TmpVars, SrcName
        func_calls = 0
        TmpVars = {}
        SrcName = ""
        try:
            parsed = parse_cases(ast.parse(source=assertions).body)
            testset["TranslationTestCase"] = parsed
            break
        except:
            continue
        
    return testset

def main():
    api_config, env_config = get_config()
    
    with open(env_config["input_json_path"], "r", encoding="utf-8") as JSON:
        problem = json.load(JSON)
    
    testset = solve(api_config, env_config, problem["python_code"])

    passed = 0
    if testset != "":
        passed += 1

    with open(env_config["test_set_json_path"] + "/translation_test_cases.json", "w", encoding="utf-8") as JSON:
        json.dump(testset, JSON)

    print(
        "Testcases generation finished."
    )
if __name__ == '__main__':
    main()
