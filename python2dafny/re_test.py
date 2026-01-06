import json

with open("log.json", "r", encoding="utf-8") as JSON:
    origin = json.load(JSON)
with open("log2.json", "r", encoding="utf-8") as JSON:
    test_result = json.load(JSON)
with open("test2.json", "r", encoding="utf-8") as JSON:
    testcase = json.load(JSON)
for task_id in origin:
    assert task_id in test_result
    if testcase[task_id]["TestCase"] == "" or origin[task_id]["status"] != test_result[task_id]["status"] :
        differ_type = "?"
        if testcase[task_id]["TestCase"] == "":
            differ_type = "Empty_Testcase"
        if origin[task_id]["status"]!= "syntax_error" and test_result[task_id]["status"] == "syntax_error":
            differ_type = "Syntax_Error_Testcase"
        print(task_id, differ_type, origin[task_id]["status"], test_result[task_id]["status"])
