# AutoVeri-Python 技术文档

## 1. 文档目的

本文档面向以下读者：

- 需要理解 AutoVeri-Python 内部工作机制的研究者；
- 需要在论文中引用仓库实现细节的作者；
- 需要维护、复现或扩展该工具链的开发者。

本文档描述的是**当前仓库中的实际实现**，而不是抽象的理想流程。

---

## 2. 系统目标

AutoVeri-Python 的目标是：给定自然语言任务描述和 Python 函数实现，自动生成对应的 Dafny 程序与 Dafny 规约，并利用 Dafny 验证器与规约评测机制对结果进行分析。

系统包含以下四个核心阶段：

1. **代码翻译阶段**：将 Python 代码翻译为 Dafny 代码；
2. **规约生成阶段**：基于任务描述、方法签名和用户样例生成前置/后置条件；
3. **循环不变式生成阶段**：为含循环的 Dafny 程序生成不变式；
4. **规约评测与验证阶段**：对生成规约进行正例与变异反例评测，并调用 Dafny 执行验证。

---

## 3. 总体架构

### 3.1 模块划分

当前实现的核心模块如下：

- `execute.py`
  - 主流程调度器；
  - 负责串联翻译、规约生成、循环不变式生成与规约评测；
  - 负责生成 `input/specgen_input.json`。

- `python2dafny/test_gen.py`
  - 翻译阶段测试用例生成器；
  - 通过大模型先生成输入，再在原 Python 函数上执行得到输出；
  - 最终产出 `output/translation_test_cases.json`。

- `python2dafny/fix_gen.py`
  - Python 到 Dafny 的翻译与修复模块；
  - 调用大模型生成 Dafny 程序；
  - 用翻译测试用例检验 `trans.dfy` 是否与原 Python 实现语义一致；
  - 若失败，则根据错误信息迭代修复。

- `spec_generation/specs_gen.py`
  - Dafny 规约生成模块；
  - 使用 `SPECS_GEN_TEMPLATE.file` 作为主模板；
  - 额外接收用户行为样例，辅助模型生成与需求一致的 `requires/ensures`。

- `spec_generation/invariants_gen.py`
  - 循环不变式生成模块；
  - 对翻译得到的 Dafny 程序进行分析；
  - 若检测到循环，则尝试插入循环不变式并调用 Dafny 验证。

- `spec_evaluation/eval_dafny_spec.py`
  - 规约评测模块；
  - 直接读取用户在 `input/input.json` 中提供的 `spec_test_cases`；
  - 基于正确输出构造正例测试，并对输出做变异生成反例；
  - 通过 Dafny 验证结果统计规约的正确性与完备性。

- `common/testcase_utils.py`
  - 公共工具模块；
  - 负责：
    - 将 `spec_test_cases` 归一化为统一结构；
    - 将 Python 值转换为 Dafny 字面量；
    - 将行为样例格式化为规约生成提示文本。

---

## 4. 数据流设计

### 4.1 输入文件

#### `input/input.json`

用户主输入文件，包含：

- `task`：自然语言任务描述；
- `python_code`：待验证的 Python 函数；
- `spec_test_cases`：用户提供的规约测试样例。

其中 `spec_test_cases` 的作用非常关键：  
它是规约生成与规约评测阶段的**需求级 oracle**。

### 4.2 中间文件

#### `input/specgen_input.json`

由 `execute.py` 自动生成，用于规约生成阶段。当前字段为：

- `task_description`
- `method_signature`
- `behavior_examples`

其中 `behavior_examples` 直接来自 `input/input.json` 中的 `spec_test_cases`。

#### `output/translation_test_cases.json`

由 `python2dafny/test_gen.py` 生成，仅供翻译阶段使用。其内部包含一个 Dafny 测试方法字符串：

- `TranslationTestCase`

该文件与规约评测阶段已经解耦。

### 4.3 输出文件

常见输出包括：

- `output/trans.dfy`：翻译后的 Dafny 程序；
- `output/trans_with_inv.dfy`：插入循环不变式后的程序；
- `output/specgen.dfy`：生成的规约；
- `output/eval.txt`：规约评测结果。

---

## 5. 工作流程

### 5.1 阶段一：翻译测试生成

入口模块：`python2dafny/test_gen.py`

工作流程如下：

1. 读取 `input/input.json` 中的 `python_code`；
2. 调用大模型生成若干组 Python 输入；
3. 在原 Python 函数上执行这些输入，得到对应输出；
4. 将输入/输出对转换为 Dafny 测试方法；
5. 保存到 `output/translation_test_cases.json`。

这一步的目标是为翻译阶段提供“与 Python 实现语义一致性”检查，因此允许使用原 Python 程序作为 oracle。

### 5.2 阶段二：Python 到 Dafny 翻译

入口模块：`python2dafny/fix_gen.py`

工作流程如下：

1. 读取 `python_code` 和 `output/translation_test_cases.json`；
2. 调用大模型生成 Dafny 程序；
3. 将结果写入 `output/trans.dfy`；
4. 执行 `dafny test`；
5. 若失败，则提取错误信息并回馈给模型，执行多轮修复；
6. 直到测试通过或达到最大尝试次数。

这一阶段的验证目标是：

- 检查 Dafny 实现是否与原 Python 实现等价；
- 并不直接判断该实现是否满足自然语言需求。

### 5.3 阶段三：规约生成

入口模块：`spec_generation/specs_gen.py`

工作流程如下：

1. 读取 `input/specgen_input.json`；
2. 用 `SPECS_GEN_TEMPLATE.file` 生成主提示；
3. 将 `behavior_examples` 额外作为一条用户消息附加给模型；
4. 调用模型生成规约代码；
5. 提取代码块并保存为 `output/specgen.dfy`。

当前实现中，模板文件未被修改；行为样例是通过额外消息的形式补充给模型的。

### 5.4 阶段四：循环不变式生成

入口模块：`spec_generation/invariants_gen.py`

工作流程如下：

1. 读取翻译后的 `output/trans.dfy`；
2. 判断程序中是否包含 `for` 或 `while`；
3. 若包含循环，则调用模型生成带循环不变式的 Dafny 程序；
4. 调用 Dafny 验证其正确性；
5. 若失败，则在最大尝试次数内继续修复。

### 5.5 阶段五：规约评测

入口模块：`spec_evaluation/eval_dafny_spec.py`

这是本仓库中最需要准确理解的阶段。当前实现已经采用以下机制：

1. 读取 `output/specgen.dfy`；
2. 读取 `input/input.json` 中用户提供的 `spec_test_cases`；
3. 将样例归一化为：
   - `inputs`
   - `outputs`
4. 为每个样例构造 Dafny test harness；
5. 用原始 `expected_output` 构造正例；
6. 对 `expected_output` 做随机变异，构造反例；
7. 使用 Dafny 验证每个 harness 是否能通过；
8. 统计：
   - `Average Correctness`
   - `Average Completeness`

### 5.6 规约评测机制的关键修正

当前仓库已修正此前一个重要设计问题：

- **旧逻辑**：规约评测使用翻译阶段自动生成的测试用例，并直接把 Python 实现运行出来的输出作为 expected output；
- **新逻辑**：规约评测只使用用户提供的 `spec_test_cases`，不再把待验证 Python 实现当作规约正确性的标准。

因此现在：

- 翻译阶段仍然是“以实现为 oracle”；
- 规约阶段改为“以用户样例为 oracle”。

这一区分对于研究型仓库非常重要，因为它避免了“错误实现 + 错误规约 + 验证通过”的假阳性风险。

---

## 6. 配置项说明

配置文件：`env.config`

当前使用到的主要分区如下：

### `[DEFAULT]`

- `openai_api_key`
- `openai_base_url`

### `[TRANS]`

- `model`
- `temp`
- `max_fixing_iterations`
- `max_test_gen_iterations`
- `test_cases_num`

### `[SPECSGEN]`

- `model`
- `temp`
- `max_specgen_num`

### `[INVSGEN]`

- `model`
- `temp`
- `max_invgen_num`

---

## 7. 关键实现细节

### 7.1 翻译测试与规约测试的职责分离

当前实现中，测试数据已经明确分为两类：

- `translation_test_cases.json`
  - 来源：自动生成；
  - 用途：翻译阶段语义一致性测试。

- `spec_test_cases`
  - 来源：用户显式输入；
  - 用途：规约生成提示增强与规约评测。

这种设计可以避免两个不同目标的测试数据被混用。

### 7.2 `common/testcase_utils.py` 的作用

该模块减少了不同阶段对测试样例的重复处理，主要负责：

- `normalize_spec_test_cases`
  - 将 `expected_output` 统一转换为列表；
  - 便于单返回值与多返回值统一处理。

- `python_value_to_dafny_literal`
  - 将 Python 值转换为 Dafny 可识别的字面量；
  - 支持基础标量、字符串、列表、元组、字典等形式。

- `format_behavior_examples`
  - 将用户样例格式化为规约生成阶段可读的提示文本。

### 7.3 规约评测中的变异测试

规约评测采用“正例 + 变异反例”的方式：

- 正例：应满足规约；
- 变异反例：应尽量不满足规约。

当前变异主要针对输出值本身进行，支持以下常见情况：

- 整数扰动；
- 布尔翻转；
- 实数微扰；
- 列表插入/删除；
- 字符串扰动。

---

## 8. 当前实现的注意事项

### 8.1 文档描述应以实现为准

如果你准备在论文中引用本仓库，请区分：

- **理想流程**：论文中的方法论描述；
- **当前实现**：本仓库实际代码行为。

技术文档和仓库说明建议优先忠实描述当前实现，以避免仓库与论文表述出现明显不一致。

### 8.2 模型服务依赖

本仓库当前依赖外部大模型服务，因此复现实验时需要：

- 合法的 API Key；
- 对应的服务地址；
- 可访问的网络环境。

### 8.3 当前发布版仓库状态

当前公开版仓库已经完成一轮发布清理：

- 本地 `env.config` 不再跟踪，仅保留 `env.config.example`；
- `output/` 目录仅保留 `output/.gitkeep`；
- `input/specgen_input.json` 作为中间文件不再跟踪；
- `venv/`、`__pycache__/`、`.DS_Store`、本地 IDE 配置已移出仓库。

---

## 9. 扩展方向

如果后续继续扩展 AutoVeri-Python，可优先考虑以下方向：

1. 增强 `spec_test_cases` 的类型表达能力；
2. 补充更完备的依赖说明与环境自动化脚本；
3. 改进 `execute.py` 的整体控制流与阶段状态输出；
4. 为规约评测增加更稳定的类型推断与多返回值支持；
5. 增加更正式的日志与实验记录导出能力。

---

## 10. 关联文档

- 仓库首页：`README.md`
- 使用说明：`docs/USAGE.md`
