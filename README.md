<p align="center">
  <img src="app.ico" alt="steamedit-Enhance Logo" width="128" />
</p>

<h1 align="center">steamedit-Enhance</h1>

<p align="center">
  <strong>本地 Steam 游戏库编辑工具</strong><br/>
  重命名游戏 · 修改排序 · 管理启动配置 · AI 翻译游戏名称
</p>

<p align="center">
  <a href="README-CN.md">中文</a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows%2010%2F11-blue?style=flat-square" alt="Platform" />
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet" alt=".NET 10" />
  <img src="https://img.shields.io/badge/UI%20Languages-English%20%7C%20简体中文%20%7C%20繁體中文-orange?style=flat-square" alt="Languages" />
  <img src="https://img.shields.io/badge/License-Proprietary-red?style=flat-square" alt="License" />
</p>

---

## 关于

steamedit-Enhance 是一款 Windows 桌面应用，用于编辑 Steam 本地的 `appinfo.vdf` 文件。你可以自定义游戏显示名称、排序方式、隐藏/完成状态、启动参数、控制器支持、VR 设置等——所有修改仅作用于本地 Steam 游戏库。

> **原创软件：** [https://steamedit.tg-software.com/](https://steamedit.tg-software.com/)

> **声明：** 本项目是原创 SteamEdit 的迁移和本地化版本。如认为权益受到侵犯，请 [提交 Issue](../../issues)，我会及时处理。

---

## 功能特性

| 功能 | 说明 |
|:---|:---|
| **游戏重命名** | 自定义游戏在 Steam 库中的显示名称 |
| **排序修改** | 设置自定义排序名称，改变游戏在列表中的位置 |
| **隐藏 / 完成** | 将游戏标记为隐藏或已完成 |
| **启动配置** | 编辑启动可执行文件、参数和工作目录 |
| **条目类型** | 更改游戏类型（Game、DLC、Demo 等） |
| **控制器支持** | 设置控制器支持级别（None / Partial / Full） |
| **VR 分类** | 设置 VR 分类（Default / Removed） |
| **自定义排序** | 按名称或 App ID 排序，支持升序/降序切换 |
| **批量查找替换** | 在筛选结果中使用纯文本或正则表达式替换游戏名称 |
| **AI 翻译** | 单个或批量翻译游戏名称，支持自定义提示词和翻译规则 |
| **Web 搜索** | 一键在浏览器中搜索游戏名称 + 翻译名称 |
| **崩溃日志** | 未处理异常时生成带时间戳的诊断日志文件，方便排查问题 |

---

## 相比原创版本的新功能

| 功能 | 说明 |
|:---|:---|
| **AI 翻译** | 集成 OpenAI 兼容 API，一键翻译游戏名称。支持自定义 API 端点、密钥和模型选择；可自动获取模型列表。自动使用已有修改作为术语库，保持翻译一致性。 |
| **术语库缓存** | 修改过的游戏名称自动缓存到 `glossary.json`，启动时增量同步，翻译时直接读取——无需重建。 |
| **Web 搜索** | 一键在浏览器中搜索游戏名称 + 翻译名称。支持 Google、Bing、百度、DuckDuckGo。 |
| **多语言 UI** | 支持英文、简体中文、繁体中文。在设置中切换语言，无需重启。 |
| **开机自启** | 支持 SteamEdit 开机自动启动。支持 `-autofix`（自动应用更改）和 `-forcestart`（重启 Steam）参数。 |
| **DPI 感知** | PerMonitorV2 高 DPI 支持——4K 或高分辨率显示器上不会模糊。 |
| **异步加载** | `appinfo.vdf` 异步加载，带等待对话框、取消选项和 30 秒超时。 |
| **筛选防抖** | 搜索输入 500ms 防抖，避免频繁刷新列表。 |
| **自定义排序** | 按名称或 App ID 排序，支持升序/降序切换。排序偏好自动持久化。 |
| **批量查找替换** | 支持纯文本和正则表达式，带大小写敏感切换。实时显示匹配数量。 |
| **翻译规则** | AI 分析术语库自动推导翻译规则，应用于后续翻译以保持风格一致。 |
| **崩溃日志** | 未处理异常时生成 `crash-{时间戳}.log` 文件，包含系统信息和完整堆栈。对话框显示文件路径，可选继续或退出。 |

---

## 相比原创版本的改进

| 改进 | 说明 |
|:---|:---|
| **属性查找加速** | PropertyTable 使用并行 Dictionary，属性查找从 O(n) 降至 O(1)。 |
| **哈希缓存** | `PropertyTable.GetHashCode` 缓存哈希值，仅在属性变更时重算。 |
| **术语库反向索引** | `TranslationGlossary.FindTranslatedByOriginalName` 从 O(n) 线性扫描优化为 O(1) 字典查找。 |
| **属性移除优化** | `PropertyTable.RemoveProperty` 利用字典定位，避免字符串比较循环。 |
| **缓冲日志写入** | 用 `StreamWriter`（每 20 行自动刷新）替代 `File.AppendAllLines`，减少磁盘 I/O。启动时清除旧日志，仅保留当前会话。 |
| **代码重构** | 提取 `AppInfoBase<TEntry>` 泛型基类统一三个 AppInfo 版本；`PropertyTableIO` 委托注入消除六段重复类型 switch；`Mods` 读取器合并（V4/V5/V6 → 单一方法）；`LocalizableForm` 基类消除 11 个窗体的 i18n 样板代码——总计消除 ~1000+ 行重复代码。 |
| **统一路径解析** | 将启动路径逻辑合并到单一方法，移除三处独立实现。 |
| **排序预计算** | 归一化排序键预先计算，避免 O(n log n) 排序中的重复字符串处理。 |
| **资源管理** | `IDisposable` 资源（如 SHA1）正确释放，消除内存泄漏。注册 `ProcessExit` 事件确保日志文件在进程退出时关闭。 |
| **空引用防护** | 在 9+ 个事件处理器中添加 null 检查，防止列表为空时崩溃。 |
| **异常捕获缩窄** | 将 4 处空 `catch {}` 缩窄为只捕获预期异常类型（`ExternalException`、`ArgumentException`），避免吞掉致命异常。 |
| **UI 布局修复** | 自启动区域使用 `FlowLayoutPanel`，中文文本不再与相邻控件重叠。 |
| **架构分层** | 进程管理提取到 `SteamProcessManager`；`EditController` 与 `GameListManager` 解耦（44→20 处直接调用）；`AppInfoEntryType` 移至 Common 层消除反向依赖；`WaitingDialog` 移至 UI 层。 |
| **防御性校验** | 二进制解析添加 count/dataSize/index 上限检查和递归深度限制（最大 32 层）。AI 翻译响应添加内容类型校验。`Settings.SaveJson` 添加 null 检查。 |
| **DRY 重构** | `AiTranslator` 提取 `CallChatCompletionAsync` 消除 ~80 行重复代码；`PropertyTableIO` Mods 格式委托 0x00000000。 |

---

## 系统要求

- Windows 10 / 11 (x64)
- 已安装 Steam 客户端

> **独立构建** 无需 .NET 运行时。**依赖框架的构建** 需要 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)。

---

## 构建和运行

```bash
# 克隆仓库
git clone https://github.com/<your-username>/steamedit.git
cd steamedit

# 开发构建
dotnet build SteamEdit.csproj

# 运行测试
dotnet test SteamEditTests/SteamEditTests.csproj

# 运行单个测试
dotnet test SteamEditTests/SteamEditTests.csproj --filter "FullyQualifiedName=SteamEditTests.PropertyTableTests.AddPropertyValue_CanReadBack"

# 发布独立单文件 EXE（~110 MB，无需运行时）
dotnet publish SteamEdit.csproj -c Release

# 发布依赖框架版本（~650 KB，需要 .NET 10 运行时）
dotnet publish SteamEdit.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=true

# 开发模式运行
dotnet run --project SteamEdit.csproj
```

发布输出位于 `bin/Release/net10.0-windows/win-x64/publish/`，生成的文件名为 `steamedit-Enhance.exe`。

---

## 使用说明

### 首次启动

1. 运行 `steamedit-Enhance.exe`
2. 程序会通过注册表自动查找 Steam 安装路径
3. 如果未找到，会弹出文件对话框——手动定位 `appinfo.vdf`
   > 默认路径：`C:\Program Files (x86)\Steam\appcache\appinfo.vdf`

### 编辑工作流

1. 从左侧列表选择一个游戏
2. 在右侧修改名称、排序键、控制器支持等字段
3. 点击 **保存** → **应用** → **重启 Steam** 使更改生效

### AI 翻译

1. 打开 **设置**，输入 AI API 端点（如 `https://api.openai.com/v1`）和 API Key
2. 点击 **获取模型列表** 选择模型，或手动输入模型名称
3. 选择目标翻译语言（简体中文 / 英文）
4. 在主窗口选择游戏，点击 **AI 翻译** 按钮——翻译名称会自动填入
5. 术语库缓存到 `glossary.json`；翻译风格会参考你之前的修改

任何兼容 OpenAI Chat Completions API 的服务均可使用（OpenAI、Claude API 代理、本地 Ollama 等）。

### Web 搜索

选择游戏后点击 **Web 搜索**，会在浏览器中搜索 `游戏名称 + 目标语言`。搜索引擎可在设置中更改（Google / Bing / 百度 / DuckDuckGo）。

### 崩溃日志

如果程序遇到未处理异常，会自动生成 `crash-{yyyy-MM-dd_HH-mm-ss}.log` 文件，包含：
- 时间戳、操作系统版本、.NET 版本、命令行
- 完整的异常堆栈跟踪

对话框会显示日志文件路径，你可以选择"关闭"（继续运行）或"退出程序"。

### 命令行模式

```bash
# 自动应用待处理的更改（无 GUI）
steamedit-Enhance.exe -autofix

# 自动应用并重启 Steam
steamedit-Enhance.exe -autofix -forcestart
```

适用于通过计划任务进行定期维护。

---

## 国际化

steamedit-Enhance 包含英文、简体中文和繁体中文，通过 `.resx` 资源文件管理。

### 切换语言

1. 打开 **设置**（齿轮图标）
2. 在 **语言** 下拉框中选择 `English`、`简体中文` 或 `繁體中文`
3. UI 即时切换——无需重启

### 添加新语言

1. 复制 `SteamEdit/Resources/Strings.resx` 为 `Strings.{culture}.resx`（如 `Strings.ja.resx`）
2. 翻译所有 `<value>` 标签内的文本
3. 在 `SettingsDlg.cs` 的 `comboLanguage.Items` 中添加对应选项

---

## 项目结构

```
steamedit/
├── SteamEdit/                    # WinForms UI 层
│   ├── Program.cs                # 入口点 + 全局异常处理
│   ├── MainWindow.cs             # 主窗口（布局 + 事件路由）
│   ├── GameListManager.cs        # 游戏列表：搜索/筛选/排序/ListBox
│   ├── EditController.cs         # 属性编辑/撤销/还原
│   ├── GameDataContext.cs        # 共享数据容器
│   ├── LocalizableForm.cs        # i18n 窗体基类
│   ├── WaitingDialog.cs          # 异步等待对话框
│   ├── LaunchEditor.cs           # 启动配置编辑器
│   ├── SettingsDlg.cs            # 设置对话框
│   ├── I18n.cs                   # 语言管理器
│   └── Resources/
│       ├── Strings.resx          # 英文资源（默认）
│       ├── Strings.zh-CN.resx    # 简体中文翻译
│       └── Strings.zh-TW.resx    # 繁体中文翻译
├── SteamEdit.Data/               # 核心数据模型与 I/O
│   ├── SteamData.cs              # 数据加载/保存/应用
│   ├── SteamProcessManager.cs    # Steam 进程管理（检测/关闭/启动）
│   ├── AppInfoFactory.cs         # 版本分发工厂
│   ├── PropertyTable.cs          # 属性表（Dictionary 优化 + 哈希缓存）
│   ├── PropertyTableIO.cs        # 属性表序列化（委托注入统一三种格式）
│   ├── ModifiedEntry.cs          # 修改条目（含 GetOriginalName）
│   └── Mods.cs                   # mods.dat 读写
├── SteamEdit.Data.Impl/          # appinfo.vdf 版本实现
│   ├── AppInfoBase.cs            # 容器基类（泛型，共享 Save/Find/Revert 逻辑）
│   ├── AppInfoEntryBase.cs       # 条目基类（共享属性/修改覆盖/写入准备）
│   ├── AppInfo_0x07564427.cs     # 旧格式
│   ├── AppInfo_0x07564428.cs     # 中间格式
│   └── AppInfo_0x07564429.cs     # 最新格式
├── Common/                       # 共享工具
│   ├── Settings.cs               # 用户设置（JSON 序列化 + 脏标记）
│   ├── Log.cs                    # 缓冲日志（ProcessExit 自动关闭）
│   ├── AiTranslator.cs           # AI 翻译器（OpenAI 兼容 API）
│   ├── TranslationGlossary.cs    # 术语缓存（glossary.json + 反向索引）
│   ├── AppHelper.cs              # 路径与参数辅助
│   └── AppInfoEntryType.cs       # 条目类型枚举（Flags）
├── SteamEditTests/               # MSTest 单元测试（120 个测试）
│   ├── PropertyTableTests.cs     # PropertyTable：15 个测试
│   ├── ModsTests.cs              # Mods 二进制格式：5 个测试
│   ├── SettingsTests.cs          # Settings JSON 序列化：4 个测试
│   ├── BinaryParsingHardeningTests.cs  # 二进制边界校验：18 个测试
│   ├── StringTitleComparerTests.cs     # 自然排序：8 个测试
│   ├── UndoManagerTests.cs       # 撤销栈：8 个测试
│   ├── AppHelperTests.cs         # CLI 参数解析：13 个测试
│   ├── TranslationGlossaryTests.cs     # 术语缓存：6 个测试
│   ├── ModifiedEntryTests.cs     # 变更覆盖层：17 个测试
│   ├── PropertyTableIOTests.cs   # 二进制序列化：8 个测试
│   ├── AppInfoFactoryTests.cs    # 版本分发：3 个测试
│   ├── LaunchTableTests.cs       # 启动配置：6 个测试
│   └── AppInfoEntryTests.cs      # Entry 属性操作：10 个测试
└── SteamEdit.csproj              # 项目文件
```

---

## 技术栈

| 技术 | 版本 |
|:---|:---|
| .NET | 10.0 |
| 语言 | C# 13+ |
| UI 框架 | Windows Forms |
| 目标平台 | win-x64 |
| 发布方式 | 单文件 EXE（独立或依赖框架） |

---

## 许可与声明

- 原创软件版权归 [Tim Green](mailto:tim@tg-software.com) 所有
- 原创下载：[https://steamedit.tg-software.com/](https://steamedit.tg-software.com/)
- 本项目仅涉及技术迁移（.NET Framework → .NET 10）和 UI 本地化
- **如存在侵权，请 [提交 Issue](../../issues) 申请移除**
