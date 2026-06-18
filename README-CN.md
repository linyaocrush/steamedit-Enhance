<p align="center">
  <img src="app.ico" alt="steamedit-Enhance Logo" width="128" />
</p>

<h1 align="center">steamedit-Enhance</h1>

<p align="center">
  <strong>Steam 游戏库本地编辑工具</strong><br/>
  重命名游戏、修改排序、管理启动配置、批量设置控制器与 VR 支持
</p>

<p align="center">
  <a href="README.md">English</a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/平台-Windows%2010%2F11-blue?style=flat-square" alt="Platform" />
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet" alt=".NET 10" />
  <img src="https://img.shields.io/badge/界面语言-English%20%7C%20简体中文-orange?style=flat-square" alt="Languages" />
  <img src="https://img.shields.io/badge/许可证-专有软件-red?style=flat-square" alt="License" />
</p>

---

## 关于

SteamEdit 是一款 Windows 桌面应用，用于编辑 Steam 本地的 `appinfo.vdf` 文件。它允许你自定义游戏库中显示的名称、排序方式、隐藏/完成状态、启动参数、控制器支持及 VR 设置等。

> **原版软件地址：** [https://steamedit.tg-software.com/](https://steamedit.tg-software.com/)

> **版权声明：** 本项目基于原版 SteamEdit 进行迁移和汉化。如果您认为本项目侵犯了您的权益，请 [提交 Issue](../../issues) 联系我，我将尽快处理。

---

## 相比原版的新功能

| 功能 | 说明 |
|:---|:---|
| **AI 翻译** | 集成 OpenAI 兼容 API，一键翻译游戏名称。支持自定义 API 地址、密钥、模型选择，可获取模型列表。翻译时自动参考已有修改记录作为术语表，保持翻译风格一致 |
| **术语表缓存** | 已修改的游戏名称自动缓存为 `glossary.json`，启动时增量同步，翻译时直接读取，无需每次重建 |
| **网页搜索** | 一键在浏览器中搜索游戏名 + 翻译名，支持 Google/Bing/Baidu/DuckDuckGo 搜索引擎选择 |
| **中英双语界面** | 支持 English 和简体中文，设置中即时切换，无需重启 |
| **开机自启动** | 可设置 Windows 启动时自动运行，支持附加 `-autofix`（自动应用更改）和 `-forcestart`（重启 Steam）参数 |
| **DPI 感知** | 启用 PerMonitorV2 高 DPI 支持，4K/高分屏不再模糊 |
| **异步加载** | appinfo.vdf 加载过程异步执行，显示等待对话框，支持取消和 30 秒超时 |
| **过滤防抖** | 搜索框输入时 500ms 防抖，避免频繁刷新列表 |
| **自定义排序** | 排序按钮支持按名称或 App ID 排序，可切换正序/倒序，设置自动持久化 |
| **批量查找替换** | 支持普通文本和正则表达式，可切换大小写敏感，实时显示匹配数量 |
| **翻译规则** | AI 分析术语表自动总结翻译规则，后续翻译时自动应用，保持风格一致 |

---

## 相比原版的优化

| 优化 | 说明 |
|:---|:---|
| **属性查找加速** | PropertyTable 引入 Dictionary 并行存储，属性查找从 O(n) 降至 O(1) |
| **日志缓冲写入** | StreamWriter 替代 File.AppendAllLines，每 20 条自动刷新，减少磁盘 I/O。每次启动清空旧日志，只保留本次运行记录 |
| **代码重构** | 三个 AppInfoEntry 版本提取公共基类，消除 ~800 行重复代码 |
| **路径解析合并** | 启动路径解析逻辑统一为单一方法，消除三重重复 |
| **排序预计算** | 排序时预计算规范化键值，避免 O(n log n) 次重复字符串处理 |
| **资源管理** | SHA1 等 IDisposable 资源正确释放，消除内存泄漏 |
| **空引用防护** | 9+ 个事件处理器添加 null 检查，防止列表为空时崩溃 |
| **UI 布局修复** | 自启动区域改用 FlowLayoutPanel，中文文本不再覆盖相邻控件 |

---

## 功能特性

| 功能 | 说明 |
|:---|:---|
| **游戏重命名** | 自定义游戏在 Steam 库中显示的名称 |
| **排序修改** | 设置自定义排序名，改变游戏在列表中的位置 |
| **隐藏 / 完成** | 标记游戏为隐藏或已完成状态 |
| **启动配置** | 编辑游戏的启动项（可执行文件、参数、工作目录） |
| **条目类型** | 修改游戏类型（Game、DLC、Demo 等） |
| **控制器支持** | 批量设置控制器支持级别（无 / 部分 / 完整） |
| **VR 类别** | 批量设置 VR 类别（默认 / 已移除） |
| **自定义排序** | 按名称或 App ID 排序，支持正序/倒序切换 |
| **批量查找替换** | 对当前筛选结果批量替换游戏名，支持正则和大小写敏感 |
| **AI 翻译** | 单个或批量翻译游戏名称，支持自定义提示词和翻译规则 |

---

## 环境要求

- Windows 10 / 11（x64）
- Steam 客户端已安装

> **自包含版**无需安装 .NET 运行时；**框架依赖版**需要 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)。

---

## 构建与运行

```bash
# 克隆仓库
git clone https://github.com/<your-username>/steamedit.git
cd steamedit

# 开发构建
dotnet build SteamEdit.csproj

# 发布自包含单文件 EXE（约 110MB，无需安装运行时）
dotnet publish SteamEdit.csproj -c Release

# 发布框架依赖版（约 567KB，需要 .NET 10 Runtime）
dotnet publish SteamEdit.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=true

# 运行
dotnet run --project SteamEdit.csproj
```

发布产物位于 `bin/Release/net10.0-windows/win-x64/publish/` 目录。

---

## 使用说明

### 首次启动

1. 运行 `SteamEdit.exe`
2. 程序会自动通过注册表查找 Steam 路径
3. 若未找到，会弹出文件选择对话框，手动定位 `appinfo.vdf`
   > 默认路径：`C:\Program Files (x86)\Steam\appcache\appinfo.vdf`

### 编辑流程

1. 在左侧列表选择要编辑的游戏
2. 修改右侧的名称、排序、控制器支持等字段
3. 点击 **保存** → **应用** → **重启 Steam** 使更改生效

### AI 翻译

1. 打开 **设置**，填写 AI API 地址（如 `https://api.openai.com/v1`）和 API Key
2. 点击 **获取模型列表** 选择模型，或手动输入模型名称
3. 选择目标翻译语言（简体中文 / English）
4. 在主界面选择游戏，点击 **AI 翻译** 按钮即可自动翻译并填入名称
5. 术语表自动缓存到 `glossary.json`，翻译风格会参考你之前的修改记录

支持所有兼容 OpenAI Chat Completions API 的服务（OpenAI、Claude API 中转、本地 Ollama 等）。

### 网页搜索

选择游戏后点击 **网页搜索** 按钮，自动在浏览器中搜索 `游戏名 + 翻译语言名`。可在设置中切换搜索引擎（Google / Bing / Baidu / DuckDuckGo）。

### 命令行模式

```bash
# 自动应用待处理更改（无 GUI）
SteamEdit.exe -autofix

# 自动应用并重启 Steam
SteamEdit.exe -autofix -forcestart
```

适合配合计划任务实现定期自动维护。

---

## 国际化

SteamEdit 内置英语和简体中文两种语言，通过 `.resx` 资源文件管理。

### 切换语言

1. 打开 **设置**（齿轮图标）
2. 在 **语言** 下拉框中选择 `English` 或 `简体中文`
3. 界面即时切换，无需重启

### 添加新语言

1. 复制 `SteamEdit/Resources/Strings.resx` 为 `Strings.{culture}.resx`（如 `Strings.ja.resx`）
2. 翻译所有 `<value>` 标签中的文本
3. 在 `SettingsDlg.cs` 的 `comboLanguage.Items` 中添加对应选项

---

## 项目结构

```
steamedit/
├── SteamEdit/                    # WinForms UI 层
│   ├── MainWindow.cs             # 主窗口（游戏列表 + 编辑面板）
│   ├── LaunchEditor.cs           # 启动配置编辑器
│   ├── SettingsDlg.cs            # 设置对话框
│   ├── I18n.cs                   # 语言管理器
│   └── Resources/
│       ├── Strings.resx          # 英文资源（默认）
│       └── Strings.zh-CN.resx   # 中文翻译
├── SteamEdit.Data/               # 核心数据模型与 I/O
│   ├── SteamData.cs              # 数据加载/保存/应用
│   ├── AppInfoFactory.cs         # 版本分发工厂
│   ├── PropertyTable.cs          # 属性表（Dictionary 优化）
│   └── Mods.cs                   # mods.dat 读写
├── SteamEdit.Data.Impl/          # appinfo.vdf 版本实现
│   ├── AppInfoEntryBase.cs       # 入口基类（共享逻辑）
│   ├── AppInfo_0x07564427.cs     # 旧版格式
│   ├── AppInfo_0x07564428.cs     # 中版格式
│   └── AppInfo_0x07564429.cs     # 新版格式
├── Common/                       # 共享工具
│   ├── Settings.cs               # 用户设置（XML 序列化）
│   ├── Log.cs                    # 缓冲日志
│   ├── AiTranslator.cs           # AI 翻译（OpenAI 兼容 API）
│   ├── TranslationGlossary.cs    # 术语表缓存（glossary.json）
│   └── AppHelper.cs              # 路径与参数工具
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
| 发布方式 | 单文件 EXE（自包含或框架依赖） |

---

## 许可与免责

- 原版软件版权归 [Tim Green](mailto:tim@tg-software.com) 所有
- 原版下载地址：[https://steamedit.tg-software.com/](https://steamedit.tg-software.com/)
- 本项目仅进行技术迁移（.NET Framework → .NET 10）和界面汉化
- **如涉及侵权，请 [提交 Issue](../../issues) 联系我删除**
