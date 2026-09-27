# AI 工具配置与调用纪律（2026-09-05 订立）

> 适用：本工作区所有会话的 AI API 使用。
> 密钥位置：根目录 `.secrets.local.json`（已被 .gitignore 排除）。**密钥绝不写进任何入库文件；程序输出不回显密钥；缺失时向用户索取。**

## 〇、本地视觉直读（2026-09-18 起，**优先**）

- 会话模型本身带视觉能力，配合 `read_image` 工具可**直接读图**：截图、贴图、contact sheet 一律先走本地，**零外呼、零费用、零 base64 转换**。
- 产出侧配套：本地离屏渲染（FNA）把特效渲成 PNG 落在工作区内，形成「渲染 → 读图 → 改参数 → 重渲」闭环。
- §二 硅基流动仅在以下情况启用：本地模型无视觉能力、需要第二意见交叉核对、或需要 OCR/小字精细提取。

---

## 一、Gemini 站（文本/代码/特效咨询）

- 地址：`https://gcli.ggchan.dev/v1`（兼容 OpenAI chat/completions 协议）
- 模型：**AGY 前缀族**，默认 `agy-gemini-3.8-flash-low`（最低消耗档）；需要更强推理用 `-medium`，复杂设计用 `-high`，均属 3.8-flash 族（用户指定）。
- 增强档（用户授权，适当调用）：`agy-claude-opus-4-6` / `agy-claude-opus-4-6-thinking`——用于需要更强代码/设计判断的任务；仍守「低并发、低 token」红线。
- 用途：简单功能实现咨询、特效/美术构思、代码复审、设计小段草稿。
- 纪律（用户红线，2026-09-18 重申）：**单次调用输入 ≤ 80k token**；默认走 `-low` 档；**禁止并发调用**（串行，一次一个）；输出 ≤500；失败最多换档重试 1 次，禁止重试风暴。

## 二、硅基流动（识图/视觉）

- 地址：`https://api.siliconflow.cn/v1`
- 余额约 20+ 元——**只用于识图**，绝不用作文本闲聊。
- 模型：默认 `Qwen/Qwen3-VL-8B-Instruct`（省）；复杂/小字图 `Qwen/Qwen3-VL-32B-Instruct`；深度推理 `Qwen/Qwen3-VL-8B-Thinking`；纯文字提取 `PaddlePaddle/PaddleOCR-VL-1.5`。
- 调用方式：run_code 内 fetch；图片经 pwsh 读字节 → base64 → data URL；单图单问，输出限 300 字。
- 适用场景：作者实测截图反馈、贴图/像素图检查、UI 截图分析。

## 三、读取密钥的规范流程

1. 程序内用 read 工具或 pwsh `Get-Content` 读 `.secrets.local.json` 取字段；
2. 把 key 赋给局部变量后直接使用，**不打印、不写回、不复制进聊天**；
3. 严禁把密钥硬编码进任何入库文档/日志。

> ⚠️ PS 5.1 下 `Get-Content -Raw | ConvertFrom-Json` 会按 ANSI 解码，中文注释被读坏导致 JSON 解析失败。
> 必须显式 UTF-8：`[System.IO.File]::ReadAllText($p, [System.Text.Encoding]::UTF8)`（Node 侧默认 UTF-8，无此问题）。

---

## 三之二、沙箱调用实录（2026-09-27，**照抄别再踩**）

### 结论：**必须用 Node 调 AGY，不要用 PowerShell / curl**

| 方式 | 结果 |
|---|---|
| `Invoke-RestMethod` / `curl.exe`（Windows schannel） | ❌ `SEC_E_NO_CREDENTIALS` / "The underlying connection was closed" —— 沙箱禁止进程访问证书库，**HTTPS 必失败**（连 example.com 都返回 000） |
| `node`（自带 OpenSSL） | ✅ 直连 200；经本机代理 `127.0.0.1:7897` 也 200 |

**正式入口**：`写法\_tools\ask-agy.mjs`

```
node .\写法\_tools\ask-agy.mjs --prompt .\写法\_agy\prompt_XX.md --out .\写法\_agy\reply_XX.md
# 可选：--model agy-gemini-3.8-flash-medium|high   --retry-high   --via-proxy
```

纪律已**硬编码进脚本**（不靠人记）：串行锁 / 输入 ≤80k token 超限即拒 / `max_tokens=500` / 失败不重试风暴 / 密钥不回显。
本机开着 TUN 虚拟网卡 → **默认直连**，代理仅作回落。

### 另两个沙箱坑

1. **Node 的 `fs.rmSync` 在本沙箱下"返回成功但文件不消失"**（pwsh 的 `Remove-Item` 能删）。
   所以串行锁**不能靠删文件实现** —— 现为 **PID 存活判定**（`process.kill(pid, 0)`），释放时把锁内容改写为 `0`。
2. **本机没有 `pwsh`**（PowerShell 7 未安装），只有 `powershell` 5.1；
   且 5.1 读脚本按 ANSI → **任何含中文的 `.ps1` 必须存 UTF-8 BOM**，否则中文串会被解析成乱码并报语法错。

### 写 prompt 的经验

`max_tokens=500` 很短，**一屏问不完就跑多轮**（本会话星网视觉设计用了 3 轮：几何与四态 → 动效与布局 → 布局表与字段表）。
把"已确定的结论"在下一轮 prompt 里复述一遍并注明"不要重复"，否则模型会把额度浪费在复述上。

---

## 四、分工默认

| 任务 | 路由 |
|---|---|
| 简单功能/特效咨询、代码复审 | Gemini（AGY 3.8-flash） |
| **UI 布局 / 特效动效 / 视觉规格设计** | Gemini（AGY 3.8-flash-low）—— **与既有配色冲突时以 AGY 为准**（用户 2026-09-27 拍板；落实方式见下） |
| 识图（截图/贴图反馈/像素检查） | 硅基流动 Qwen3-VL |
| 写码与最终判定 | 本地（代理自身 + Mod-Gate 门禁为权威） |

### 配色冲突的处理方式（2026-09-27 拍板）

**R5 修订**：R5 只管"颜色集中在 `Core\MalachitePalette.cs`"这一件事，**不约束色值取向**。
AGY 稿与既有配色冲突时**以 AGY 为准**，但**不是就地硬编码** —— 把 AGY 的色值
**收编进 `MalachitePalette` 的具名条目并注明来源**，这样"改一处全局生效"的好处保留。

范例：`StarCardBg`(#0D131F) / `StarCardBorder`(#3A4D6B) / `StarCardBorderDim` / `StarInk` / `StarInkDim`。
