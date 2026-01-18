import os
import shutil
import traceback
from openai import OpenAI
# from time import sleep
import configparser
from .services import utils as utility
from .services import dafny_verifyer as verifier
import re
import subprocess
import sys

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
    api_config["model"] = config.get('INVSGEN', 'model')
    api_config["temp"] = float(config.get('INVSGEN', 'temp'))

    env_config = dict()
    env_config["max_invgen_num"] = int(config.get('INVSGEN', 'max_invgen_num'))
    return api_config, env_config

base_prompt = """
You are an expert in the Dafny programming language and formal verification. 
Your task is to insert only loop invariants into the provided Dafny code, following these specific instructions with no deviations:

Task Requirements:
- The Dafny code will be enclosed between the tags BEGIN DAFNY and END DAFNY.
- You must not modify the original code in any way. Only insert loop invariants.
- Do not provide any explanations or comments in your output. Only output the modified code with loop invariants.

Output Format:
- The loop invariants must appear immediately after the loop header and before the loop body braces {, as in:
   while i < n
     invariant 0 <= i <= n
   {
     i := i + 1;
   }
- Use Dafny's correct syntax, such as:
   -- Logical implication as ==>.
   -- Sequence length as |s| and array length as a.Length.
   -- Avoid unsupported dot operations on sequences and sets like s.Map, s.Contains, s.Min, etc.

Guidelines for Writing Invariants:
- Try to create loop invariants with a structure similar to the method post-conditions ('ensures' clause), reusing auxiliary functions or predicates mentioned in those clauses, to be incrementally enforced as the loop progresses.
- Where applicable, prefer sequence operations over quantifiers on arrays.
- Always provide explicit lower bounds in quantifiers like forall k :: 0 <= k <= n, instead of forall k :: k < n.
- Do not reference uninitialized variables or output parameters in the invariants.
- You must first understand the role of each variable in the algorithm, using any comments provided, to properly construct meaningful invariants.
- Create separate invariants for each variable manipulated within the loop, ensuring that each is well-defined.
- Do not include redundant or overly generic invariants.
- When a loop in a method modifies an array ('modifies' clause), a loop invariant should exist for each segment unchanged up to the current iteration, using old().
- 'for' loops do not need a 'decreases' clause.
- 'for' loops do not need a loop invariant for the loop index bounds.
- When 'boolean' variables are manipulated in a loop, the loop invariants should describe the conditions upon which they may be true and false (covering both cases).
- In 'for' loops, the upper bound is exclusive.
- In the case of descending for loops ('downto'), the loop iterator is implicitly decremented at the begin of the loop body (not at the end).
- The syntax function (specification) 'by method' (implementation) is valid in Dafny.
   
Failure to strictly follow these instructions will result in incorrect output.
"""

def gen(api_config, env_config):
    with open(os.path.join(os.getcwd(), 'output/trans.dfy'), 'r', encoding='utf-8') as file:
        trans = file.read()
    
    lines = base_prompt.split('\n') 
    if 'for ' not in trans:
        lines = [line for line in lines if 'for' not in line]
    if 'downto ' not in trans:
        lines = [line for line in lines if 'downto' not in line]
    if 'by method' not in trans:
        lines = [line for line in lines if 'by method' not in line]
    if 'modifies ' not in trans:
        lines = [line for line in lines if 'modifies' not in line]
    if 'boolean ' not in trans:
        lines = [line for line in lines if 'boolean' not in line]
    instructions_prompt = "\n".join(lines)

    code_prompt = "BEGIN DAFNY\n" + trans + "\nEND DAFNY\n"

    try:
        client = OpenAI(base_url=api_config["openai_base_url"],api_key=api_config["openai_api_key"])
    
        response = client.chat.completions.create(
            messages=[{"role": "system", "content": instructions_prompt}, 
                    {"role": "user", "content": code_prompt} ],
            model=api_config["model"],
            temperature=api_config["temp"],
        )
        result = response.choices[0].message.content
        if "```dafny" in result:
            # extract just the substring between "```dafny" and '```' (excluded)
            result = result[result.find("```dafny") + 8:result.rfind("```")]
        elif "```" in result:
            # extract just the substring between "```" and '```' (excluded)
            result = result[result.find("```") + 3:result.rfind("```")]
        if "BEGIN DAFNY\n" in result:
            # extract just the substring between "BEGIN DAFNY" and 'END DAFNY' (excluded)
            result = result[result.find("BEGIN DAFNY") + 12:result.rfind("END DAFNY")]
        if result.startswith("Here"):
                result = result[result.find("\n")+1:]
        
        output_lines = result.split('\n')
        # do some cleanup
        for i in range(len(output_lines)):
            line = output_lines[i]

            # Remove ';' from the end of line of lines that start with invariant
            if line.strip().startswith('invariant ') and line.strip().endswith(';'):
                output_lines[i] = line[:-1]
                line = output_lines[i]

            # Remove decreases clause
            if line.strip().startswith('decreases '):
                output_lines[i] = ""
                line = ""

            # Replace '// Inv: '  with 'invariant '
            if line.strip().startswith('// Inv: '):
                output_lines[i] = line.replace('// Inv: ', 'invariant ')
                line = output_lines[i]   

            # Replace '[..0..'  with '[..'
            if '[..0..' in line:
                output_lines[i] = line.replace('[..0..', '[..')
                line = output_lines[i]

            if line.strip() == "{}": # possibly erased body (removes to facilitate merge!)
                output_lines[i] = ""
                line = ""                 

            # solve problems of misplaced invariants
            saved_invariants = []
            saved_open_brace = []
            expecting_invariant = False
            missing_invariant = False
            for i in range(len(output_lines)):
                line = output_lines[i]
                
                if line.strip().startswith('invariant ') or line.strip().startswith('decreases '):
                    if not expecting_invariant: 
                        saved_invariants.append(line)
                        output_lines[i] = ""
                    missing_invariant = False

                elif line.strip().startswith('for ') or line.strip().startswith('while '):
                    if saved_open_brace != []:
                        output_lines[i] = saved_open_brace[0] + "\n" + line 
                        saved_open_brace = []
                        line = output_lines[i]
                    expecting_invariant = True
                    missing_invariant = True
                    if saved_invariants != []:
                    # insert the saved lines after the current one, keeping the current
                        new_line = line 
                        # add the saved lines one by one
                        for saved_line in saved_invariants:
                            new_line = new_line + "\n" + saved_line 
                        saved_invariants = []
                        output_lines[i] = new_line
                        missing_invariant = False

                elif line.strip() == "{" and missing_invariant:
                    saved_open_brace = [line] 
                    output_lines[i] = ""

                elif line.strip() != "" and not line.strip().startswith('//'):
                    if saved_open_brace != []:
                        output_lines[i] = saved_open_brace[0] + "\n" + line 
                        saved_open_brace = []
                        line = output_lines[i]
                    expecting_invariant = False
                    missing_invariant = False
                    saved_invariants = []

        with open(os.path.join(os.getcwd(), 'output/trans_with_inv.dfy'), 'w', encoding='utf-8') as file:
            file.write("\n".join(output_lines))
    except Exception as e:
        print(f"Error generating invariants: {e}")
    
def main():
    api_config, env_config = get_config()
    for i in range(env_config["max_invgen_num"]):
        gen(api_config, env_config)
        process = subprocess.run(["dafny",  "verify", "--allow-warnings", "--standard-libraries", os.path.join(os.getcwd(), "output/trans_with_inv.dfy")], capture_output=True, text=True)
        ret = process.returncode
        if ret == 0:
            print("Invariants generation finished.")
            return
    print("Invariants generation failed.")
    sys.exit()

if __name__ == '__main__':
    main()
