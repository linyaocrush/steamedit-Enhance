<p align="center">
  <img src="app.ico" alt="steamedit-Enhance Logo" width="120" />
</p>

<h1 align="center">✨ Steamedit Enhance ✨</h1>

<p align="center">
  <strong>本地 Steam 游戏库编辑器</strong><br/>
  重命名游戏 · 自定义排序 · 启动配置 · AI 翻译
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Windows-10%2F11-blue?style=flat-square&logo=windows" alt="Platform" />
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet" alt=".NET 10" />
  <img src="https://img.shields.io/badge/Tests-342%20passing-2ea043?style=flat-square" alt="Tests" />
  <img src="https://img.shields.io/badge/UI-EN%20%7C%20ZH--CN%20%7C%20ZH--TW-orange?style=flat-square" alt="Languages" />
  <img src="https://img.shields.io/badge/License-Proprietary-red?style=flat-square" alt="License" />
</p>

---

## 📖 简介

**Steamedit Enhance** 是一款 Windows 桌面应用，直接编辑 Steam 本地缓存文件 `appinfo.vdf`。

> 自定义游戏名称、排序、隐藏状态、启动配置——**所有修改仅影响本地 Steam 库，不动云端数据。**

> 基于 [SteamEdit](https://steamedit.tg-software.com/) 的 .NET 10 迁移增强版。

---

## ✨ 功能

### 🎮 游戏管理

| 功能 | 说明 |
|:---|:---|
| 重命名 | 自定义游戏在 Steam 库中的显示名称 |
| 排序 | 设置排序键，改变游戏列表位置（支持自然排序，自动去除 The/An/A） |
| 隐藏 / 完成 | 标记游戏隐藏或已完成 |
| 启动配置 | 编辑启动可执行文件、参数、工作目录 |
| 条目类型 | 更改类型（Game / DLC / Demo / Tool 等） |
| 控制器 | 设置控制器支持级别（None / Partial / Full） |
| VR 分类 | 移除 VR 分类标识 |

### 🔧 效率工具

| 功能 | 说明 |
|:---|:---|
| 批量查找替换 | 纯文本与正则表达式，实时匹配计数 |
| AI 翻译 | OpenAI 兼容 API，单条/批量翻译，**术语库缓存保证一致性** |
| Web 搜索 | 一键搜索游戏名+翻译名，多引擎支持 |
| 撤销 | 多级撤销（上限 500 步），支持复合操作（如 VR 同时改两属性） |
| 批量操作 | 批量重命名、改类型、标记隐藏/完成、重置 |
| 日志系统 | 可配置 Debug/Info/Warning/Error 级别，过滤调试信息 |

### ⚡ 性能

| 优化 | 说明 |
|:---|:---|
| **原生 Rust 内核** | `appinfo.vdf` 属性表编解码由 Rust 实现（`native/steamedit-core`，cdylib + P/Invoke）。旧版 `0x00000000` 格式解码 **约 1.8×** 于托管实现；原生库缺失时自动回退到托管实现 |
| **惰性 originalData** | 加载时不再对每个条目做“深拷贝 + 全量序列化”，只保留原始字节，首次修改时才惰性生成 → **加载快约 25–30%** |
| Memory-Mapped I/O | appinfo.vdf 通过内存映射文件加载，降低内存占用与启动延迟 |
| 异步排序 | 搜索/筛选/排序在后台线程执行，UI 不卡顿 |
| 防抖筛选 | 输入 500ms 防抖 + 200ms 状态栏防抖，避免频繁刷新 |
| DrawItem 缓存 | 列表项绘制复用 GraphicsPath，减少 GC 压力 |
| 流式哈希 | 条目写入时流式计算 SHA1，消除中间大字符串分配 |
| 增量属性缓存 | PropertyTable 属性名缓存只在结构变更时失效 |

### 🌐 其他

| 功能 | 说明 |
|:---|:---|
| 多语言 UI | 英文 / 简体中文 / 繁体中文，即时切换，无需重启 |
| DPI 感知 | PerMonitorV2，4K 清晰不模糊 |
| 崩溃日志 | 自动生成 `crash-{timestamp}.log`，附诊断信息 |
| 自动修复 | 支持 `-autofix` 命令行静默模式，适用于计划任务 |
| 日志系统 | 可配置 Debug/Info/Warning/Error 级别，即时生效，适合调试与生产 |

---

## 📋 系统要求

- **Windows 10 / 11** (x64)
- 已安装 Steam 客户端

| 发布方式 | 大小 | 需要 .NET 运行时 |
|:---|:---|:---|
| 独立单文件 | ~110 MB | ❌ 不需要 |
| 依赖框架 | ~650 KB | ✅ 需要 .NET 10 Desktop Runtime |

---

## 🚀 快速开始

### 首次使用

1. 下载并运行 `steamedit-Enhance.exe`
2. 自动通过注册表查找 Steam 路径；若未找到，手动选择 `appinfo.vdf`
   > 默认路径：`C:\Program Files (x86)\Steam\appcache\appinfo.vdf`

### 编辑工作流

```
1. 左侧列表选游戏 → 2. 右侧改属性 → 3. 保存 → 应用 → 重启 Steam
```

### AI 翻译

1. **设置 → AI**：填入 API 端点与 Key，选择模型
2. 选中游戏 → **AI 翻译** → 结果自动填入
3. 已翻译条目缓存到术语库，后续翻译保持一致

> 兼容 OpenAI Chat Completions API（OpenAI、Claude 代理、本地 Ollama 等）。

### 日志级别配置

1. **设置 → 常规**：调整日志级别下拉框
2. 可选择 Debug/Info/Warning/Error 级别
3. **变更立即生效**：无需重启应用
4. 调试时设为 Debug，生产环境建议设为 Info 或 Warning

```bash
# 静默应用待处理更改
steamedit-Enhance.exe -autofix

# 应用更改并强制重启 Steam
steamedit-Enhance.exe -autofix -forcestart
```

---

## 🛠️ 开发

```bash
# 构建
dotnet build steamedit-Enhance.csproj

# 测试（342 个）
dotnet test steamedit-EnhanceTests/steamedit-EnhanceTests.csproj

# 只跑性能探针（会读取真实 appinfo.vdf，缺失则跳过）
dotnet test steamedit-EnhanceTests/steamedit-EnhanceTests.csproj --filter "TestCategory=Perf"

# 运行
dotnet run --project steamedit-Enhance.csproj

# 发布独立 EXE（~110 MB，Release 构建自动混淆）
dotnet publish steamedit-Enhance.csproj -c Release

# 发布依赖框架（~650 KB，Release 构建自动混淆）
dotnet publish steamedit-Enhance.csproj -c Release -p:SelfContained=false -p:PublishSingleFile=true
```

> **原生内核**：`dotnet build` 会自动调用 `cargo build --release`（见 `steamedit-Enhance.csproj` 的
> `BuildRustCore` 目标），并把 `steamedit_core.dll` 复制到输出目录。未安装 Rust 工具链或
> 构建失败不会中断构建，程序运行时会回退到托管实现；也可用 `STEAMEDIT_DISABLE_NATIVE=1`
> 强制使用托管实现。手动构建：`cargo build --release --manifest-path native/steamedit-core/Cargo.toml`。

发布输出：`bin/Release/net10.0-windows/win-x64/publish/steamedit-Enhance.exe`

> **代码混淆**：Release 构建自动运行 [Obfuscar](https://github.com/obfuscar/obfuscar)：
> - 原始 DLL → `bin/Release/net10.0-windows/win-x64/obfuscated/steamedit-Enhance.dll`
> - `PublishSingleFile=true` 时，publish 流程使用混淆后的 DLL 打包单文件 EXE
> - 跳过类型：`Program`（入口点）、`Strings`（资源文件）

---

## 🌐 国际化

| 语言 | 资源文件 |
|:---|:---|
| English | `Strings.resx`（默认） |
| 简体中文 | `Strings.zh-CN.resx` |
| 繁體中文 | `Strings.zh-TW.resx` |

添加新语言：复制 `Strings.resx` → `Strings.{culture}.resx`，翻译 `<value>` 标签，在 `SettingsDlg.cs` 语言列表中添加选项。

---

## 📁 项目结构

```
steamedit-Enhance/
├── steamedit-Enhance/               # 🖥️ WinForms UI
│   ├── Program.cs                   # 入口 + 全局异常处理 + 命令行
│   ├── MainWindow.cs                # 主窗口
│   ├── GameListManager.cs           # 列表搜索/筛选/排序/绘制
│   ├── EditController.cs            # 属性编辑/撤销/还原
│   ├── BatchTranslateDialog.cs      # 批量 AI 翻译（并行）
│   ├── Resources/Strings*.resx      # 多语言资源
│   └── Properties/AssemblyInfo.cs   # InternalsVisibleTo
├── steamedit-Enhance.ViewModels/    # 📐 MVVM ViewModel 层
│   ├── MainViewModel.cs             # 应用状态/命令/窗口标题
│   ├── GameListViewModel.cs         # 搜索/排序/筛选
│   ├── PropertyEditorViewModel.cs   # 属性绑定 + 撤销管理
│   ├── BatchTranslateViewModel.cs   # 并行翻译/暂停/取消
│   ├── ViewModelBase.cs             # INotifyPropertyChanged
│   └── RelayCommand.cs              # ICommand 实现
├── steamedit-Enhance.Data/          # 📦 数据模型与 I/O
│   ├── SteamData.cs                 # 加载/保存/应用协调
│   ├── PropertyTable.cs             # 属性表（Dictionary O(1) + 缓存）
│   ├── PropertyTableIO.cs           # 三格式序列化（委托注入）
│   ├── Mods.cs                      # mods.dat 读写
│   └── UndoManager.cs               # 多级撤销
├── steamedit-Enhance.Data.Impl/     # 🔬 版本特定解析器
│   ├── AppInfoBase.cs               # 泛型容器基类
│   └── AppInfoEntryBase.cs          # 条目基类（读/写/修改）
├── Common/                          # 🛠️ 共享工具
│   ├── Settings.cs                  # JSON 序列化 + 脏标记
│   ├── AiTranslator.cs              # OpenAI 兼容 API 客户端
        │   ├── TranslationGlossary.cs       # 术语缓存
        │   ├── StringTitleComparer.cs       # 自然排序
        │   └── Log.cs                       # 日志系统（多级别过滤）
└── steamedit-EnhanceTests/          # ✅ MSTest 单元测试（342 个）
└── native/steamedit-core/           # 🦀 原生 Rust 内核（appinfo 属性表编解码，cdylib）

 ```

---

## 🧰 技术栈

| 技术 | 详情 |
|:---|:---|
| .NET | **10.0**（最新版） |
| 语言 | C# 13+（文件范围命名空间、模式匹配、`is not`） |
| UI | Windows Forms + PerMonitorV2 DPI |
| 目标 | `win-x64` |
| 发布 | 单文件 EXE（独立或依赖框架） |
| 测试 | MSTest（342 通过，0 失败） |

---

## 📜 许可

- **原创版权** © [Tim Green](mailto:tim@tg-software.com)
- 原创下载：[steamedit.tg-software.com](https://steamedit.tg-software.com/)
- 本仓库仅涉及技术迁移（.NET Framework → .NET 10）与功能增强
- 如有权益问题请 [提交 Issue](https://github.com/your-org/steamedit/issues)
