import os
import shutil
import traceback
from openai import OpenAI
# from time import sleep
import configparser
from .services import utils as utility
from .services import dafny_verifyer as verifier

# os.environ["http_proxy"] = "http://127.0.0.1:7890"
# os.environ["https_proxy"] = "http://127.0.0.1:7890"

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
    api_config["model"] = config.get('SPECSGEN', 'model')
    api_config["temp"] = float(config.get('SPECSGEN', 'temp'))

    env_config = dict()
    env_config["K_run"] = config.get('SPECSGEN', 'K_run')
    env_config["cool_down_time"] = config.get('SPECSGEN', 'cool_down_time')
    env_config["data_path"] = config.get('SPECSGEN', 'data_path')
    env_config["base_output_path"] = config.get('SPECSGEN', 'base_output_path')
    return api_config, env_config


def get_output_paths(_task, _temp, _K, _model, _base_path):
    out_paths = dict()
    common_path = "specs_" + "-" + _model + "-" + "temp_" + str(
        _temp) + "-" + "k_" + str(_K)
    
    if os.path.exists(_base_path):
        shutil.rmtree(_base_path)
    os.makedirs(_base_path, exist_ok=True)
        
    # out_paths["saved_path"] = os.path.join(_base_path, common_path + ".json")
    out_paths["dfy_src_path"] = os.path.join(_base_path, common_path + ".dfy")
    out_paths["verification_path"] = os.path.join(_base_path, common_path + "_verification_log.txt")
    return out_paths


def get_specs_gen_prompt_template(_task):
    script_dir_path = "/Users/luyihan/Desktop/AutoVeri/spec_generation"
    prompt_path = os.path.join(script_dir_path, 'prompts/SPECS_GEN_TEMPLATE.file')
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
    print(response.choices[0].message.content)
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
    for run_count in range(1, int(_env_config["K_run"]) + 1):
        output_paths = get_output_paths(_task=task, _temp=_api_config["temp"], _K=run_count,
                                        _model=model,
                                        _base_path=_env_config["base_output_path"])
        try:
            response = ""
            if ("gpt" in model): 
                response = invoke_llm(model=model, messages=messages, _temp=_api_config['temp'], _key=_api_config['openai_api_key'], _base=_api_config['openai_base_url'])
            if ("deepseek" in model):
                response = invoke_llm(model=model, messages=messages, _temp=_api_config['temp'], _key=_api_config['deepseek_api_key'], _base=_api_config['deepseek_base_url'])
            
            # code = verifier.parse_code(response)
            utility.write_to_file(response, output_paths["dfy_src_path"])
        except Exception as e:
            traceback.print_exc() 
            print("Error while processing => " + "in temperature =>" + str(
                _api_config['temp']) + str(e))
        # sleep(int(_env_config['cool_down_time']))

def main():
    api_config, env_config = get_config()
    execute_signature_prompt(api_config, env_config)
    print("Done")

if __name__ == '__main__':
    main()
