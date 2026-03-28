import os
import shutil
import traceback
from openai import OpenAI
# from time import sleep
import configparser
from .services import utils as utility
from .services import dafny_verifyer as verifier
import re
from common.testcase_utils import format_behavior_examples

# os.environ["http_proxy"] = "http://127.0.0.1:7890"
# os.environ["https_proxy"] = "http://127.0.0.1:7890"

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
    api_config["model"] = config.get('SPECSGEN', 'model')
    api_config["temp"] = float(config.get('SPECSGEN', 'temp'))

    env_config = dict()
    env_config["data_path"] = os.path.join(os.getcwd(), "input/specgen_input.json")
    env_config["base_output_path"] = os.path.join(os.getcwd(), "output")
    return api_config, env_config

def get_specs_gen_prompt_template(_task):
    script_dir_path = os.getcwd()
    prompt_path = os.path.join(script_dir_path, 'spec_generation/prompts/SPECS_GEN_TEMPLATE.file')
    if not (os.path.exists(prompt_path)):
        print("prompts/SPECS_GEN_TEMPLATE.file not found!!")
        return
    template = utility.read_file(prompt_path)
    # _clean_code = utility.read_file(code_path)
    final_prompt = template.format(task_description=_task['task_description'],
                                   method_signature=_task['method_signature'])
    # print(final_prompt)
    return final_prompt


def invoke_llm(model, messages, _temp, _key, _base):
    client = OpenAI(
        base_url=_base,
        api_key=_key
    )
  
    response = client.chat.completions.create(
        model=model,
        messages=messages,
        temperature=_temp,
        # top_p=0.8
    )
    result = response.choices[0].message.content
    # print(response.choices[0].message.content)
    return result


def prepare_model_response(_task, _temp, _K, _res, _model, _dafny_code, _isVerified, _verification_bits):
    saved_map = {
        "K": _K,
        "temperature": _temp,
        "task_description": _task['task_description'],
        "model": _model,
        "response": _res,
        "dafny_code": _dafny_code,
        "isVerified": _isVerified,
        "verification_bits": _verification_bits
    }
    return saved_map


def execute_signature_prompt(_api_config, _env_config):
    all_response = []
    task = utility.load_json(_env_config["data_path"])
    model = _api_config['model']
    prompt_ = get_specs_gen_prompt_template(task)
    messages = [{"role": "user", "content": prompt_}]
    behavior_examples = task.get("behavior_examples", [])
    if behavior_examples:
        examples_prompt = "\n".join(
            [
                "The following behavior examples are authoritative requirement-level examples.",
                "Generate requires/ensures that are consistent with them.",
                "Do not infer expected outputs from the current Python implementation.",
                "",
                format_behavior_examples(behavior_examples),
            ]
        )
        messages.append({"role": "user", "content": examples_prompt})
   
    try:
        response = invoke_llm(
            model=model,
            messages=messages,
            _temp=_api_config['temp'],
            _key=_api_config['openai_api_key'],
            _base=_api_config['openai_base_url'],
        )
        
        specs = re.search(r'```dafny(.*?)```', response, flags=re.DOTALL).group(1).strip()
        if specs[-1] != '}':
            specs = specs + "\n{\n}"
        utility.write_to_file(specs, os.path.join(_env_config["base_output_path"], "specgen.dfy"))
    except Exception as e:
        traceback.print_exc() 
        print("Error while processing => " + "in temperature =>" + str(
            _api_config['temp']) + str(e))

def main():
    api_config, env_config = get_config()
    execute_signature_prompt(api_config, env_config)

if __name__ == '__main__':
    main()
