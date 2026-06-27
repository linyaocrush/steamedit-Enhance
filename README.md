<p align="center">
  <img src="app.ico" alt="steamedit-Enhance Logo" width="120" />
</p>

<h1 align="center">✨ Steamedit Enhance ✨</h1>

<p align="center">
  <strong>本地 Steam 游戏库编辑工具</strong><br/>
  重命名游戏 · 自定义排序 · 管理启动配置 · AI 翻译游戏名称
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows%2010%2F11-blue?style=flat-square" alt="Platform" />
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet" alt=".NET 10" />
  <img src="https://img.shields.io/badge/UI%20Languages-English%20%7C%20简体中文%20%7C%20繁體中文-orange?style=flat-square" alt="Languages" />
  <img src="https://img.shields.io/badge/License-Proprietary-red?style=flat-square" alt="License" />
</p>

---

## 📖 简介

**Steamedit Enhance** 是一款 Windows 桌面应用，用于编辑 Steam 本地的 `appinfo.vdf` 文件。

你可以自定义游戏显示名称、排序方式、隐藏/完成状态、启动参数、控制器支持等——**所有修改仅作用于本地 Steam 游戏库，不影响 Steam 云端数据。**

> **基于原创 [SteamEdit](https://steamedit.tg-software.com/) 的 .NET 10 迁移增强版。**

---

## ✨ 功能一览

### 🎮 游戏管理

| 功能 | 说明 |
|:---|:---|
| 游戏重命名 | 自定义游戏在 Steam 库中的显示名称 |
| 排序修改 | 设置自定义排序名称，改变游戏列表位置 |
| 隐藏 / 完成 | 将游戏标记为隐藏或已完成 |
| 启动配置 | 编辑启动可执行文件、参数和工作目录 |
| 条目类型 | 更改游戏类型（Game / DLC / Demo 等） |
| 控制器支持 | 设置控制器支持级别（None / Partial / Full） |
| VR 分类 | 设置 VR 分类（Default / Removed） |

### 🔧 效率工具

| 功能 | 说明 |
|:---|:---|
| **自定义排序** | 按名称或 App ID 排序，支持升序/降序，偏好自动持久化 |
| **批量查找替换** | 支持纯文本与正则表达式，实时匹配计数 |
| **AI 翻译** | OpenAI 兼容 API，单条/批量翻译，**带术语库缓存保证一致性** |
| **Web 搜索** | 一键搜索「游戏名 + 翻译名」，支持多搜索引擎 |

### 💪 体验优化

| 功能 | 说明 |
|:---|:---|
| **多语言 UI** | 英文 / 简体中文 / 繁体中文，设置中即时切换，无需重启 |
| **DPI 感知** | PerMonitorV2 高 DPI 支持，4K 显示器清晰不模糊 |
| **异步加载** | `appinfo.vdf` 异步加载，带进度对话框和 30 秒超时保护 |
| **筛选防抖** | 搜索输入 500ms 防抖，避免频繁刷新列表 |
| **崩溃日志** | 未处理异常时自动生成诊断日志，方便排查问题 |

---

## 📋 系统要求

- Windows 10 / 11 (x64)
- 已安装 Steam 客户端

> **独立构建** 无需 .NET 运行时即可运行。<br/>
> **依赖框架构建** 需要 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)。

---

## 🚀 快速开始

### 首次使用

1. 下载并运行 `steamedit-Enhance.exe`
2. 程序自动通过注册表查找 Steam 路径
3. 若未找到，手动选择 `appinfo.vdf`（默认路径：`C:\Program Files (x86)\Steam\appcache\appinfo.vdf`）

### 编辑工作流

1. 从左侧列表选择一个游戏
2. 在右侧修改名称、排序键等字段
3. 点击 **💾 保存 → 🔄 应用 → 🔄 重启 Steam** 使更改生效

### AI 翻译

1. 打开 **设置** → 填入 AI API 端点（如 `https://api.openai.com/v1`）和 API Key
2. 点击 **获取模型列表** 选择模型
3. 选择目标语言，点击 **AI 翻译**——结果自动填入
4. 已修改的游戏名称自动缓存到术语库，保持后续翻译一致性

> 兼容 OpenAI Chat Completions API 的任何服务（OpenAI、Claude API 代理、本地 Ollama 等）。

### 命令行模式

```bash
# 静默应用待处理更改（无 GUI）
steamedit-Enhance.exe -autofix

# 应用更改并重启 Steam
steamedit-Enhance.exe -autofix -forcestart
```

适用于计划任务定期维护。

---

## 🔧 开发构建

```bash
# 克隆仓库
git clone https://github.com/<your-repo>/steamedit.git
cd steamedit

# 开发构建
dotnet build SteamEdit.csproj

# 运行测试
dotnet test SteamEditTests/SteamEditTests.csproj

# 运行单个测试
dotnet test SteamEditTests/SteamEditTests.csproj --filter "<测试完全限定名>"

# 发布独立单文件 EXE（~110 MB，无需运行时）
dotnet publish SteamEdit.csproj -c Release

# 发布依赖框架版本（~650 KB，需要 .NET 10 运行时）
dotnet publish SteamEdit.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=true

# 开发模式运行
dotnet run --project SteamEdit.csproj
```

发布输出位于 `bin/Release/net10.0-windows/win-x64/publish/`，文件名为 `steamedit-Enhance.exe`。

---

## 🌐 国际化

支持即时切换语言，无需重启。

| 语言 | 资源文件 |
|:---|:---|
| English | `Strings.resx`（默认） |
| 简体中文 | `Strings.zh-CN.resx` |
| 繁體中文 | `Strings.zh-TW.resx` |

**添加新语言：** 复制 `Strings.resx` 为 `Strings.{culture}.resx`，翻译所有 `<value>` 标签，并在 `SettingsDlg.cs` 的语言列表中添加选项。

---

## 📁 项目结构

```
steamedit/
├── SteamEdit/                      # 🖥️ WinForms UI 层
│   ├── Program.cs                  # 入口点 + 全局异常处理
│   ├── MainWindow.cs               # 主窗口
│   ├── GameListManager.cs          # 游戏列表：搜索/筛选/排序
│   ├── EditController.cs           # 属性编辑/撤销/还原
│   ├── I18n.cs                     # 语言管理器
│   └── Resources/
│       └── Strings*.resx           # 多语言资源
├── SteamEdit.Data/                 # 📦 核心数据模型与 I/O
│   ├── SteamData.cs                # 数据加载/保存/应用
│   ├── PropertyTable.cs            # 属性表（Dictionary 优化 + 哈希缓存）
│   ├── Mods.cs                     # mods.dat 读写
│   └── ModifiedEntry.cs            # 修改条目模型
├── SteamEdit.Data.Impl/            # 🔬 appinfo.vdf 版本解析器
│   ├── AppInfoBase.cs              # 容器基类
│   └── AppInfoEntryBase.cs         # 条目基类
├── Common/                         # 🛠️ 共享工具
│   ├── Settings.cs                 # 用户设置（JSON 序列化）
│   ├── AiTranslator.cs             # AI 翻译器（OpenAI 兼容）
│   └── TranslationGlossary.cs      # 术语缓存
└── SteamEditTests/                 # ✅ MSTest 单元测试（155+ 个）
```

---

## 🧰 技术栈

| 技术 | 详情 |
|:---|:---|
| .NET | 10.0 |
| 语言 | C# 13+（文件范围命名空间、模式匹配、表达式体成员） |
| UI 框架 | Windows Forms（PerMonitorV2 DPI） |
| 目标平台 | win-x64 |
| 发布方式 | 单文件 EXE（独立或依赖框架） |
| 测试框架 | MSTest |

---

## 📜 许可与声明

- **原创软件版权**归 [Tim Green](mailto:tim@tg-software.com) 所有
- **原创下载：** [steamedit.tg-software.com](https://steamedit.tg-software.com/)
- 本仓库仅涉及 **技术迁移**（.NET Framework → .NET 10）与 **功能增强**
- 如认为权益受到侵犯，请 [提交 Issue](https://github.com/<your-repo>/steamedit/issues) 处理
