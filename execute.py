import argparse
import configparser
import os
import json
import re

import python2dafny.fix_gen as trans
import python2dafny.test_gen as testgen
from spec_generation import specs_gen as specgen

parser = argparse.ArgumentParser(description='Evaluate Python Code')

def get_config():
    script_dir_path = "/Users/luyihan/Desktop/AutoVeri/"
    config_path = os.path.join(script_dir_path, 'env.config')
    if not (os.path.exists(config_path)):
        print("env.config not found!!")
        return
    config = configparser.ConfigParser()
    config.read(config_path)

    env_config = dict()
    env_config["input_path"] = config.get('DEFAULT', 'input_json_path')
    env_config["trans_output_path"] = config.get('TRANS', 'translation_path')
    env_config["trans_output_file_name"] = config.get('TRANS', 'trans_output_file_name')
    env_config["specgen_input_file_path"] = config.get('SPECSGEN', 'data_path')

    return env_config

def get_input(env_config):
    with open(env_config["input_path"], "r", encoding="utf-8") as CODE_JSON:
        problems = json.load(CODE_JSON)

    task = problems["task"]
    python_code = problems["python_code"]
    # test_cases = problems["test_cases"]

    return task, python_code

def get_dafny_code(env_config):
    file = env_config["trans_output_path"] + '/' + env_config["trans_output_file_name"]
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
    

if __name__ == '__main__':
    args = parser.parse_args()
    env_config = get_config()
    # get testcases
    testgen.main()
    # python2dafny
    trans.main()
    # specgen
    specgen_input_process(env_config)
    specgen.main()
