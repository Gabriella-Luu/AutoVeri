import argparse
import configparser
import os
import json
import re
import shutil
import subprocess

import python2dafny.fix_gen as trans
import python2dafny.test_gen as testgen
from spec_generation import specs_gen as specgen
from spec_generation import invariants_gen as invgen
from spec_evaluation import eval_dafny_spec as speceval

parser = argparse.ArgumentParser(description='Evaluate Python Code')

def get_config():
    script_dir_path = os.getcwd()
    config_path = os.path.join(script_dir_path, 'env.config')
    if not (os.path.exists(config_path)):
        print("env.config not found!!")
        return
    config = configparser.ConfigParser()
    config.read(config_path)

    env_config = dict()
    env_config["input_path"] = os.path.join(os.getcwd(), "input/input.json")
    env_config["trans_output_path"] = os.path.join(os.getcwd(), "output")
    env_config["specs_output_path"] = os.path.join(os.getcwd(), "output")
    env_config["specgen_input_file_path"] = os.path.join(os.getcwd(), "input/specgen_input.json")
    env_config["max_specgen_num"] = int(config.get('SPECSGEN', 'max_specgen_num'))
    return env_config

def get_input(env_config):
    with open(env_config["input_path"], "r", encoding="utf-8") as CODE_JSON:
        problems = json.load(CODE_JSON)

    task = problems["task"]
    python_code = problems["python_code"]
    # test_cases = problems["test_cases"]

    return task, python_code

def get_dafny_code(env_config):
    file = env_config["trans_output_path"] + '/' + "trans.dfy"
    with open(file) as reader:
        return reader.read()

def specgen_input_process(env_config):
    task, python_code = get_input(env_config)
    task_description = task.replace("python", "dafny").replace("function", "method")

    pattern = r'def\s+(\w+)\s*\('
    match = re.search(pattern, python_code, re.MULTILINE)
    if match:
        method_name = match.group(1)

    dafny_code = get_dafny_code(env_config)
    for line in dafny_code.split('\n'):
        if 'method' in line and method_name in line:
            method_signature = line.strip()
            break

    content = {
       "task_description": task_description,
       "method_signature": method_signature
    }
    content = json.dumps(content)

    with open(env_config["specgen_input_file_path"], 'w') as file:
        file.write(content)
    
def isSpecsCorrect():
    file_path = os.path.join(os.getcwd(), "output/eval.txt")
    with open(file_path, 'r') as file:
        content = file.read()
    if "Error processing file" in content:
        return False
    avg_correctness = re.search(r'Average Correctness: ([\d.]+)', content).group(1)
    if avg_correctness != '1.0':
        return False
    avg_completeness = re.search(r'Average Completeness: ([\d.]+)', content).group(1)
    if avg_completeness != '1.0':
        return False
    return True

def combine(trans_path, specs_path):
    with open(trans_path, "r") as file:
        trans_lines = file.readlines()
    with open(specs_path, "r") as file:
        specs_lines = file.readlines()
    
    filtered_specs_lines = [s for s in specs_lines if any(keyword in s for keyword in ["requires", "ensures", "modifies"])]

    pattern = r'method\s+(\w+)\s*\('
    match = re.search(pattern, specs_lines[0], re.MULTILINE)
    if match:
        method_name = match.group(1)
    for i, s in enumerate(trans_lines):
        if "method " + method_name in s:
            position = i + 1
            break

    lines = trans_lines[:position] + filtered_specs_lines + trans_lines[position:]

    code = "".join(lines)
    with open(os.path.join(env_config["trans_output_path"], "final_program.dfy"), "w", encoding="utf-8") as DafnyFile:
        print(code, file=DafnyFile, flush=True)

def isLoopFound():
    with open(os.path.join(env_config["trans_output_path"], "trans.dfy"), "r") as file:
        content = file.read()
    if " for " in content or " while " in content:
        return True
    return False

if __name__ == '__main__':
    args = parser.parse_args()
    env_config = get_config()

    # step=0:testcases gen; step=1:python2dafny step=2:invgen; step=3:specgen
    step = 2
    isLoop = False

    if step == 0:
        # clean
        output_path = os.path.join(os.getcwd(), "output")
        if os.path.exists(output_path):
            shutil.rmtree(output_path)  # 删除整个文件夹
            os.makedirs(output_path)    # 重新创建空文件夹

        # testcases gen
        testgen.main()

    if step <= 1:
        # python2dafny
        trans.main()

    if step <= 2:
        # invariants gen
        loop_flag = isLoopFound()
        if loop_flag:
            invgen.main()

    if step <= 3:
        # specs gen & eval
        specgen_input_process(env_config)
        eval_res = False
        for i in range(env_config["max_specgen_num"]):
            speceval.main()
            eval_res = isSpecsCorrect()
            if eval_res:
                break
            specgen.main()

        if eval_res:
            print("Specs generation finished.")
        else:
            print("Specs generation failed.")

        # combine code and specs
        if loop_flag:
            combine(os.path.join(env_config["trans_output_path"], "trans_with_inv.dfy"), os.path.join(env_config["specs_output_path"], "specgen.dfy"))
        else:
            combine(os.path.join(env_config["trans_output_path"], "trans.dfy"), os.path.join(env_config["specs_output_path"], "specgen.dfy"))

        # verify final program
        print("-----------------Verification Start-----------------")
        result = subprocess.run(["dafny",  "verify", "--allow-warnings", "--standard-libraries", os.path.join(env_config["trans_output_path"], "final_program.dfy")], capture_output=True, text=True)
        print(result.stdout)
        print(result.stderr)
        print("-------------------------End-------------------------")
