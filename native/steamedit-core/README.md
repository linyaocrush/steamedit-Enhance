# steamedit-core

Steamedit Enhance 的原生内核（Rust）。托管侧（C# / WinForms）只保留 UI、对象模型与最薄的编排；
所有“内核”能力——二进制编解码、文件/网络 IO、图像算法、缓存——都在这里实现，并通过
C ABI（`steamedit_core.dll`）以 P/Invoke 暴露。

```
C# (WinForms) ──P/Invoke──▶ steamedit_core.dll (Rust)
   界面 · 对象模型 · 最薄封装        编解码 · 网络 · 图像 · 日志 · 缓存 · 封面解析
```

## 构建

```bash
cargo build --release --manifest-path native/steamedit-core/Cargo.toml
cargo test  --release --manifest-path native/steamedit-core/Cargo.toml
```

主工程 (`.csproj`) 在 `BeforeBuild` 自动调用 `cargo build --release`，并在
`AfterTargets="Build"` / `ComputeFilesToPublish` 把 `steamedit_core.dll` 复制到输出目录。
**内核是必需的**：缺失会直接报错，不存在托管回退。

依赖刻意保持精简：

| crate | 用途 |
|---|---|
| `ureq`（`native-tls` + `gzip`） | 阻塞式 HTTPS；`native-tls` 走 Windows SChannel，无需额外证书包 |
| `serde` / `serde_json` | JSON（术语库 / 名称缓存 / Steam / OpenAI 响应） |

## 内存与错误约定

- **返回值**：`byte*` + `out_len`，缓冲区由内核分配，调用方用 `ste_free(ptr, len)` 释放；
  出错返回 `null`（或 `0`）。
- **错误信息**：`ste_last_error()` 返回 NUL 结尾 UTF-8 字符串（线程本地缓冲，下次调用前有效）。
- **panic 隔离**：所有导出都经 `catch_unwind`，panic 不会跨越 FFI 边界。
- **字符串入参**：均为 NUL 结尾 UTF-8；路径同样以 UTF-8 传递。
- **句柄**：字符串表、热点路径、封面解析器、名称缓存、术语库都通过不透明 `*mut` 句柄传递，
  由对应的 `*_free` 释放（托管侧用 `IDisposable`/终结器包一层）。

## 已导出接口

### appinfo.vdf 编解码
`ste_vdf_to_flat` / `ste_vdf_to_flat_07564429` / `ste_vdf_to_flat_sparse`、
`ste_flat_to_vdf` / `ste_flat_to_vdf_07564429`、
`ste_strtab_new|parse|dump|free`、`ste_pathset_new|free`

### mods.dat
`ste_mods_to_flat` / `ste_mods_from_flat`（v7 长度前缀，旧版无缝升级）

### 网络 (`net.rs`)
`ste_net_cover_download`、`ste_net_official_name`、`ste_net_ai_chat`、`ste_net_ai_models`、
`ste_net_ai_error`（kind：`-1 none / 0 network / 1 api / 2 parse / 3 circuit`）
— 含多源兜底、图片头校验、原子写入、重试退避与熔断器。

### 图像 (`image.rs`)
`ste_image_resize_bgra` — 原始 BGRA8 缓冲的 Lanczos-3（放大）/ 双三次（缩小）重采样。

### 日志 (`logging.rs`)
`ste_log_init|reset|configure|write|flush|close|recent` — 文件句柄、级别过滤、行格式化、尾部读取。

### 封面解析 (`cover.rs`)
`ste_cover_resolver_new|portrait|wide|free` — 自定义 grid → 官方固定名 → 模糊扫描按宽高比识别；
含 PNG/JPEG/GIF/BMP/WebP 文件头尺寸探测与正/负缓存。

### 官方名称缓存 (`namecache.rs`)
`ste_namecache_new|get|contains|set|total|save|clear|free`

### 翻译术语库 (`glossary.rs`)
`ste_glossary_new|initialize|has_app|update|count|find|prompt|flush|free`
— 大小写不敏感索引、原子落盘、`.bak` 回退。

### 通用
`ste_free`、`ste_last_error`

## 迁移状态

**已迁入 Rust（内核）**
- `appinfo.vdf` 属性表编解码、字符串表、稀疏解码 / 热点路径
- `mods.dat` 读写
- 网络：封面下载、官方名称、AI 对话 / 模型列表
- 图像：Lanczos-3 / 双三次重采样核心（GDI+ 仅做解码/编码）
- 日志落盘（`Common/Log`）
- 官方名称显示缓存（`LocalizedNameCache`）
- 翻译术语库（`TranslationGlossary`）
- 本地封面解析（`SteamCoverResolver`）

**仍留在托管侧（非“内核”，或与 UI/平台强耦合）**
- **对象模型**：`PropertyTable` / `ModifiedEntry` / `UndoManager` / `Mods` / `SteamData` /
  `AppInfo*` —— 这些是绑定到 ViewModel、撤销栈与 WinForms 数据绑定的内存对象图；
  其序列化底层（codec）已经在内核。
- **应用配置**：`Settings` / `SettingsData` —— 类型化属性包 + 旧格式反射迁移，属应用状态。
- **平台/操作系统**：`AppHelper`（进程/Uri）、`AutoStartService`（注册表）、
  `SteamProcessManager`（进程）、`ShortcutHandler`（Shell）。
- **本地化/排序**：`SteamLanguages`、`EnumMappings`、`StringTitleComparer`
  （依赖 .NET 的 `CultureInfo` / `StringComparer.CurrentCulture` 平台排序）。
- **UI**：`ViewModels`、窗体/对话框、绘制、主题。

后续如需继续下沉，应按“先把对象模型改为句柄式包装、再迁移计算”的顺序推进，
每一步都保持构建绿色并以原生 + 托管双层测试覆盖。
