import json
from threading import Thread, BoundedSemaphore, Lock
import subprocess
import re

import settings


def parse_errmsg(errmsg: str) -> str:
    if "invalid UnaryExpression" in errmsg:
        return "The body of a Dafny function must be an expression, loops are not allowed; use recursions or methods instead"
    if "Expected 'to' or 'downto'" in errmsg:
        return "The for-loop format in Dafny is ```for i:= a to b```"
    return errmsg


with open(settings.source_code_json_path, "r", encoding="utf-8") as JSON:
    problems = json.load(JSON)
with open(settings.dafny_code_path, "r", encoding="utf-8") as JSON:
    paths = json.load(JSON)
with open(settings.test_set_json_path, "r", encoding="utf-8") as JSON:
    testset = json.load(JSON)

log = {problem: {} for problem in problems}


def eval(
    task_id: str,
    CodePath: str,
    TestCase: str,
):
    try:
        test_threads.acquire()
        with open(CodePath, "r", encoding="utf-8") as DafnyFile:
            Code = DafnyFile.read()
        TestCode = Code + "\n" + TestCase
        with open(CodePath, "w", encoding="utf-8") as DafnyFile:
            print(TestCode, file=DafnyFile, flush=True)
        process = subprocess.run(
            "dafny test %s --no-verify --standard-libraries" % CodePath,
            timeout=settings.timeout_per_task,
        )
        process = subprocess.run(
            "dafny test %s --no-verify --standard-libraries" % CodePath,
            timeout=settings.timeout_per_task,
            capture_output=True,
        )
        ret = process.returncode
    except subprocess.TimeoutExpired:
        ret = -1
    test_threads.release()
    with open(CodePath, "w", encoding="utf-8") as DafnyFile:
        print(Code, file=DafnyFile, flush=True)

    print("code tested:problem %s" % (task_id), flush=True)
    global syntax_passed_list, test_passed_list
    result_counter.acquire_lock()
    match ret:
        case 0:
            log[task_id] = {"status": "passed"}
            syntax_passed_list.add(task_id)
            test_passed_list.add(task_id)
        case 2:
            log[task_id] = {"status": "syntax_error", "error_messages": []}
            try:
                buggy_code = TestCode.split("\n")
                report = str(process.stdout).replace("\\r", "")
                messages = re.findall(r".dfy(.+?)\\n", report)
                regex_messages = [
                    re.match(
                        r"\((?P<lineno>\d+),(?P<pos>\d+)\): (?P<error>.+)",
                        message,
                    )
                    for message in messages
                ]
                log[task_id]["error_messages"] = [
                    {
                        "content": buggy_code[int(regex_message.group("lineno"))],
                        "line": int(regex_message.group("lineno")),
                        "position": int(regex_message.group("pos")),
                        "error_type": parse_errmsg(regex_message.group("error")),
                    }
                    for regex_message in regex_messages
                ]
            except:
                pass
        case -1:
            log[task_id] = {"status": "timeout"}
            syntax_passed_list.add(task_id)
            timeout_list.add(task_id)
        case 3:
            log[task_id] = {"status": "semantic_error", "error_messages": []}
            try:
                buggy_code = TestCode.split("\n")
                report = str(process.stdout).replace("\\r", "")
                messages = re.findall(r".dfy(.+?)\\n", report)
                regex_messages = [
                    re.match(
                        r"\((?P<lineno>\d+),(?P<pos>\d+)\): (?P<error>.+)",
                        message,
                    )
                    for message in messages
                ]
                assign_or_expect = buggy_code[int(regex_messages[0].group("lineno"))]
                caller = None
                value = None
                expect = None
                if assign_or_expect.find(":=") != -1:
                    caller = assign_or_expect[
                        assign_or_expect.find("call") : assign_or_expect.find(":=")
                    ]
                    value = assign_or_expect[
                        assign_or_expect.find(":=") + 2 : assign_or_expect.find(";")
                    ]
                if assign_or_expect.find("==") != -1:
                    caller = assign_or_expect[
                        assign_or_expect.find("call") : assign_or_expect.find("==")
                    ]
                    expect = assign_or_expect[
                        assign_or_expect.find("==") + 2 : assign_or_expect.find(";")
                    ]
                assert caller != None
                if value == None:
                    for line in buggy_code:
                        if line.find(caller) != -1 and line.find(":=") != -1:
                            value = line[line.find(":=") + 2 : line.find(";")]
                            break
                    assert value != None
                if expect == None:
                    for line in buggy_code:
                        if line.find(caller) != -1 and line.find("==") != -1:
                            expect = line[line.find("==") + 2 : line.find(";")]
                            break
                    assert expect != None
                log[task_id]["error_messages"] = [
                    {
                        "content": "expect %s==%s;" % (value, expect),
                        "line": int(regex_messages[0].group("lineno")),
                        "position": int(regex_messages[0].group("pos")),
                    }
                ]
            except:
                pass
            syntax_passed_list.add(task_id)
            test_failed_list.add(task_id)
    result_counter.release_lock()


test_threads = BoundedSemaphore(value=settings.max_test_threads)

syntax_passed_list = set()
test_passed_list = set()
timeout_list = set()
test_failed_list = set()
result_counter = Lock()

threads = []

for task_id in problems:
    thread = Thread(
        target=eval,
        args=(
            task_id,
            paths[task_id]["CodePath"],
            testset[task_id]["TestCase"],
        ),
    )
    threads.append(thread)
    thread.start()

for thread in threads:
    thread.join()

print(
    "%d/%d(%.2f%%) tasks passed syntax test"
    % (
        len(syntax_passed_list),
        len(problems),
        len(syntax_passed_list) * 100.0 / len(problems),
    )
)
print(
    "%d/%d(%.2f%%) tasks timeout"
    % (len(timeout_list), len(problems), len(timeout_list) * 100.0 / len(problems))
)
print(
    "%d/%d(%.2f%%) tasks failed semantic test"
    % (
        len(test_failed_list),
        len(problems),
        len(test_failed_list) * 100.0 / len(problems),
    )
)
print(
    "%d/%d(%.2f%%) tasks passed semantic test"
    % (
        len(test_passed_list),
        len(problems),
        len(test_passed_list) * 100.0 / len(problems),
    )
)

with open(settings.log_json_path, "w", encoding="utf-8") as JSON:
    json.dump(log, JSON)
