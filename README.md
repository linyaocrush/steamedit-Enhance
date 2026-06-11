<p align="center">
  <img src="app.ico" alt="SteamEdit Logo" width="128" />
</p>

<h1 align="center">SteamEdit</h1>

<p align="center">
  <strong>Steam 游戏库本地编辑工具</strong><br/>
  重命名游戏、修改排序、管理启动配置、批量设置控制器与 VR 支持
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
| **中英双语** | 支持 English 和简体中文，可在设置中即时切换 |
| **单文件发布** | 编译为单个 EXE 文件，无需安装 |

---

## 环境要求

- Windows 10 / 11（x64）
- .NET 10.0 Desktop Runtime（仅开发构建时需要 SDK）
- Steam 客户端已安装

---

## 构建与运行

```bash
# 克隆仓库
git clone https://github.com/<your-username>/steamedit.git
cd steamedit

# 开发构建
dotnet build SteamEdit.csproj

# 发布单文件 EXE（约 110MB，自包含运行时）
dotnet publish SteamEdit.csproj -c Release

# 运行
dotnet run --project SteamEdit.csproj
```

发布产物位于 `publish_output/` 目录，仅包含 `SteamEdit.exe` 和 `SteamEdit.pdb`。

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

### 命令行模式

```bash
# 自动应用待处理更改（无 GUI）
SteamEdit.exe autofix

# 自动应用并重启 Steam
SteamEdit.exe autofix forcestart
```

适合配合计划任务实现定期自动维护。

---

## 国际化

SteamEdit 内置英语和简体中文两种语言，通过 `.resx` 资源文件管理（共 108 个翻译键）。

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
│   ├── SettingsDlg.cs            # 设置对话框（语言切换）
│   ├── ControllerBatch.cs        # 控制器支持批量修改
│   ├── VRBatch.cs                # VR 类别批量修改
│   ├── I18n.cs                   # 语言管理器
│   └── Resources/
│       ├── Strings.resx          # 英文资源（默认）
│       └── Strings.zh-CN.resx   # 中文翻译
├── SteamEdit.Data/               # 核心数据模型与 I/O
│   ├── SteamData.cs              # 数据加载/保存/应用
│   ├── AppInfoFactory.cs         # 版本分发工厂
│   └── Mods.cs                   # mods.dat 读写
├── SteamEdit.Data.Impl/          # appinfo.vdf 版本实现
│   ├── AppInfo_0x07564427.cs     # 旧版格式
│   ├── AppInfo_0x07564428.cs     # 中版格式
│   └── AppInfo_0x07564429.cs     # 新版格式
├── Common/                       # 共享工具
│   ├── Settings.cs               # 用户设置（XML 序列化）
│   ├── Log.cs                    # 日志
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
| 发布方式 | 单文件自包含 EXE |

---

## 许可与免责

- 原版软件版权归 [Tim Green](mailto:tim@tg-software.com) 所有
- 原版下载地址：[https://steamedit.tg-software.com/](https://steamedit.tg-software.com/)
- 本项目仅进行技术迁移（.NET Framework → .NET 10）和界面汉化
- **如涉及侵权，请 [提交 Issue](../../issues) 联系我删除**
