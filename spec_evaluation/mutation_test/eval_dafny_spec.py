
def read_dafny_program(file_path):
    with open(file_path, "r") as file:
        lines = file.readlines()
        dafny_code = "".join(lines)
        return dafny_code

import os
import random
import re
import subprocess

# global variables
max_mutations = None
dafny_binary_path = None
smoke_test = False
user_labels_json = []

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
                    for arg in arguments_str.split(','):
                        for key, value in temp_inputs.items():
                            if arg.strip() in key and not arg.strip().isdigit():
                                current_test['inputs'].append(value)
                                break
                        else:
                            var_value = arg.strip()
                            current_test['inputs'].append(var_value + ";")

        # Detect expected results in comments
        elif line.startswith('//expected'):
            expected_result = line.replace('//expected', '').strip().strip(';').strip()
            current_test['outputs'].append(expected_result)
            test_cases.append(current_test)
            # test_inputs.append(temp_inputs)
            current_test = None
            
        # Detect expected results in comments
        elif line.startswith('// expected'):
            expected_result = line.replace('// expected', '').strip().strip(';').strip()
            current_test['outputs'].append(expected_result)
            test_cases.append(current_test)
            # test_inputs.append(temp_inputs)
            current_test = None

        # Detect assert statement for output
        elif line.startswith('assert') or line.startswith('//assert') or line.startswith('expect'):
            output_value = line.split('==')[1].strip().strip(';').strip()
            current_test['outputs'].append(output_value)
            test_cases.append(current_test)
            # test_inputs.append(temp_inputs)
            current_test = None

        # Detect method call and expected result
        if callee_name in line:
            # result_assignment, method_call = line.split(':=')
            current_test['method_call'] = callee_name

    return test_cases

# a wrapper around process_example that checks for exceptions
def process_example(dafny_file_path_prefix):

    try:
        user_label = None
        if not (user_labels_json  == []):
            # find out the user-label for this example
            # find the suffix id of dafny_file_path_prefix by splitting on "task_id_<id>"
            suffix = (dafny_file_path_prefix.split("task_id_")[1]).strip()
            print (f"{dafny_file_path_prefix} Suffix: {suffix}")
            # find user label by looking for "suffix" in "id" field and reading the "note" field
            user_label = next(label["note"] for label in user_labels_json if label["id"] == suffix)
            # strip any newlines for printing
            user_label = user_label.replace("\n", "").strip()

        (avg_correct_stats, avg_complete_stats) = process_example_aux(dafny_file_path_prefix)
        # print these stats
        print(f"{dafny_file_path_prefix} Average Correctness: {avg_correct_stats}, Average Completeness: {avg_complete_stats}, User label: {user_label}\n---")
    except Exception as e:
        print (f"{dafny_file_path_prefix} Error processing file")
        print (e)

        

def process_example_aux(dafny_file_path_prefix):
    # my code
    dafny_file_path = dafny_file_path_prefix + ".dfy"
    task_id = re.search(r"task_id_(\d+)", dafny_file_path).group(1) 
    test_cases_path = "/Users/luyihan/Desktop/AutoVeri/experiments/all_tests/task_id_" + task_id + ".dfy"
    print(f"Processing file: {dafny_file_path}")
    dafny_file_code = read_dafny_program(dafny_file_path)
    dafny_code = re.search(r'```dafny(.*?)```', dafny_file_code, flags=re.DOTALL).group(1).strip()
    if dafny_code[-1] != '}':
        dafny_code = dafny_code + "\n{\n}"
    test_cases_code = read_dafny_program(test_cases_path)
    
    # Parse the code
    dafny_code_parsed_blocks = parse_code_blocks(dafny_code)
    test_cases_parsed_blocks = parse_code_blocks(test_cases_code)
    for block in dafny_code_parsed_blocks:
        print(f"Name: {block['name']}")
        print(f"Type: {block['type']}")
        print(f"Prefix:\n{block['prefix']}")
        print(f"Body:\n{block['body']}\n---")

    # get test body
    # find the name of the block being invoked in block named 'Main'
    main_block = next(block for block in test_cases_parsed_blocks if block['name'] == 'Main')
    invoked_block_name = re.search(r'\w+', main_block['body']).group(0)
    print (f"Block being invoked in Main: {invoked_block_name}")
    #find the block being invoked in the main block
    invoked_block = next(block for block in test_cases_parsed_blocks if block['name'] == invoked_block_name)
    # find the body of the invoked block
    invoked_block_body = invoked_block['body']
    # remove "Test" suffix from the invoked block name
    invoked_block_name = invoked_block_name.replace("Test", "")

    # correctness stats 
    correct_stats = 0
    complete_stats = 0

    tests = parse_tests(invoked_block_body, invoked_block_name)    
    for i, test in enumerate(tests):
        # create an outcome for each test and mutants
        outcomes = []
        # print(f"Inputs: {test['inputs']}")
        # print(f"Method Call: {test['method_call']}")
        # print(f"Expected Output: {test['outputs']}\n---")
        tmp = generate_dafny_test_harness(test, dafny_code_parsed_blocks, dafny_file_path_prefix)
        outcomes.append((-1, tmp))
        # generate some mutations of the test harness
        for i in range(max_mutations):
            tmp = generate_dafny_test_harness(test, dafny_code_parsed_blocks, dafny_file_path_prefix, mutate=True)
            outcomes.append((i, tmp))
        # print only the 2nd component of each outcome
        print(f"{dafny_file_path_prefix} Outcomes: {[(i, res) for (i, (a, res, b)) in outcomes]}\n---")
        # print a stripped version where we only look for # of errors in the string
        # look for a regex ".*Dafny program verifier finished with \d+ verified, \d+ error.*"
        # first get the compressed outcomes using the regex on 2nd component of each outcome
        compressed_outcomes = [re.search(r'.*Dafny program verifier finished with (\d+) verified, (\d+) error.*', res, re.DOTALL) for (i, (a, res, b)) in outcomes]
        # then get the number of errors from the compressed outcomes
        errors = [int(x.group(2)) for x in compressed_outcomes]
        print(f"{dafny_file_path_prefix} Dafny Statistics for test: {errors}\n---")
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
    # test_harness_code += f"  //var {test['inputs'][-1][0]} := {test['inputs'][-1][1]};\n"

    # parse the arguments and returns from the last line of test['inputs']
    # ret_params = test['inputs'][-1]
    # ret_params = ret_params.split(',')
    # for ret_param in ret_params:
    #     test_harness_code += f"  //ret_args {ret_param.strip()};\n"
    
    # # parse input_value from the last line of test['inputs']
    # input_value = test['inputs'][-1][1]
    # # parse the args 
    # args = re.search(r'\((.*?)\)', input_value).group(1)
    # args = args.split(',')
    # for arg in args:
    #     test_harness_code += f"  //arg {arg.strip()};\n"

    #test_harness_code += f"  assert false;\n"

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
    if smoke_test:
        test_harness_code += f"  assert false;\n"

    # test_harness_code += f"  //var expectedOutput := {ret_param};\n"
    test_harness_code += f"}}"
    print("\n------------\nTest Harness Code:\n-----------------")
    print(test_harness_code)
    # create an output file wiht dafny_file_path + "_test_harness.dfy"
    with open(dafny_file_path + "_test_harness.dfy", "w") as file:
        file.write(test_harness_code)

    # invoke dafny on the test harness file with argument "verify"
    result = subprocess.run([dafny_binary_path,  "verify", "--allow-warnings", dafny_file_path + "_test_harness.dfy"], capture_output=True, text=True)
    print(result.stdout)
    print(result.stderr)

    return (test_harness_code, result.stdout, result.stderr)

# add command line arguments to the harness
import argparse
import sys
import json

def init_parser():
    parser = argparse.ArgumentParser(description='Check a Dafny specification for test-set correctness and completeness for user-intent formalization.')
    parser.add_argument('--file', type=str, help='path to the dafny file without the .dfy extension')
    # add a flag to mutate the inputs and number of mutations (default 5)
    parser.add_argument('--mutate', type=int, default=5, help='number of mutations to apply to each test')
    # log file to store the results
    parser.add_argument('--log', type=str, help='path to the log file')
    # path to Dafny binary
    parser.add_argument('--dafny-binary', type=str, default="/home/shuvendu/dafny/Scripts/dafny",  help='path to the Dafny binary')
    # smoke test to check if assert false is reachable
    parser.add_argument('--smoke-test', action='store_true', help='smoke test to check if assert false is reachable')
    # a json file with user labels
    parser.add_argument('--user-labels-json', type=str, help='path to the json file with user labels')
    return parser

# Example usage
if __name__ == "__main__":
    parser = init_parser()
    args = parser.parse_args()
    log_file = args.log
    if args.dafny_binary:
        print (f"Using Dafny binary: {args.dafny_binary}")
        dafny_binary_path = args.dafny_binary

    # redirect the output to the log file
    if log_file:
        print (f"Redirecting output to {log_file}")
        sys.stdout = open('speceval_output/' + log_file, 'w')

    # set the mutation flag
    max_mutations = args.mutate

    # smoke test to check if assert false is reachable
    if args.smoke_test:
        smoke_test = True

    if args.user_labels_json:
        # read the json file
        with open(args.user_labels_json, "r") as file:
            user_labels_json = json.load(file)
            
    # if the file argument is provided, then process the file
    if args.file:
        # ensure that the file exists and has no extension
        assert os.path.exists(args.file+".dfy"), "File does not exist"
        process_example(args.file)    
    else:
        # hardcode some examples for testing
        process_example("examples/task_id_2") # first 
        process_example("examples/task_id_2_fixed") # <==> instead of ==>
        process_example("examples/task_id_101") # assert for expected output
        process_example("examples/task_id_594") # //assert for expected output
        process_example("examples/task_id_433") # truea/falsea
        process_example("examples/task_id_447") # truea/falsea
        process_example("examples/task_id_105") # recursive function needs {: fuel} annotation. DOES NOT WORK yet




