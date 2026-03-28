import json


def normalize_to_output_list(value):
    if isinstance(value, tuple):
        return list(value)
    if isinstance(value, list):
        return value
    return [value]


def normalize_spec_test_cases(raw_cases):
    normalized_cases = []
    for case in raw_cases:
        inputs = case.get("inputs", [])
        if not isinstance(inputs, list):
            raise ValueError("spec_test_cases.inputs must be a list")
        normalized_cases.append(
            {
                "inputs": inputs,
                "outputs": normalize_to_output_list(case.get("expected_output")),
            }
        )
    return normalized_cases


def python_value_to_dafny_literal(value):
    if isinstance(value, bool):
        return "true" if value else "false"
    if value is None:
        return "null"
    if isinstance(value, str):
        return json.dumps(value)
    if isinstance(value, (int, float)):
        return repr(value)
    if isinstance(value, list):
        return "[" + ", ".join(python_value_to_dafny_literal(item) for item in value) + "]"
    if isinstance(value, tuple):
        return "(" + ", ".join(python_value_to_dafny_literal(item) for item in value) + ")"
    if isinstance(value, dict):
        entries = []
        for key, item in value.items():
            entries.append(
                f"{python_value_to_dafny_literal(key)} := {python_value_to_dafny_literal(item)}"
            )
        return "map[" + ", ".join(entries) + "]"
    raise TypeError(f"Unsupported testcase value: {type(value).__name__}")


def format_behavior_examples(raw_cases):
    normalized_cases = normalize_spec_test_cases(raw_cases)
    lines = []
    for index, case in enumerate(normalized_cases, start=1):
        inputs = ", ".join(repr(item) for item in case["inputs"])
        outputs = case["outputs"]
        expected_output = outputs[0] if len(outputs) == 1 else tuple(outputs)
        lines.append(
            f"{index}. inputs = ({inputs}), expected_output = {repr(expected_output)}"
        )
    return "\n".join(lines)
