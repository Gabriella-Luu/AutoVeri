# AutoVeri-Python

AutoVeri-Python 是一个面向 Python 函数自动验证的原型工具链。系统以自然语言任务描述和待验证的 Python 函数为输入，自动完成以下工作：

- 生成翻译测试用例；
- 将 Python 函数翻译为 Dafny 程序；
- 为 Dafny 方法生成前置条件与后置条件规约；
- 为包含循环的 Dafny 程序生成循环不变式；
- 调用 Dafny 验证器执行静态验证与规约评测。

本仓库包含工具代码、技术文档和使用说明，可直接作为论文附带实现的公开仓库基础版本。

## 1. 项目定位

AutoVeri-Python 关注的是“从 Python 实现到 Dafny 可验证程序”的自动化流程。当前实现将整个流程拆分为四个核心阶段：

1. `Python -> Dafny` 代码翻译；
2. Dafny 规约生成；
3. 循环不变式生成；
4. Dafny 验证与规约评测。

与论文中的理想流程保持一致，当前实现已经区分两类测试数据：

- **翻译测试用例**：用于检查翻译得到的 Dafny 程序是否与原 Python 实现语义一致；
- **规约测试用例**：由用户显式提供，用于评测生成规约是否符合需求，而不是是否贴合当前 Python 实现的行为。

## 2. 仓库结构

```text
AutoVeri-Python/
├── execute.py                       # 主流程调度脚本
├── env.config.example               # 配置模板
├── common/
│   ├── __init__.py
│   └── testcase_utils.py            # 公共测试样例工具
├── input/
│   ├── input.json                   # 用户输入：任务、Python 代码、规约测试样例
├── output/                          # 运行输出目录（仓库中仅保留 .gitkeep）
├── python2dafny/
│   ├── test_gen.py                  # 翻译阶段测试用例生成
│   └── fix_gen.py                   # Python -> Dafny 翻译与修复
├── spec_generation/
│   ├── specs_gen.py                 # 规约生成
│   ├── invariants_gen.py            # 循环不变式生成
│   └── prompts/
│       └── SPECS_GEN_TEMPLATE.file  # 规约生成模板
├── spec_evaluation/
│   └── eval_dafny_spec.py           # 规约评测与变异测试
└── docs/
    ├── TECHNICAL.md                 # 技术文档
    └── USAGE.md                     # 使用说明
```

## 3. 运行环境

建议环境：

- Python 3.11 及以上；
- Dafny 已正确安装并可通过命令行调用；
- 已安装 `openai` Python SDK；
- 能够访问所配置的大模型服务。

安装依赖：

```bash
pip install -r requirements.txt
```

## 4. 快速开始

### 4.1 配置模型与路径

先复制配置模板：

```bash
cp env.config.example env.config
```

然后根据你的模型服务地址与密钥填写 `env.config`。

### 4.2 准备输入

编辑 `input/input.json`，至少包含三个字段：

- `task`：自然语言任务描述；
- `python_code`：待验证的 Python 函数；
- `spec_test_cases`：用户提供的规约评测样例。

### 4.3 执行主流程

```bash
python execute.py
```

运行后会在 `output/` 中生成中间文件与结果文件。

## 5. 输入与输出

### 输入文件

`input/input.json` 当前采用如下结构：

```json
{
  "task": "Write a function to multiply two integers without using the * operator in python.",
  "python_code": "def multiply_int(x, y): ...",
  "spec_test_cases": [
    {
      "inputs": [2, 3],
      "expected_output": 6
    }
  ]
}
```

### 主要输出文件

- `output/translation_test_cases.json`：翻译阶段测试用例；
- `output/trans.dfy`：翻译得到的 Dafny 程序；
- `output/trans_with_inv.dfy`：插入循环不变式后的程序（若存在循环）；
- `output/specgen.dfy`：生成的 Dafny 规约；
- `output/eval.txt`：规约评测输出。

## 6. 文档导航

- 技术文档：`docs/TECHNICAL.md`
- 使用说明：`docs/USAGE.md`

## 7. 发布说明

当前仓库已经整理为适合公开发布的结构：

- 不再跟踪本地 `env.config`，仅保留 `env.config.example`；
- 不再跟踪 `input/specgen_input.json` 和 `output/` 中的运行产物；
- 不再跟踪 `venv/`、`__pycache__/`、`.DS_Store` 和本地 IDE 配置；
- 当前版本不包含任何 `DeepSeek` 相关配置与代码分支。
