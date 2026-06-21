# steamedit-Enhance 代码审查报告

> 审查日期：2026-06-21
> 审查范围：全部模块（SteamEdit.Data / SteamEdit.Data.Impl / Common / SteamEdit UI）
> 测试状态：120 个测试全部通过

---

## 🔴 严重问题（需优先处理）

### ~~1. `SHA1` 哈希写入但从未校验~~ ✅ 已降级为建议

> 已移至 🟡 改进建议 #26。哈希覆盖的数据格式（字符串表示 vs 二进制数据）在读取时无法直接重算，且 Steam 自身会验证哈希。添加读取时校验的实现复杂度高，收益有限。

---

### ~~2. `AppInfoPaths` — 可变静态数组~~ ✅ 已降级为建议

> 已移至 🟡 改进建议 #25。由于 `params string[]` 签名约束，修改涉及 30+ 调用点，风险收益比不高。

---

## 🟡 改进建议

### 1. `SteamData` — 全静态可变状态，无线程同步

**文件:** `SteamEdit.Data/SteamData.cs` 全文

`_initProps`、`_modData`、`_needsSave`、`_needsApply` 等所有字段都是 static，且无任何同步机制。`NeedsSave` 的 setter 触发事件 `OnNeedsSave?.Invoke()`，如果多个线程同时读写，存在数据竞争。

**收益:** 消除多线程环境下的数据损坏风险。

**实现路径:** 对 `NeedsSave`/`NeedsApply` 使用 `volatile` 或 `Interlocked`；对 `_modData` 访问加锁或使用不可变引用模式。

---

### 2. `PropertyTable` — 部分锁定，数据结构未受保护

**文件:** `SteamEdit.Data/PropertyTable.cs`

`_cacheLock` 仅保护 `_propertyNamesCache` 和 `_hashCode`，但 `_properties` 列表和 `_propertyMap` 字典的读写完全没有同步。`Equals` 方法锁定 `this._cacheLock` 但读取 `other._propertyMap` 时未加锁。

**收益:** 防止并发修改导致的 `InvalidOperationException` 或数据损坏。

**实现路径:** 使用 `ReaderWriterLockSlim` 保护所有对 `_properties`/`_propertyMap` 的访问。

---

### 3. `PropertyTable.Equals` — 只锁定 `this`，不锁定 `other`

**文件:** `SteamEdit.Data/PropertyTable.cs:291-298`

**收益:** 消除并发比较时的竞态条件。

**实现路径:** 对两个对象使用一致的锁定顺序（如按哈希码排序）以避免死锁。

---

### 4. `TranslationGlossary.Save()` — 锁内执行同步 I/O

**文件:** `Common/TranslationGlossary.cs:187-207`

`Save()` 在 `_lock` 内执行 `File.WriteAllText` 和 `File.Move`，阻塞所有其他调用者。

**收益:** 减少锁竞争，提升批量翻译性能。

**实现路径:** 将脏数据快照到锁外，然后在锁外执行 I/O。

---


### 5. `AiTranslator` — 无重试/退避机制

**文件:** `Common/AiTranslator.cs:116-125`

所有 HTTP 调用没有重试逻辑。HTTP 429（限流）或瞬态网络错误会直接传播为异常。批量翻译场景下尤其影响可靠性。

**收益:** 显著提升批量翻译成功率。

**实现路径:** 添加指数退避重试（3 次，间隔 1s/2s/4s），区分可重试错误（429、503、网络超时）和不可重试错误（401、400）。

---

### 6. `AiTranslator` — API 密钥明文存储

**文件:** `Common/Settings.cs` 中 `AiApiKey` 属性

API 密钥以明文存储在 `Settings.json` 中。任何能读取该文件的程序都可以获取密钥。

**收益:** 提升安全性。

**实现路径:** 使用 Windows DPAPI 或 `System.Security.Cryptography.ProtectedData` 加密存储。

---

### 7. `AppInfoEntryBase.RevertModifiedProperty` — 每次反序列化整个原始数据

**文件:** `SteamEdit.Data.Impl/AppInfoEntryBase.cs:229-249`

每次还原单个属性都创建 `MemoryStream` 并解析完整的 `OriginalData` 属性表。对于大型条目，这是 O(N) 操作。

**收益:** 大幅提升频繁还原操作的性能。

**实现路径:** 缓存解析后的原始 `PropertyTable`，在 `Modification` 设置时一次性解析。

---


### 8. `AppInfoEntryBase.Launches` — 每次访问创建新对象

**文件:** `SteamEdit.Data.Impl/AppInfoEntryBase.cs:194-195`

```csharp
get => new LaunchTable(GetLaunchPropertyTable());
```

不像 `Name`/`SortAs`/`Type` 有缓存，`Launches` 每次 getter 都创建新的 `LaunchTable`。

**收益:** 减少 UI 频繁访问时的 GC 压力。

**实现路径:** 添加 `_cachedLaunches` 字段，在 `ClearCachedProps()` 时清除。

---

### 9. `WaitingDialog.ShowAndWait` — `Application.DoEvents` 重入风险

**文件:** `SteamEdit/WaitingDialog.cs:65-82`

使用 `DoEvents` 循环泵送消息，`_pumpingMessages` 布尔值非线程安全，且无法防止事件处理器触发新的 `ShowAndWait` 调用。

**收益:** 消除 UI 重入导致的未定义行为。

**实现路径:** 改用 `async/await` 模式，或使用 `IMessageFilter` 替代 `DoEvents` 循环。

---

### 10. `Program.cs` — 任务取消后竞态条件

**文件:** `SteamEdit/Program.cs:86-88`

```csharp
task.Wait(TimeSpan.FromSeconds(5));
return false;
```

5 秒超时后返回 `false`，但后台线程可能仍在运行并修改 `SteamData` 状态。

**收益:** 消除取消后的数据竞争。

**实现路径:** 使用 `CancellationTokenSource` 取消后台任务，而不是仅等待超时。

---


### 11. `UndoManager` — 无撤销栈深度限制

**文件:** `Common/UndoManager.cs`

撤销栈无限增长，每个 `UndoRecord` 持有 `IAppInfoEntry` 引用和旧值。长时间编辑会话可能消耗大量内存。

**收益:** 控制内存使用。

**实现路径:** 设置最大深度（如 100），超出时移除最旧的记录。

---

### 12. `BatchReplaceDialog` — 批量替换无撤销支持

**文件:** `SteamEdit/BatchReplaceDialog.cs:390-428`

`ReplaceAll()` 直接修改 `entry.Name`，不经过 `UndoManager`。与单条编辑不同，批量替换不可逆。

**收益:** 防止用户误操作导致不可恢复的数据丢失。

**实现路径:** 替换前将所有修改条目推入撤销栈，或在执行前显示确认对话框。

---

### 13. `Settings.SaveJson` — 临时文件替换在 Windows 上非原子

**文件:** `Common/Settings.cs:408-410`

```csharp
File.WriteAllText(tempPath, json);
File.Move(tempPath, path, overwrite: true);
```

Windows 上 `File.Move(overwrite: true)` 非原子。进程崩溃可能导致设置文件丢失。

**收益:** 保护用户设置不丢失。

**实现路径:** 使用 `File.Replace`（NTFS 原子操作）。

---

### 14. `SteamData.Save` — 备份恢复本身可能失败

**文件:** `SteamEdit.Data/SteamData.cs:167-172`

```csharp
catch (Exception ex)
{
    File.Copy(text, ModInfoPath, overwrite: true);  // 这也可能失败
    throw;
}
```

如果备份恢复也失败（如 `.bak` 文件被锁定），原始异常被覆盖，mods 文件可能处于不可恢复状态。

**收益:** 提升数据安全性。

**实现路径:** 备份恢复包裹在独立 try-catch 中，失败时记录日志并提示用户手动恢复。

---

### 15. `SettingsDlg.ApplyAutoStartRegistry` — Checkbox 变更立即写入注册表

**文件:** `SteamEdit/SettingsDlg.cs:323,333,344`

每次 checkbox 变更立即写入注册表。如果用户点击"取消"，代码尝试回滚，但如果回滚写入失败，用户会得到意外的注册表变更。

**收益:** 避免取消操作后的不一致状态。

**实现路径:** 只在用户点击"确定"时批量写入注册表。

---

### 16. `EditController._listEntryType.SelectedItem` — 未检查 null

**文件:** `SteamEdit/EditController.cs:391`

```csharp
entry.Type = (AppInfoEntryType)_listEntryType.SelectedItem;
```

如果 `SelectedItem` 为 null（如语言切换后条目类型不匹配），unboxing null 到值类型会抛出 `NullReferenceException`。

**收益:** 防止语言切换时的 UI 崩溃。

**实现路径:** 添加 null 检查：`if (_listEntryType.SelectedItem is AppInfoEntryType type) entry.Type = type;`

---

### 17. `GameListManager.SortEntries` — 每次排序分配新 Dictionary

**文件:** `SteamEdit/GameListManager.cs:315-316`

每次筛选/排序都创建一个与条目数等大的 Dictionary。对于数千个游戏的库，这是不必要的 GC 压力。

**收益:** 减少排序时的内存分配。

**实现路径:** 预分配或复用 Dictionary；或在 `SortAs` 变更时预计算归一化键并缓存在条目上。

---

### 18. `MainWindow` — 静态事件导致内存泄漏风险

**文件:** `SteamEdit/MainWindow.cs:132-133`

`SteamData.OnNeedsSave` 和 `SteamData.OnNeedsApply` 是静态事件。虽然 `FormClosing` 中取消订阅，但如果窗体未正常关闭（如异常终止），处理器不会被移除，`MainWindow` 实例永远不会被 GC。

**收益:** 消除潜在内存泄漏。

**实现路径:** 在 `Dispose` 方法中也取消订阅，或使用弱事件模式。

---

### 19. `Settings._values` — 无线程安全

**文件:** `Common/Settings.cs`

`_values` 字典、`_isDirty` 标志和所有属性访问器无任何同步。后台线程读取设置时可能与 UI 线程写入冲突。

**收益:** 消除数据竞争。

**实现路径:** 使用 `ConcurrentDictionary` 或在访问时加锁。

---

### 20. `LaunchTable.Items` — 每次枚举重新执行 LINQ 查询

**文件:** `SteamEdit.Data/LaunchTable.cs:16`

`Items` 属性是 LINQ 查询，每次枚举都重新执行过滤、排序、投影。

**收益:** 避免重复计算。

**实现路径:** 缓存结果为 `IReadOnlyList<LaunchItem>`，在属性表变更时清除缓存。

---

## 🟢 性能优化

### 1. `AppInfoEntryBase.RevertModifiedProperty` — 缓存解析后的原始属性表

**优化点:** 每次还原单个属性都反序列化完整的 `OriginalData`。

**预期收益:** 对频繁还原操作，性能提升 O(N) → O(1)（摊销）。

**实现概要:** 在 `ModifiedEntry` 中添加 `_parsedOriginalCache` 字段，首次访问时解析，后续直接查找。

---

### 2. `PropertyTable.GetHashCode` — XOR 哈希的同值抵消问题

**优化点:** XOR 组合导致相同哈希值的属性互相抵消（A ^ A = 0）。

**预期收益:** 减少哈希冲突，提升集合操作性能。

**实现概要:** 改用 `(hash * 397) ^ property.GetHashCode()` 或 `HashCode.Combine`。

---

### 3. `AppInfoBase.Save` — 流式写入替代全量缓冲

**优化点:** 当前整个文件写入 MemoryStream 再复制到文件。

**预期收益:** 峰值内存减少约 50%（100MB+ 文件场景）。

**实现概要:** 直接传入 `FileStream` 作为 `BinaryWriter` 的底层流。

---

### 4. `TranslationGlossary.Save()` — 锁外执行 I/O

**优化点:** 磁盘写入在锁内执行，阻塞所有读取者。

**预期收益:** 批量翻译期间的锁竞争显著降低。

**实现概要:** 在锁内快照脏数据，锁外执行文件写入。

---

### 5. `AiTranslator` — 无 SSE 流式支持

**优化点:** 当前等待完整响应后才返回，AI 推理延迟期间用户无反馈。

**预期收益:** 翻译延迟感知降低，用户体验提升。

**实现概要:** 使用 `HttpCompletionOption.ResponseHeadersRead` + 逐行读取 SSE 流，支持进度回调。

---

## 🔵 简化建议

### 1. `AppInfoEntry_0x07564427/28/29` — 版本实现代码重复率 ~90%

**简化点:** 三个版本的 `FromReader`/`Write` 方法结构几乎完全相同，仅属性表读取委托和哈希块数量不同。

**收益:** 消除约 200 行重复代码，新增版本只需配置差异参数。

**实现概要:** 在 `AppInfoEntryBase` 中实现通用的 `FromReader(reader, readPropertyTable, hashBlockCount)` 模板方法，各版本子类仅提供委托和参数。

---

### 2. `AppInfo_0x07564427/28/29` — 容器类同样重复

**简化点:** 三个 `AppInfo_0x...` 的 `Load` 方法结构完全相同，仅 magic number 和 entry 类型不同。

**收益:** 消除约 60 行重复代码。

**实现概要:** 在 `AppInfoBase<TEntry>` 中实现通用 `Load` 方法，子类仅提供 magic number 常量。

---

### 3. `Settings` 属性样板代码

**简化点:** ~25 个属性，每个都有相同的 `Get<T>`/`Set<T>` + `[DefaultValue]` 模式。

**收益:** 减少约 200 行样板代码。

**实现概要:** 使用 `CallerMemberName` + 字典模式，或使用 C# Source Generator 自动生成属性。

---

### 4. `EditController`/`BatchTranslateDialog` — `InvokeRequired`/`BeginInvoke` 重复模式

**简化点:** `BatchTranslateDialog` 中 5+ 处重复的 `if (InvokeRequired) BeginInvoke(...)` 模式。

**收益:** 减少样板代码，统一异常处理。

**实现概要:** 提取 `void InvokeOnUI(Action action)` 辅助方法。

---

### 5. `MainWindow` 保存/应用错误处理重复

**简化点:** `OnBtnSave_Click`、`OnBtnSaveApply_Click`、`OnBtnSaveApplyRestart_Click` 中相同的 try-catch 模式。

**收益:** 统一错误处理逻辑，减少代码重复。

**实现概要:** 提取 `bool TrySave()` 和 `bool TryApply()` 辅助方法。

---

### 6. `MainWindow.OnBtnShortcuts_Click` — 程序化构建快捷键对话框

**简化点:** 在代码中手动构建整个对话框（518-544 行），使用硬编码尺寸。

**收益:** 与其他对话框保持一致，便于维护和本地化。

**实现概要:** 提取为独立的 `ShortcutsDialog` 窗体类。

---

## 📊 总结评分

| 维度 | 评分 | 说明 |
|:---|:---:|:---|
| 代码质量 | **7/10** | 整体编码规范良好，C# 13+ 特性运用得当。但存在 null 注解不一致（`null!` 滥用）、部分构造函数绕过校验等问题。大量 CS8618/CS8602/CS8622 编译警告表明 nullable 上下文尚未完全清理。 |
| 架构设计 | **8/10** | 四层分层清晰，委托注入消除格式分支，版本多态设计优雅。但 `SteamData` 全静态单例不利于测试隔离，`AppInfoEntryBase.DisplaySettings` 静态全局状态增加耦合。 |
| 测试覆盖 | **7/10** | 120 个测试覆盖核心数据层，BinaryParsingHardeningTests 质量较高。但 UI 层零测试，AI 翻译零测试，`ModifiedEntry.ApplyChanges`、`RevertModifiedProperty`、`Launches` 等关键路径缺少测试。 |
| 性能表现 | **7/10** | 已有 O(1) 属性查找、哈希缓存、预计算排序键等优化。但 `AppInfoBase.Save` 全量内存缓冲、`RevertModifiedProperty` 每次全量反序列化、`TranslationGlossary` 锁内 I/O 是显著瓶颈。 |
| 代码简洁度 | **7/10** | 已进行过一轮 DRY 重构（消除 ~1000 行重复），但 Impl 层三个版本仍有 ~90% 重复，Settings 属性样板冗长，`InvokeRequired` 模式重复出现。 |

**总体评价:** 代码库整体质量高于平均水平。架构分层合理，关键数据结构有性能优化，防御性编程（二进制解析深度限制、边界检查）做得较好。主要风险集中在数据安全性（非原子写入、无 Apply 错误处理）、线程安全（静态状态无同步）和异常处理（async void、未捕获异常）三个维度。建议优先处理 🔴 严重问题中的 #3（非原子写入）、#5（Apply 无错误处理）、#7（async void 异常），这些是实际可能导致数据丢失或应用崩溃的问题。
