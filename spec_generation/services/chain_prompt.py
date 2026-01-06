from langchain_openai import ChatOpenAI
import services.utils as utility
import services.prompt_template_generator as prompt_generator

from langchain_google_genai import GoogleGenerativeAI

from langchain.chains import LLMChain
from langchain.memory import ConversationBufferMemory

# import uuid
# from langchain_core.messages import HumanMessage
# from langgraph.checkpoint.memory import MemorySaver
# from langgraph.graph import START, MessagesState, StateGraph


def initialize_llm(_api_config):
    model = _api_config['model']
    if model == "gpt-4":
        return ChatOpenAI(model="gpt-4", temperature=_api_config['temp'],
                        api_key=_api_config['openai_api_key'], base_url="https://api2.aigcbest.top/v1")
    if model == "gemini":
        # gemini.configure(api_key=_api_config['google_api_key'])
        # gemini_model = gemini.GenerativeModel(
        #     'gemini-1.5-pro',
        #     generation_config=gemini.GenerationConfig(
        #         max_output_tokens=4000,
        #         temperature=_api_config['temp'],
        #     )
        # )
        return GoogleGenerativeAI(model="gemini-1.5-flash", temperature=_api_config['temp'],
                          api_key=_api_config['google_api_key'],
                          max_tokens=4000)


def run_chain(_api_config, _env_config, new_task,
              _example_db_50_tasks,
              spec_example_selector,
              code_example_selector,
              spec_prompt_template,
              code_prompt_template):
    llm = initialize_llm(_api_config)
    temperature = _api_config['temp']
    spec_shot_count = int(_env_config["spec_shot_count"])
    code_shot_count = int(_env_config["code_shot_count"])

    similar_tasks = spec_example_selector.select_examples(new_task)
    spec_examples_ids = [t['task_id'] for t in similar_tasks]

    specification_prompt = prompt_generator.create_few_shot_specification_prompts(spec_examples_ids,
                                                                                  _example_db_50_tasks,
                                                                                  spec_prompt_template)
    
    # # Define a new graph workflow
    # workflow = StateGraph(state_schema=MessagesState)
    # # Define the function that calls the model
    # def call_model(state: MessagesState):
    #     response = llm.invoke(state["messages"])
    #     print(response)
    #     return {"messages": response}

    # # Define the nodes and edges
    # workflow.add_edge(START, "llm")
    # workflow.add_node("llm", call_model)

    # # Adding memory
    # memory = MemorySaver()
    # app = workflow.compile(checkpointer=memory)

    # # Generate a unique thread ID
    # thread_id = uuid.uuid4()
    # config = {"configurable": {"thread_id": thread_id}}

    # # Specification Prompt Response
    # input_message = HumanMessage(content=str(specification_prompt))
    # for event in app.stream({"messages": [input_message]}, config, stream_mode="values"):
    #     specification_response = event["messages"][-1].content

    # next_input_task_with_spec = utility.parse_specification_response(new_task, specification_response)

    # spec_similar_code_tasks = code_example_selector.select_examples(next_input_task_with_spec)
    # code_examples_ids = [t['task_id'] for t in spec_similar_code_tasks]

    # code_prompt = prompt_generator.create_few_shot_code_prompts(code_examples_ids, _example_db_50_tasks,
    #                                                             code_prompt_template)
    # # print(code_prompt.get_prompts())

    # # Dynamic Few-Shot Prompt Response
    # input_message = HumanMessage(content=str(code_prompt))
    # for event in app.stream({"messages": [input_message]}, config, stream_mode="values"):
    #     code_response = event["messages"][-1].content






    # print(specification_prompt)
    # Memory
    specification_memory = ConversationBufferMemory(input_key='task', memory_key='chat_history')
    specification_chain = LLMChain(llm=llm, prompt=specification_prompt, verbose=False, output_key='specifications',
                                   memory=specification_memory)

    # Specification Prompt Response
    specification_response = specification_chain.run(new_task['task_description'])

    next_input_task_with_spec = utility.parse_specification_response(new_task, specification_response)

    spec_similar_code_tasks = code_example_selector.select_examples(next_input_task_with_spec)
    code_examples_ids = [t['task_id'] for t in spec_similar_code_tasks]
    
    code_prompt = prompt_generator.create_few_shot_code_prompts(code_examples_ids, _example_db_50_tasks,
                                                                code_prompt_template)

    code_memory = ConversationBufferMemory(input_key='task', memory_key='chat_history')
    code_chain = LLMChain(llm=llm, prompt=code_prompt, verbose=False, output_key='script', memory=code_memory)

    # Dynamic Few-Shot Prompt Response
    code_response = code_chain.run(new_task['task_description'])





    saved_map = {
        "temperature": temperature,
        "spec_example_shots": spec_shot_count,
        "code_example_shots": code_shot_count,
        "spec_examples_ids": spec_examples_ids,
        "specification_response": specification_response,
        "code_examples_ids": code_examples_ids,
        "code_response": code_response
    }
    return saved_map
