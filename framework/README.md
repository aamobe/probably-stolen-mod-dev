# Probably Stolen Mod 骨架

这是基于官方 `IMod` 和游戏随附 Harmony 的源码模板，目标为 `netstandard2.1`。使用具备 .NET Standard 2.1 引用包的 .NET SDK 8 或更高版本。

1. 将本目录中的项目文件、`manifest.xml` 和 `src/` 复制到新 Mod 目录。需要保留骨架回归时，同时复制 `checks/`。
2. 设置清单中的稳定 ID、名称、作者、版本和目标游戏版本。若重命名项目或程序集，同步更新清单中的 DLL 名。保留检查工程时，同步替换其中引用的源码命名空间。清单的 `ModVersion` 是骨架运行时日志读取的版本来源。
3. 从通用模板成对适配 `AGENTS.md` 和《总方案》，填入实际目标版本、构建命令、阶段和发布基线。接入前核对目标游戏程序集与官方接口。
4. 在项目根目录编译。游戏引用仅供编译，构建不会部署或打包。

   ```powershell
   dotnet build ./ProbablyStolenModTemplate.csproj -c Release '-p:GameManaged=<游戏目录>/Probably Stolen_Data/Managed'
   ```

## 接入第一个功能

在 `src/` 下新增一个同命名空间的 `partial class ModEntry`。实现 `InstallCore(RuntimeLifetime lifetime, Harmony harmony)` 安装主菜单阶段的订阅或补丁，实现 `InstallAssets(RuntimeLifetime lifetime, LoadedMod mod)` 安装依赖本 Mod 资源的模块。用 `lifetime.Own(acquire, release)` 登记成对操作。局内数据仍从对应的官方读档事件开始访问。补丁使用传入的 Harmony 实例，使停用只清理由本 Mod ID 持有的补丁。

- `RuntimeLifetime` 负责分阶段获取、逆序释放和失败回滚。释放失败后需重启游戏再重新启用。
- `OwnedCallbacks` 可在模块内持有事件租约。由模块将 `ReleaseFailed` 传回运行期释放路径。
- `ExtensionEvent` 提供主线程具名订阅和故障隔离。用 `new ExtensionEvent("事件名", _diagnostics.Record)` 接入诊断。
- `WriteDiagnostics()` 可绑定到新 Mod 自己的维护菜单或命令，将只读状态与有界故障计数写入 `Player.log`。

物品注册、读档与库存恢复使用各项目自己的 ID、存档结构和目标版本适配。接入前查阅[框架与维护接口](../docs/框架与维护接口.md)的验证场景。

## 验证

在本目录运行 `dotnet run --project ./checks/Checks.csproj` 可验证启停、资源事件筛选、回滚和事件诊断。检查使用游戏边界替身。游戏内还需确认启用日志、资源就绪日志、停用清理和真实功能链路。

生命周期、回调租约和事件结构取自 CorpPresence 发布源码 `19cbbf9`，改为骨架自己的命名空间和诊断入口。 [来源提交](https://github.com/aamobe/CorpPresence/commit/19cbbf9831e0143123eb7501032951de8e0e19f6)
