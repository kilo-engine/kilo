# Claude Code / Kimi Code CLI 监控客户端可行性分析

> 目标：开发 Windows 桌面应用，实时监控多个 CLI 对话的执行状态、Agent 层级、对话内容。

---

## 一、需求拆解

| 需求 | 技术要点 |
|------|----------|
| **多 Tab 会话管理** | 同时托管多个 `claude` / `kimi` 进程，每个进程对应一个 Tab |
| **实时状态监控** | 区分：`执行中`（LLM 推理/工具执行）、`等待输入`（需要人工介入）、`空闲/结束` |
| **Agent 层级可视化** | 识别父对话 → 子 Agent 的嵌套关系，显示各 Agent 的独立状态 |
| **美观对话渲染** | Markdown、代码块高亮、工具调用折叠/展开、流式打字机效果 |
| **双向交互** | 用户能在 GUI 中向指定会话发送消息，接管 CLI 输入 |

---

## 二、核心问题：是否有官方协议支持？

### 结论：没有。

**Claude Code** 和 **Kimi Code CLI** 都是**面向终端的闭源/独立 CLI 应用**，目前均未公开以下任何形式的监控接口：

- ❌ HTTP REST API
- ❌ WebSocket 事件流
- ❌ gRPC 服务
- ❌ 本地 Named Pipe / Unix Socket 协议
- ❌ 结构化 JSON-RPC over stdio（用于监控目的）

### 关于 MCP 的误解澄清

**MCP (Model Context Protocol)** 是 Anthropic 推动的开放协议，但：
- 它是 **AI ↔ 工具** 之间的通信协议，不是 **人类 ↔ 监控 UI** 的协议
- Claude Code 可以作为 MCP Client 调用外部 MCP Server
- 你可以写一个 MCP Server 来“间接”记录工具调用，但**无法获取完整的对话流、LLM 推理过程、Agent 嵌套状态**
- Kimi Code CLI 目前未见公开支持 MCP

### 因此，任何第三方 GUI 监控都必须走“非官方”路径。

---

## 三、可行技术方案对比

### 方案 A：ConPTY 终端捕获 + 输出解析（推荐作为可行性验证）

**原理**：
使用 Windows Pseudo Console (ConPTY) API 创建一个虚拟终端，将 `claude.exe` 或 `kimi.exe` 作为子进程运行在其中。GUI 通过 PTY 的 stdout pipe 捕获完整的 ANSI 转义序列和文本输出，再解析状态和对话内容。

**架构图**：

```
┌─────────────────────────────────────────┐
│         Windows Desktop App             │
│  ┌─────────┐  ┌─────────┐  ┌─────────┐  │
│  │  Tab 1  │  │  Tab 2  │  │  Tab 3  │  │
│  │ (Kimi)  │  │(Claude) │  │ (Kimi)  │  │
│  └────┬────┘  └────┬────┘  └────┬────┘  │
│       └─────────────┴─────────────┘      │
│                   │                      │
│         ConPTY Host (C#)                │
│    CreatePseudoConsole + pipe stdout    │
│                   │                      │
└───────────────────┼──────────────────────┘
                    │
         ┌──────────┴──────────┐
         ▼                     ▼
   ┌─────────────┐       ┌─────────────┐
   │  kimi.exe   │       │ claude.exe  │
   │  (子进程)    │       │  (子进程)    │
   └─────────────┘       └─────────────┘
```

**状态推断逻辑（启发式）**：

| 捕获到的输出特征 | 推断状态 |
|------------------|----------|
| 包含工具调用块（如 `<antThinking>`、`<antThinking>`、特定 JSON/XML 标记） | 🟢 执行中 |
| LLM 正在流式输出文本（Token 持续涌入，无 Prompt） | 🟢 执行中 |
| 出现输入提示符（如 `>` 或 `kimi>`），且后续长时间无输出 | 🔴 等待输入 / 需要人工介入 |
| 进程退出或收到 EOF | ⚫ 执行结束 |

**Agent 检测**：
- Kimi Code CLI 在启动子 Agent 时，通常会在输出中显示 `Agent:` 或 `Subagent` 相关标识（如 `description` 字段），可通过正则匹配识别 Agent 名称和任务描述
- 缩进层级、特殊前缀可用于推断父子关系

**优点**：
- ✅ 无需修改 CLI 源码，通用性强
- ✅ 可直接复用 Windows 原生 ConPTY API（C# 通过 P/Invoke 或 `System.IO.Pipes` + 第三方库调用）
- ✅ 能获取完整的视觉输出（包括 CLI 的 TUI 动画、颜色）

**缺点**：
- ❌ **脆弱**：输出格式（ANSI 序列、Prompt 样式、工具调用标记）随 CLI 版本更新可能变化，解析逻辑需要持续维护
- ❌ 状态推断是**启发式**的，无法 100% 准确区分"LLM 正在思考"和"网络卡顿"
- ❌ 需要处理复杂的 ANSI Escape Codes（光标移动、清屏、颜色代码）

---

### 方案 B：Wrapper CLI + 结构化 IPC（更稳健但需用户切换入口）

**原理**：
不直接启动 `kimi.exe`，而是让用户启动一个你自己开发的 `kimi-monitor.exe`（Wrapper）。Wrapper 内部通过 ConPTY 或直接的 Process 启动真实的 `kimi.exe`，同时作为一个中间代理：
1. 将用户的键盘输入转发给 `kimi.exe` 的 stdin
2. 将 `kimi.exe` 的 stdout 捕获并解析
3. 将解析出的**结构化事件**（JSON）通过 Named Pipe / WebSocket 发送到 GUI

**事件示例**：
```json
{
  "event": "agent.start",
  "sessionId": "session-001",
  "agentId": "agent-abc",
  "parentAgentId": null,
  "description": "Explore codebase",
  "timestamp": "2026-04-12T10:00:00Z"
}
```

```json
{
  "event": "tool.call",
  "sessionId": "session-001",
  "agentId": "agent-abc",
  "tool": "Shell",
  "parameters": { "command": "dotnet build" },
  "status": "running"
}
```

```json
{
  "event": "state.change",
  "sessionId": "session-001",
  "state": "awaiting_user_input",
  "prompt": "Please choose an option..."
}
```

**优点**：
- ✅ 解析逻辑集中在 Wrapper，GUI 只接收干净的事件
- ✅ 状态推断可以做得更智能（如在 Wrapper 中维护状态机）
- ✅ 即使 GUI 崩溃或重启，Wrapper 仍可保持会话存活

**缺点**：
- ❌ 需要用户改变使用习惯（从运行 `kimi` 改为运行 `kimi-monitor`）
- ❌ 本质上仍然是解析终端输出，只是将解析层外移了
- ❌ 如果 CLI 使用了复杂的 TUI（如 ncurses 式全屏应用），PTY 捕获会更复杂

---

### 方案 C：日志/会话文件监控（非侵入式但信息不足）

**原理**：
监控 CLI 在本地的日志目录：
- Kimi Code CLI：可能在 `~/.kimi/sessions/`、`~/.kimi/logs/` 下保存会话记录
- Claude Code：可能在 `~/.claude/`、`~/.config/claude/` 下保存状态

通过 `FileSystemWatcher` 实时监听文件变化，读取最新的对话轮次。

**优点**：
- ✅ 完全非侵入式，不影响 CLI 正常运行
- ✅ 不需要启动子进程

**缺点**：
- ❌ **信息严重不足**：日志通常只保存最终对话历史，不保存实时状态（如当前是否在执行 Shell、Agent 是否活跃）
- ❌ 文件格式不公开，且可能加密或频繁变更
- ❌ 无法感知"等待用户输入"这类瞬时状态

**结论**：仅适合作为**历史记录回看**，不适合实时监控。

---

### 方案 D：MCP 代理 + 工具调用拦截（仅限 Claude，且信息残缺）

**原理**：
为 Claude Code 配置一个自定义 MCP Server。当 Claude 调用工具时，MCP Server 可以记录工具名称和参数。

**局限**：
- 只能看到**工具调用**事件，看不到 LLM 的纯文本推理、Agent 启动/结束、对话内容
- 无法知道当前是"执行中"还是"等待输入"
- 对 Kimi Code CLI 不适用

**结论**：不满足需求，不推荐。

---

## 四、Windows 桌面应用技术栈建议

### 推荐组合

| 层级 | 技术 | 说明 |
|------|------|------|
| **UI 框架** | **WinUI 3** (Windows App SDK) 或 **WPF** | WinUI 3 更现代，支持 Fluent Design；WPF 开发效率更高，生态成熟 |
| **对话渲染** | **WebView2** (Microsoft Edge WebView2) | 在 WinUI/WPF 中嵌入 WebView2，用 HTML/CSS/JS 渲染 Markdown、代码高亮、流式文本，效果远胜于原生 RichTextBlock |
| **进程托管** | **ConPTY** (Windows Pseudo Console) | 通过 C# P/Invoke 调用 `CreatePseudoConsole` API，托管 CLI 进程 |
| **进程间通信** | **Named Pipe** 或 **Memory-Mapped File** | 如果选择方案 B（Wrapper + IPC），Named Pipe 是 Windows 上最轻量的方案 |
| **数据存储** | **SQLite** 或 **LiteDB** | 保存会话历史、Agent 关系树、状态变化时间线 |

### ConPTY 在 C# 中的实现要点

Windows 10/11 提供了 `CreatePseudoConsole` API。基本流程：

```csharp
// P/Invoke 声明
[DllImport("kernel32.dll", SetLastError = true)]
static extern int CreatePseudoConsole(
    COORD size,
    IntPtr hInput,
    IntPtr hOutput,
    uint dwFlags,
    out IntPtr phPC);

// 创建匿名管道
CreatePipe(out IntPtr hInRead, out IntPtr hInWrite, ...);
CreatePipe(out IntPtr hOutRead, out IntPtr hOutWrite, ...);

// 创建 Pseudo Console
CreatePseudoConsole(size, hInRead, hOutWrite, 0, out IntPtr hPC);

// 启动子进程（kimi.exe / claude.exe）
// 通过 UpdateProcThreadAttribute 设置 PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE
// 子进程会认为自己在真实的终端中运行

// GUI 端：
// - 向 hInWrite 写入用户键盘输入
// - 从 hOutRead 读取 ANSI 输出流
```

**解析 ANSI 输出**：
- 可使用第三方库如 `Vezel.Terminal` 或 `System.CommandLine.Rendering` 解析 ANSI escape sequences
- 或者简单地将原始 ANSI 文本丢给 WebView2，由前端库（如 `xterm.js`）渲染，最大程度还原终端效果

### UI 布局建议

```
┌──────────────────────────────────────────────────────────────┐
│ [Tab: Kimi #1] [Tab: Claude #2] [Tab: Kimi #3] [+]          │
├──────────────┬───────────────────────────────────────────────┤
│              │                                               │
│  Agent 树    │           对话内容 (WebView2)                 │
│  ─────────   │           ───────────────────                 │
│  🟢 Agent A  │  User: 帮我分析代码                           │
│  └── 🟡 AgB  │  ─────────────────────────────                │
│      └── ⚫ C│  🤖 正在分析...                               │
│  🟢 Agent D  │  🔧 调用 Shell: `findstr ...`                 │
│              │  ⏱️  执行中...                                 │
│  ─────────   │                                               │
│  状态面板    │  [输入框] [发送] [中断]                        │
│  会话: 执行中 │                                               │
│  Agent: 2活跃 │                                               │
└──────────────┴───────────────────────────────────────────────┘
```

---

## 五、关键挑战与风险

### 1. 状态推断的准确性（最大风险）

CLI 不会明确广播 "我现在开始执行工具了"、"我在等待用户输入"。你必须从文本流中推断：

- **误判为"执行中"**：网络延迟导致 LLM 输出卡顿，UI 可能误以为它正在忙碌
- **误判为"等待输入"**：LLM 生成了大量代码后的短暂停顿，可能触发"等待输入"状态
- **解决方案**：
  - 结合超时机制（如 5 秒无输出才判定为等待输入）
  - 维护一个状态机，根据已知 Prompt 样式精确匹配
  - 对 Kimi CLI，利用其 `<system>` / `<system-reminder>` / `<antThinking>` 标签做更精确的模式识别

### 2. Agent 层级识别（中等风险）

Kimi Code CLI 的子 Agent 输出通常有特定的 XML 包裹格式（如 `<antThinking>`）或缩进。但如果 CLI 更新输出格式，Agent 树会错乱。
- **缓解**：不要过于依赖嵌套缩进，而以 Agent 的 `agent_id` 和明确的启动/结束标记为准
- **观察**：从 Kimi CLI 的实际输出来看，它确实会输出类似 `Agent:` 的任务描述和 agent_id

### 3. TUI 兼容性问题

如果 `claude` 或 `kimi` 使用了全屏 TUI（如 `curses`/`blessed` 风格界面，支持光标到处跳、清屏、颜色渐变），PTY 捕获会收到大量光标定位指令，直接渲染很困难。
- **缓解**：使用 `xterm.js` 在 WebView2 中渲染，它是专业的终端模拟器前端库，能完美处理 ANSI 序列
- **副作用**：如果用了 xterm.js，你的"美观对话查看"就变成了"嵌入终端"，需要再做一层提取才能把它变成聊天界面样式

### 4. 用户输入的接管

当 CLI 调用 `AskUserQuestion` 或处于交互式提示时，GUI 必须能向该特定进程的 stdin 发送准确的输入。
- ConPTY 方案天然支持这一点（写入 hInWrite 管道即可）
- 但需要处理特殊按键（如 `Ctrl+C` 中断、方向键选择选项）

### 5. 多实例资源占用

每个 Tab 对应一个独立的 `kimi.exe` / `claude.exe` 进程，内存和 CPU 开销较大。
- 建议限制最大并发 Tab 数（如 5-8 个）
- 对非活跃 Tab 可进入"低功耗模式"（暂停流式读取，只保留进程存活）

---

## 六、分阶段实施建议

### Phase 1：POC 验证（1-2 周）

**目标**：验证 ConPTY 方案能否稳定捕获 `kimi` / `claude` 的输出。

1. 用 C# + P/Invoke 写一个简单的 ConPTY Host
2. 启动 `kimi` 或 `claude`，能捕获 stdout 并显示在 `xterm.js` 或 RichTextBox 中
3. 测试基本输入转发（发送一条消息，CLI 能正常响应）
4. 观察并记录以下场景的输出模式：
   - LLM 流式生成文本
   - 执行 Shell 工具
   - 启动子 Agent
   - 调用 `AskUserQuestion`
   - 任务完成后的 Prompt 样式

### Phase 2：状态机与解析器（2-3 周）

**目标**：从原始输出中提取结构化状态。

1. 设计一个 `SessionStateMachine`：
   - `Idle` → `Generating` → `ToolExecuting` → `AwaitingInput` → `Idle`
2. 基于 Phase 1 的输出样本，编写正则/模式匹配规则
3. 实现 Agent 生命周期跟踪（Agent Start / Agent End / Agent Output）
4. 构建一个简单的 WPF/WinUI 原型：左侧状态树，右侧 WebView2 对话流

### Phase 3：美观渲染与交互（2-3 周）

**目标**：将捕获的原始输出转换为美观的聊天界面。

1. 前端使用 React + WebView2，实现：
   - Markdown 渲染（`react-markdown`）
   - 代码块高亮（`Prism.js` 或 `highlight.js`）
   - 工具调用卡片（可折叠）
   - Agent 消息嵌套气泡
   - 流式打字机效果
2. GUI 与 CLI 的双向交互（发送消息、中断执行、选择选项）

### Phase 4：工程化与发布（2 周）

1. 支持多 Tab、会话持久化、历史记录搜索
2. 支持自定义主题、字体、快捷键
3. 打包为 MSI / MSIX 安装包

---

## 七、总体可行性结论

| 维度 | 评估 |
|------|------|
| **技术可行性** | ✅ **可行**。Windows ConPTY 提供了成熟的子终端托管能力，WPF/WinUI + WebView2 能做出非常现代的 UI。 |
| **协议支持** | ❌ **无官方协议**。必须基于终端输出解析，这是最大的工程风险点。 |
| **准确性** | ⚠️ **中等**。状态推断是启发式的，准确率约 80%-95%，取决于输出格式稳定性。需要持续维护解析规则。 |
| **开发工作量** | 📅 **中等偏大**。POC 约 1-2 周，完整可用版本约 6-8 周（1 名全栈 Windows 开发者）。 |

### 最终建议

1. **采用"ConPTY + Wrapper + WebView2"混合架构**：
   - ConPTY 托管 CLI 进程
   - Wrapper（C# 中间层）负责解析 ANSI 输出并生成结构化事件
   - GUI 用 WinUI 3 + WebView2 展示

2. **优先支持 Kimi Code CLI**：
   - 因为 Kimi CLI 的输出格式相对规律（有明显的 XML 工具调用标记、Agent 标识），且你在 Windows 环境，测试成本低。

3. **对 Claude Code 保持观望**：
   - 如果 Claude Code 未来官方开放了 API 或 MCP 监控接口，可以无缝迁移；目前走 PTY 捕获同样适用，但解析规则需要单独维护。

4. **降低解析风险的设计**：
   - 不要只做一个"状态灯"，而是同时提供**原始终端视图**（xterm.js）和**格式化对话视图**（WebView2）。当格式化解析出错时，用户可切回原始终端确认。
