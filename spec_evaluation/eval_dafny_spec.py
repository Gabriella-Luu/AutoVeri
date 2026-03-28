import json
import os
import random
import re
import subprocess
import sys
import configparser

from common.testcase_utils import normalize_spec_test_cases, python_value_to_dafny_literal

def read_dafny_program(file_path):
    with open(file_path, "r") as file:
        lines = file.readlines()
        dafny_code = "".join(lines)
        return dafny_code
    
def load_json(_file):
    with open(_file, 'r') as j:
        return json.loads(j.read())

def get_config():
    script_dir_path = os.getcwd()
    config_path = os.path.join(script_dir_path, 'env.config')
    if not (os.path.exists(config_path)):
        print("env.config not found!!")
        return
    config = configparser.ConfigParser()
    config.read(config_path)

    env_config = dict()
    env_config["specs_path"] = os.path.join(os.getcwd(), "output")
    env_config["input_json_path"] = os.path.join(os.getcwd(), "input/input.json")
    env_config["base_output_path"] = os.path.join(os.getcwd(), "output")
    return env_config

def parse_code_blocks(source_code):
    # Define states
    BEGIN_BLOCK, IN_BLOCK, NO_BLOCK = 'BEGIN_BLOCK', 'IN_BLOCK', 'NO_BLOCK'
    state = NO_BLOCK

    # Define keywords and initialize variables
    keywords = ['predicate', 'function', 'method', 'lemma', 'ghost function']
    blocks = []
    index = 0
    block_type = []
    block_name = []
    block_prefix = []
    block_body = []
    current_prefix = ''
    current_name = None
    current_body = ''
    current_type = ''
    brace_stack = []

    # Helper function to reset the current block when it ends
    def reset_current_block():
        nonlocal current_type, current_body, current_prefix, state
        block_body.append(current_body)
        #block_type.append(current_type)
        current_body = ''
        current_prefix = ''
        current_name = None
        state = NO_BLOCK

    # Process each character in the source code
    while index < len(source_code):
        char = source_code[index]

        if state == NO_BLOCK:
            # Check if the upcoming substring is a keyword
            for keyword in keywords:
                if source_code[index:].startswith(keyword):
                    state = BEGIN_BLOCK
                    block_type.append(keyword)
                    index += len(keyword)
                    block_prefix.append('')
                    # find the block name as the first word after the keyword
                    b_name = re.search(r'\w+', source_code[index:])
                    current_name = b_name.group(0)  
                    block_name.append(current_name)
                    # increment the index by the length of the block name
                    index += len(current_name)
                    break

        elif state == BEGIN_BLOCK:
            if char == '{' and (source_code[index+1] == '\n' or source_code[index+1] == '}'):
                brace_stack.append(char)
                state = IN_BLOCK
                current_body += char
            else:
                block_prefix[-1] += char

        elif state == IN_BLOCK:
            current_body += char
            if char == '{':
                brace_stack.append(char)
            elif char == '}':
                brace_stack.pop()
                if not brace_stack:  # End of block detected
                    reset_current_block()
                    continue

        index += 1

    # If the file ends but still in IN_BLOCK state, finalize the last block
    if state == IN_BLOCK and brace_stack == []:
        reset_current_block()

    # Combine prefixes and bodies
    combined_blocks = [{'name' : name, 'type' : type, 'prefix': prefix, 'body': body} for name, type, prefix, body in zip(block_name, block_type, block_prefix, block_body)]

    return combined_blocks

def parse_tests(source_code, callee_name):
    test_cases = []
    # test_inputs = []
    current_test = None

    lines = source_code.split('\n')
    for line in lines:
        line = line.strip()
        
        # Detect variable initialization with array assignment
        if (line.startswith('var') and ':=' in line) or (callee_name in line):
            if current_test is None:
                current_test = {'inputs': [], 'outputs': []}
                temp_inputs = {}
            if callee_name not in line: 
                var_name, var_value = line.split(':=')[0].strip(), line.split(':=')[1].strip()
                var_name = var_name.replace('var ', '').strip()
                temp_inputs[var_name] = var_value
            else:
                pattern = r'\((.*?)\)' 
                match = re.search(pattern, line)
                if match:
                    arguments_str = match.group(1)
                    json_str = "[" + arguments_str + "]"
                    data_list = json.loads(json_str)
                    for arg in data_list:
                        arg = str(arg)
                        for key, value in temp_inputs.items():
                            if arg.strip() in key and not arg.strip().isdigit():
                                current_test['inputs'].append(value)
                                break
                        else:
                            var_value = arg.strip()
                            current_test['inputs'].append(var_value + ";")
                    
                    # find corresponding output
                    var_name = line.split(':=')[0].strip()
                    output_line_startwith = var_name.replace('var ', 'expect ').strip()
                    for output_line in lines:
                        if output_line_startwith in output_line:
                            output_value = output_line.split('==')[1].replace(";", "").replace("}", "").strip()
                            current_test['outputs'].append(output_value)
                            test_cases.append(current_test)
                            current_test = None
                            break
    
    return test_cases

# a wrapper around process_example that checks for exceptions
def process_example(dafny_file_path, input_json_path):
    try:
        (avg_correct_stats, avg_complete_stats) = process_example_aux(dafny_file_path, input_json_path)
        # print these stats
        print(f"{dafny_file_path} Average Correctness: {avg_correct_stats}, Average Completeness: {avg_complete_stats}\n---")
    except Exception as e:
        print (f"{dafny_file_path} Error processing file")
        print (e)

        

def process_example_aux(dafny_file_path, input_json_path):
    # Parse the code
    dafny_code = read_dafny_program(os.path.join(dafny_file_path, 'specgen.dfy'))
    problem = load_json(input_json_path)
    tests = normalize_spec_test_cases(problem["spec_test_cases"])
    dafny_code_parsed_blocks = parse_code_blocks(dafny_code)
    for block in dafny_code_parsed_blocks:
        print(f"Name: {block['name']}")
        print(f"Type: {block['type']}")
        print(f"Prefix:\n{block['prefix']}")
        print(f"Body:\n{block['body']}\n---")

    # correctness stats 
    correct_stats = 0
    complete_stats = 0

    pattern = r'method\s+(\w+)\s*\('
    match = re.search(pattern, dafny_code, re.MULTILINE)
    if match:
        method_name = match.group(1)
    max_mutations = 5
    for i, test in enumerate(tests):
        # create an outcome for each test and mutants
        outcomes = []
        # print(f"Inputs: {test['inputs']}")
        # print(f"Method Call: {test['method_call']}")
        # print(f"Expected Output: {test['outputs']}\n---")
        tmp = generate_dafny_test_harness(test, dafny_code_parsed_blocks, dafny_file_path)
        outcomes.append((-1, tmp))
        # generate some mutations of the test harness
        for i in range(max_mutations):
            tmp = generate_dafny_test_harness(test, dafny_code_parsed_blocks, dafny_file_path, mutate=True)
            outcomes.append((i, tmp))
        # print only the 2nd component of each outcome
        print(f"{dafny_file_path} Outcomes: {[(i, res) for (i, (a, res, b)) in outcomes]}\n---")
        # print a stripped version where we only look for # of errors in the string
        # look for a regex ".*Dafny program verifier finished with \d+ verified, \d+ error.*"
        # first get the compressed outcomes using the regex on 2nd component of each outcome
        compressed_outcomes = [re.search(r'.*Dafny program verifier finished with (\d+) verified, (\d+) error.*', res, re.DOTALL) for (i, (a, res, b)) in outcomes]
        # then get the number of errors from the compressed outcomes
        errors = [int(x.group(2)) for x in compressed_outcomes]
        print(f"{dafny_file_path} Dafny Statistics for test: {errors}\n---")
        # update correct and complete stats
        correct_stats += 1 if errors[0] == 0 else 0
        # check number of incorrect mutations as number of 1s in errors[1:]
        complete_stats += (max_mutations - errors[1:].count(0))
    avg_correct_stats = correct_stats/len(tests)
    avg_complete_stats = complete_stats/(len(tests)*max_mutations)
    return (avg_correct_stats, avg_complete_stats)

def split_top_level_fields(text):
    fields = []
    current = []
    stack = []
    matching = {')': '(', ']': '[', '>': '<', '}': '{'}
    openers = set(matching.values())
    closers = set(matching.keys())
    for char in text:
        if char == ',' and not stack:
            field = "".join(current).strip()
            if field:
                fields.append(field)
            current = []
            continue
        current.append(char)
        if char in openers:
            stack.append(char)
        elif char in closers and stack and stack[-1] == matching[char]:
            stack.pop()
    field = "".join(current).strip()
    if field:
        fields.append(field)
    return fields

def convert_value_to_declared_type(value, declared_type):
    literal = python_value_to_dafny_literal(value)
    array_match = re.match(r'array<(.+)>$', declared_type.strip())
    if array_match:
        return f"new {array_match.group(1).strip()}[]{literal}"
    return literal

def mutate_value(input_value, ret_type, mutate):
    if not mutate:
        return convert_value_to_declared_type(input_value, ret_type)
    # if mutation is enabled, then mutate the input randomly
    
    print (f"mutating input_value: {input_value}")
    if ret_type == "int" and isinstance(input_value, int):
        val = int(input_value)
        # randomly choose a positive integer and randomly add or subtract it
        random_val = random.randint(1, 10)
        if random.choice([True, False]):
            input_value = str(val + random_val)
        else:
            input_value = str(val - random_val)
     # if the input is a boolean, then negate it
    elif (ret_type == "bv32" or ret_type == "nat") and isinstance(input_value, int):
        input_value = str((random.randint(1, 100) + int(input_value))%1000)
    elif ret_type == "real" and isinstance(input_value, (int, float)):
        val = float(input_value)
        random_val = random.randint(1, 100)
        if random.choice([True, False]):
            input_value = str(val + float(random_val)/10)
        else:
            input_value = str(val - float(random_val)/10)
    elif isinstance(input_value, bool):
        input_value = not input_value
    elif isinstance(input_value, list):
        input_value = mutate_array_value(input_value)
    else:
        input_value = str(input_value)
        input_value = input_value.replace('"', '')
        print (f"entering mutating alnum value with {input_value}")
        random_char = random.choice("abcdefghijklmnopqrstuvwxyz")
        random_pos = random.randint(0, len(input_value)+1)
        if random_pos == len(input_value) + 1:
            input_value = input_value + random_char
        else:
            input_value = input_value[:random_pos] + random_char + input_value[random_pos:]
    print (f"mutated input_value: {input_value}")

    if ret_type == "int" or ret_type == "bv32" or ret_type == "nat":
        return input_value
    if ret_type == "real":
        return input_value
    return convert_value_to_declared_type(input_value, ret_type)

def mutate_array_value(input_value):
    input_value = list(input_value)
    if not input_value:
        input_value.append(0)
        return input_value
    random_pos = random.randint(0, len(input_value)-1)
    random_pos1 = random.randint(0, len(input_value)-1)
    random_val = input_value[random_pos1]
    if random.choice([True, False]):
        input_value.insert(random_pos, random_val)
    else:
        input_value.pop(random_pos)
    return input_value

def generate_dafny_test_harness(test, parsed_blocks, dafny_file_path, mutate=False):
    """
    Generate a test harness for the given test case and parsed blocks
    Really hacky string/regex processing, but it works for now except array equality
    """

    # find all non-method blocks
    non_method_blocks = [block for block in parsed_blocks if block['type'] != 'method']
    # inline these blocks in the test harness
    test_harness_code = ""  
    for block in non_method_blocks:
        test_harness_code += f"{block['type']} {block['name']} {block['prefix']}\n{block['body']}\n"

    # callee_block = next(block for block in parsed_blocks if block['name'].lower() == test['method_call'].lower())
    callee_block = parsed_blocks[0]
    # callee_block_body = callee_block['body']
    test_harness_code += f"method {callee_block['name']} {callee_block['prefix']}{{\n"

    # parse the arguments and return from prefix of the block
    prefix = callee_block['prefix']
    params = re.search(r'\((.*?)\)\s*returns', prefix).group(1)
    params = split_top_level_fields(params)
    # for param in params:
    #     test_harness_code += f"  //param {param.strip()};\n"
    temp_out_params = re.search(r'.*returns\s*\((.*?)\)\s*\n', prefix)
    if temp_out_params:
        out_params = split_top_level_fields(temp_out_params.group(1))
    else:
        out_params = []
    
    input_literals = []
    for index, (input_value, param1) in enumerate(zip(test['inputs'], params)):
        declared_type = param1.split(':', 1)[1].strip()
        input_literal = convert_value_to_declared_type(input_value, declared_type)
        input_literals.append(input_literal)
        # don't mutate inputs, only outputs
        test_harness_code += f"  var v{index} := {input_literal}\n"
  
    # create a zip of params and args
    for index, (param1, inp, inp_literal) in enumerate(zip(params, test['inputs'], input_literals)):
        # print (f"param: {param1}, arg: {arg}")
        # strip the type from x: type from params
        param = param1.split(':', 1)[0]
        # check if the arg is an array
        type = param1.split(':', 1)[1].strip()
        # check if type contains the regex pattern array<\w+>
        # if re.search(r'array<\w+>', type):
        if "array" in type and "<array" not in type:
            # if it does, then we need to convert arg to sequence
            param2 = param 
            param = param + f"[..{param}.Length]"
            arg = f"v{index}[..v{index}.Length]"  
            # cannot compare arrays as they are references
            # so need to compare each element of the array
            test_harness_code += f"  //need to equate the elements of the array, and not reference (which is inconsistent)\n"
            test_harness_code += f"  assume {{:axiom}} {param.strip()} == {arg.strip()};\n"

            # extract the size of the array by looking for number of "," in the arg
            size = len(inp)
            # need to add redundant asserts to make dafny happy by iterating over all elements of the array
            # explicitly add the equality of the elements upto the size
            test_harness_code += f"  //redundant asserts to make dafny happy\n"
            for i in range(size):
                test_harness_code += f"  assert {param2.strip()}[{i}] == v{index}[{i}];\n"
        else:
            test_harness_code += f"  assume {{:axiom}} {param.strip()} == v{index};\n"
            if "seq<seq" in type:
                size = len(inp)
                for i in range(size):
                    test_harness_code += f"  assert {param.strip()}[{i}] == v{index}[{i}];\n"
            elif "seq" in type:
                size = len(inp)
                test_harness_code += f"  //redundant asserts to make dafny happy\n"
                for i in range(size):
                    test_harness_code += f"  assert {param.strip()}[{i}] == v{index}[{i}];\n"
                # test_harness_code += f"  assert {param.strip()}[0] == v{index}[0];\n"
            elif "string" in type:
                size = len(inp)
                for i in range(size):
                    test_harness_code += f"  assert {param.strip()}[{i}] == v{index}[{i}];\n"

    declared_outputs = out_params
    if not declared_outputs:
        for input_param in params:
            if "array" in input_param:
                declared_outputs = [input_param]
                break
    if len(declared_outputs) != len(test['outputs']):
        raise ValueError("The number of expected outputs does not match the method signature")
    for out_param_decl, output_value in zip(declared_outputs, test['outputs']):
        out_param, ret_type = out_param_decl.split(':', 1)
        ret_value = mutate_value(output_value, ret_type.strip(), mutate)
        test_harness_code += f"  {out_param.strip()} := {ret_value.strip()};\n"
   
    # test_harness_code += f"  //var expectedOutput := {ret_param};\n"
    test_harness_code += f"}}"
    print("\n------------\nTest Harness Code:\n-----------------")
    print(test_harness_code)
    with open(os.path.join(dafny_file_path, "test_harness.dfy"), "w") as file:
        file.write(test_harness_code)

    # invoke dafny on the test harness file with argument "verify"
    result = subprocess.run(["dafny",  "verify", "--allow-warnings", os.path.join(dafny_file_path, "test_harness.dfy")], capture_output=True, text=True)
    print(result.stdout)
    print(result.stderr)

    return (test_harness_code, result.stdout, result.stderr)

# Example usage
def main():
    env_config = get_config()
    with open(os.path.join(env_config["base_output_path"], "eval.txt"), 'w', encoding='utf-8') as f:
        sys.stdout = f
        process_example(env_config["specs_path"], env_config["input_json_path"])    
        sys.stdout = sys.__stdout__

if __name__ == "__main__":
    main()
