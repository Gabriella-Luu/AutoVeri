
def read_dafny_program(file_path):
    with open(file_path, "r") as file:
        lines = file.readlines()
        dafny_code = "".join(lines)
        return dafny_code
    
def load_json(_file):
    with open(_file, 'r') as j:
        return json.loads(j.read())

import os
import random
import re
import subprocess
import configparser

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
    env_config["test_cases_path"] = os.path.join(os.getcwd(), "output")
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
def process_example(dafny_file_path, test_cases_path):
    try:
        (avg_correct_stats, avg_complete_stats) = process_example_aux(dafny_file_path, test_cases_path)
        # print these stats
        print(f"{dafny_file_path} Average Correctness: {avg_correct_stats}, Average Completeness: {avg_complete_stats}\n---")
    except Exception as e:
        print (f"{dafny_file_path} Error processing file")
        print (e)

        

def process_example_aux(dafny_file_path, test_cases_path):
    # Parse the code
    dafny_code = read_dafny_program(os.path.join(dafny_file_path, 'specgen.dfy'))
    test_cases_code = load_json(os.path.join(test_cases_path, 'test_cases.json'))
    test_cases_code = test_cases_code["TestCase"]
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
    tests = parse_tests(test_cases_code, method_name)    
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

def mutate_value(input_value, ret_type, mutate):
    if not mutate:
        return input_value
    # if mutation is enabled, then mutate the input randomly

    print (f"mutating input_value: {input_value}")
    if ret_type == "int":
        val = int(input_value)
        # randomly choose a positive integer and randomly add or subtract it
        random_val = random.randint(1, 10)
        if random.choice([True, False]):
            input_value = str(val + random_val)
        else:
            input_value = str(val - random_val)
     # if the input is a boolean, then negate it
    elif ret_type == "bv32" or ret_type == "nat":
        input_value = str((random.randint(1, 100) + int(input_value))%1000)
    elif ret_type == "real":
        val = float(input_value)
        random_val = random.randint(1, 100)
        if random.choice([True, False]):
            input_value = str(val + float(random_val)/10)
        else:
            input_value = str(val - float(random_val)/10)
    elif input_value == "true":
        input_value = "false"
    elif input_value == "false":
        input_value = "true"
    elif input_value.startswith("new int[]"):
        # remove the "new int[]" from the input_value
        input_value = input_value.replace("new int[]", "")
        # mutate the array value
        input_value = mutate_array_value(input_value)
        # add "new int[]" back to the input_value
        input_value = "new int[]" + input_value
    elif input_value.startswith("new char[]"):
        input_value = input_value.replace("new char[]", "")
        # mutate the array value
        input_value = mutate_array_value(input_value)
        # add "new int[]" back to the input_value
        input_value = "new char[]" + input_value
    elif input_value.startswith("["): # seq of integers
        input_value = mutate_array_value(input_value)
    else:
        # replace any " " in the string
        input_value = input_value.replace('"', '')
        print (f"entering mutating alnum value with {input_value}")
        # if the input is a alphanumeric string, then add a character to it
        # choose a random character to add or remove from input_value
        random_char = random.choice("abcdefghijklmnopqrstuvwxyz")
        random_pos = random.randint(0, len(input_value)+1)
        if random_pos == len(input_value) + 1:
            input_value = input_value + random_char
        else:
            input_value = input_value[:random_pos] + random_char + input_value[random_pos:]
        input_value = f"\"{input_value}\""
    print (f"mutated input_value: {input_value}")

    return input_value

def mutate_array_value(input_value):
    # remove the "[" and "]" from the input_value
    input_value = input_value[1:-1]
    # parse input_value to get the array of integers
    # input_value = [int(x) for x in input_value.split(",")]
    # not universal
    if "(" in input_value:
        input_value = input_value[:-1].split("),")
        input_value = [item+")" for item in input_value]
    elif "[" in input_value:
        input_value = input_value[:-1].split("],")
        input_value = [item+"]" for item in input_value]
    else:
        input_value = [x.strip() for x in input_value.split(",")]
        
    if input_value[0].replace("-", "").isdigit():
        input_value = [int(x) for x in input_value]
        # randomly choose a position to add or remove an element
        random_pos = random.randint(0, len(input_value)-1)
        # randomly choose a value to add to the array
        # random_val = random.randint(0, 100)
        random_pos1 = random.randint(0, len(input_value)-1)
        if random.choice([True, False]):
            # add the random_val to the array at random_pos
            input_value.insert(random_pos, input_value[random_pos1])
        else:
            # remove the element at random_pos
            input_value.pop(random_pos)
    elif "(" in input_value[0]: #(int, int)?
        random_pos = random.randint(0, len(input_value)-1)
        random_pos1 = random.randint(0, len(input_value)-1)
        random_val = input_value[random_pos1]
        if random.choice([True, False]):
            # add the random_val to the array at random_pos
            input_value.insert(random_pos, random_val)
        else:
            # remove the element at random_pos
            input_value.pop(random_pos)
    elif "'" in input_value[0]: # char?
        input_value = [x.replace("'", "") for x in input_value]
        # randomly choose a position to add or remove an element
        random_pos = random.randint(0, len(input_value)-1)
        random_pos1 = random.randint(0, len(input_value)-1)
        # randomly choose a value to add to the array
        # random_val = random.choice("abcdefghijklmnopqrstuvwxyz")
        random_val = input_value[random_pos1]
        if random.choice([True, False]):
            # add the random_val to the array at random_pos
            input_value.insert(random_pos, random_val)
        else:
            # remove the element at random_pos
            input_value.pop(random_pos)
        input_value = ["'"+x+"'" for x in input_value]
    elif '"' in input_value[0]: # string?
        input_value = [x.replace('"', "") for x in input_value]
        # randomly choose a position to add or remove an element
        random_pos = random.randint(0, len(input_value)-1)
        random_pos1 = random.randint(0, len(input_value)-1)
        # randomly choose a value to add to the array
        # random_val = random.choice("abcdefghijklmnopqrstuvwxyz")
        random_val = input_value[random_pos1]
        if random.choice([True, False]):
            # add the random_val to the array at random_pos
            input_value.insert(random_pos, random_val)
        else:
            # remove the element at random_pos
            input_value.pop(random_pos)
        input_value = ['"'+x.strip()+'"' for x in input_value]
    elif "[" in input_value[0]:
        random_pos = random.randint(0, len(input_value)-1)
        random_pos1 = random.randint(0, len(input_value)-1)
        # random_val1 = random.randint(0, 100)
        # random_val2 = random.randint(0, 100)
        # random_val = [random_val1, random_val2]
        random_val = input_value[random_pos1]
        if random.choice([True, False]):
            # add the random_val to the array at random_pos
            input_value.insert(random_pos, random_val)
        else:
            # remove the element at random_pos
            input_value.pop(random_pos)    # convert the input_value back to the string
    return "[" + ",".join([str(x) for x in input_value]) + "]"

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
    # not universal
    if "seq<(" in params:
        params = params.split('>,')
        params[0] = params[0]+">"
    else:
        params = params.split(',')
    # for param in params:
    #     test_harness_code += f"  //param {param.strip()};\n"
    temp_out_params = re.search(r'.*returns\s*\((.*?)\)\s*\n', prefix)
    if temp_out_params:
        out_params = temp_out_params.group(1)
    else:
        out_params = ""
    # we only handle a single output for now
    # assert out_params.count(',') == 0, "Multiple outputs not supported"
    # for out_param in out_params.split(','):
    #     test_harness_code += f"  //out_param {out_param};\n"
    
    for index, input_value in enumerate(test['inputs']):
        # don't mutate inputs, only outputs
        test_harness_code += f"  var v{index} := {input_value}\n"
  
    # create a zip of params and args
    for index, (param1, inp) in enumerate(zip(params, test['inputs'])):
        # print (f"param: {param1}, arg: {arg}")
        # strip the type from x: type from params
        param = param1.split(':')[0]
        # check if the arg is an array
        type = param1.split(':')[1].strip()
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
            # not universal
            if "]," in inp:
                size = inp.count("],") + 1
            else:
                size = inp.count(",") + 1
            # need to add redundant asserts to make dafny happy by iterating over all elements of the array
            # explicitly add the equality of the elements upto the size
            test_harness_code += f"  //redundant asserts to make dafny happy\n"
            for i in range(size):
                test_harness_code += f"  assert {param2.strip()}[{i}] == v{index}[{i}];\n"
        else:
            test_harness_code += f"  assume {{:axiom}} {param.strip()} == v{index};\n"
            if "seq<seq" in type:
                size = inp.count("],[") + inp.count("], [") + 1
                for i in range(size):
                    test_harness_code += f"  assert {param.strip()}[{i}] == v{index}[{i}];\n"
            elif "seq" in type:
                if "]," in inp:
                # if "]," in inp or "[[" in inp:
                    size = inp.count("],") + 1
                elif "[(" in inp:
                    size = inp.count("),") + 1
                else:
                    size = inp.count(",") + 1
                test_harness_code += f"  //redundant asserts to make dafny happy\n"
                for i in range(size):
                    test_harness_code += f"  assert {param.strip()}[{i}] == v{index}[{i}];\n"
                # test_harness_code += f"  assert {param.strip()}[0] == v{index}[0];\n"
            elif "string" in type:
                size = len(inp[1:-2])
                for i in range(size):
                    test_harness_code += f"  assert {param.strip()}[{i}] == v{index}[{i}];\n"

    # create a zip of out_params and test['outputs']
    if out_params != "":
        [out_param, ret_type] = out_params.split(':')
    else:
        for input in params:
            if "array" in input:
                [out_param, ret_type] = input.split(':')
                break
    ret_value = test['outputs'][0] # assuming a single output for now
    # add "new int[]" to the out_param if the type of the out_param is array and ret_param has no "new int[]"
    # print (f"out_params: {out_params}, ret_value: {ret_value}")
    match = re.match(r'array<(\w+)>$', ret_type.strip())
    if match:
        if not "new" in ret_value:
            ret_value = "new " + match.group(1).strip() + "[]" + ret_value 

    ret_value = mutate_value(ret_value, ret_type.strip(), mutate)
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

# add command line arguments to the harness
import sys
import json

# Example usage
def main():
    env_config = get_config()
    with open(os.path.join(env_config["base_output_path"], "eval.txt"), 'w', encoding='utf-8') as f:
        sys.stdout = f
        process_example(env_config["specs_path"], env_config["test_cases_path"])    
        sys.stdout = sys.__stdout__

if __name__ == "__main__":
    main()

