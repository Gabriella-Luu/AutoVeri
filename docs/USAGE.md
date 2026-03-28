# AutoVeri-Python 使用说明

## 1. 文档目的

本文档提供 AutoVeri-Python 的实际使用方法，面向以下场景：

- 本地运行工具；
- 准备实验输入；
- 理解输出文件；
- 排查常见问题。

如果你希望先快速了解项目背景，请先阅读 `README.md`；  
如果你需要了解模块设计与实现细节，请阅读 `docs/TECHNICAL.md`。

---

## 2. 环境准备

### 2.1 基础环境

建议使用以下环境：

- Python 3.11 及以上；
- Dafny 已安装并加入命令行环境；
- 可访问所配置的大模型服务；
- 已安装 `openai` Python SDK。

### 2.2 创建虚拟环境

推荐做法：

```bash
python3 -m venv .venv
source .venv/bin/activate
pip install --upgrade pip
pip install -r requirements.txt
```

如果你的环境中已经安装好 Dafny，请确保以下命令可用：

```bash
dafny --version
```

---

## 3. 配置文件

### 3.1 生成本地配置

先复制模板文件：

```bash
cp env.config.example env.config
```

然后根据你的服务信息填写真实配置。

### 3.2 配置项说明

`env.config` 主要包括以下内容：

#### `[DEFAULT]`

- `openai_api_key`
- `openai_base_url`

#### `[TRANS]`

- `model`
- `temp`
- `max_fixing_iterations`
- `max_test_gen_iterations`
- `test_cases_num`

#### `[SPECSGEN]`

- `model`
- `temp`
- `max_specgen_num`

#### `[INVSGEN]`

- `model`
- `temp`
- `max_invgen_num`

### 3.3 配置建议

- 如果翻译、规约生成与循环不变式生成都使用同一个模型，可以在多个分区中设置相同模型名；
- 如果模型提供方不同，请分别填写对应的 `base_url` 与 `api_key`；
- `temp` 建议保持较低值，以减少输出波动；
- `max_*_num` 与 `max_fixing_iterations` 控制重试次数。

---

## 4. 输入准备

### 4.1 输入文件位置

主输入文件为：

`input/input.json`

### 4.2 输入结构

当前推荐格式如下：

```json
{
  "task": "Write a function to multiply two integers without using the * operator in python.",
  "python_code": "def multiply_int(x, y):\n    if y < 0:\n        return -multiply_int(x, -y)\n    elif y == 0:\n        return 0\n    elif y == 1:\n        return x\n    else:\n        return x + multiply_int(x, y - 1)",
  "spec_test_cases": [
    {
      "inputs": [2, 3],
      "expected_output": 6
    },
    {
      "inputs": [3, -2],
      "expected_output": -6
    }
  ]
}
```

### 4.3 字段含义

- `task`
  - 自然语言任务描述；
  - 推荐明确说明目标函数语义和限制条件。

- `python_code`
  - 待验证的 Python 代码；
  - 当前工具默认围绕单个主函数工作；
  - 建议将相关辅助函数与主函数一并放在同一段代码中。

- `spec_test_cases`
  - 用户提供的规约测试样例；
  - 当前是规约生成与规约评测阶段的权威样例来源。

### 4.4 `spec_test_cases` 编写规则

每个测试样例都应包含：

- `inputs`
  - 按 Python 函数参数顺序给出；
  - 必须是 JSON 数组。

- `expected_output`
  - 单返回值时可直接写标量；
  - 多返回值时建议写数组；
  - 应表达“需求层面正确输出”，而不是“程序当前运行结果”。

例如：

- 单返回值：

```json
{ "inputs": [2, 3], "expected_output": 6 }
```

- 多返回值：

```json
{ "inputs": [[1, 2, 3]], "expected_output": [1, 3, 6] }
```

---

## 5. 运行方式

## 5.1 运行完整主流程

在仓库根目录执行：

```bash
python execute.py
```

主流程会依次执行：

1. 翻译测试用例生成；
2. Python 到 Dafny 翻译；
3. 循环不变式生成；
4. 规约生成；
5. 规约评测。

### 5.2 分阶段运行

如果你需要单独调试某个阶段，可以直接运行对应脚本。

#### 生成翻译测试用例

```bash
python python2dafny/test_gen.py
```

#### 执行 Python -> Dafny 翻译

```bash
python python2dafny/fix_gen.py
```

#### 生成循环不变式

```bash
python -m spec_generation.invariants_gen
```

#### 生成规约

```bash
python -m spec_generation.specs_gen
```

#### 执行规约评测

```bash
python -m spec_evaluation.eval_dafny_spec
```

---

## 6. 输出说明

### 6.1 中间文件

- `input/specgen_input.json`
  - 规约生成阶段的中间输入；
  - 包含 `task_description`、`method_signature` 和 `behavior_examples`。

- `output/translation_test_cases.json`
  - 翻译阶段生成的 Dafny 测试方法；
  - 只用于翻译阶段，不用于规约评测。

### 6.2 核心输出

- `output/trans.dfy`
  - 翻译后的 Dafny 程序。

- `output/trans_with_inv.dfy`
  - 带循环不变式的 Dafny 程序。

- `output/specgen.dfy`
  - 生成的规约代码。

- `output/eval.txt`
  - 规约评测输出；
  - 包含平均正确性与平均完备性统计结果。

---

## 7. 推荐使用流程

对于论文实验或案例复现，建议采用如下流程：

1. 配置好 `env.config`；
2. 准备 `input/input.json`；
3. 明确写出高质量的 `spec_test_cases`；
4. 运行 `python execute.py`；
5. 检查：
   - `output/trans.dfy`
   - `output/specgen.dfy`
   - `output/eval.txt`
6. 如需分析中间结果，再查看 `input/specgen_input.json` 与翻译测试文件。

---

## 8. 常见问题

### 8.1 找不到 `env.config`

现象：

- 启动脚本后提示 `env.config not found!!`

处理方式：

- 确认当前工作目录是仓库根目录；
- 确认已从 `env.config.example` 复制出本地 `env.config`。

### 8.2 模型调用失败

常见原因：

- API Key 错误；
- `base_url` 配置错误；
- 网络不可达；
- 模型名称拼写错误。

建议检查：

- `env.config`
- 网络环境
- 模型服务提供方的接口兼容性

### 8.3 `dafny` 命令不可用

现象：

- 脚本在运行 `dafny test` 或 `dafny verify` 时失败

处理方式：

- 确认 Dafny 已正确安装；
- 确认 `dafny` 可在终端直接执行；
- 检查 PATH 环境变量。

### 8.4 规约评测结果异常

首先确认：

- `spec_test_cases` 是否表达了真实需求；
- `expected_output` 是否是需求正确输出；
- 输入数据类型是否与 Python 函数参数匹配；
- 多返回值时是否按数组形式给出输出。

## 9. 文档与代码的对应关系

- 项目总览：`README.md`
- 技术细节：`docs/TECHNICAL.md`
- 主调度脚本：`execute.py`
- 翻译测试生成：`python2dafny/test_gen.py`
- Python 到 Dafny 翻译：`python2dafny/fix_gen.py`
- 规约生成：`spec_generation/specs_gen.py`
- 规约评测：`spec_evaluation/eval_dafny_spec.py`
