# SpireLab

《杀戮尖塔 2》C# 角色 Mod。`v1.0.0` 新增原创角色「魔女」，实现规格书的 91 种卡牌、9 件遗物和 3 种药水。提灯测绘师仍作为独立角色可选。

- [魔女 V1.0 规格书](SpireLab_The_Witch_GDD_v1.0.md)
- [魔女构建、验证与已知边界](docs/witch-validation.md)
- [魔女内容索引](docs/witch-content.tsv)
- [测试版内容、改动位置和美术记录](docs/surveyor-preview.md)
- [学习同步](LEARNING_SYNC.md)
- [早期原版机制与角色候选](docs/character-design.md)

## 构建环境

| 组件 | 本机验证版本 |
| --- | --- |
| macOS | 26.5，Apple M5，arm64 |
| Steam 正式版 | `v0.111.1`，build `26655341`，游戏提交 `cc1f9462` |
| 游戏 .NET runtime | `net9.0`，随附 runtime 9.0.7 |
| C# SDK | .NET SDK `10.0.404` LTS，macOS arm64，编译目标为游戏的 `net9.0` |
| 编辑器 | MegaDot `4.5.1-m.14` Mono，Godot.NET.Sdk `4.5.1`。代码编辑使用 VS Code 与 Microsoft C# 扩展 |
| 模板 | `Alchyr.Sts2.Templates 2.5.3`，`alchyrsts2charmod` |
| BaseLib | NuGet 和游戏内均为 `3.4.8`，编译分析器 `0.2.1` |
| 本地程序集检查 | `ilspycmd 11.2.0.10050`，由 .NET 本地工具清单锁定 |
| 版本管理 | Git `2.54.0`，GitHub CLI `2.92.0` |

默认从 Steam 库发现程序集。macOS 优先使用 `SlayTheSpire2.app/Contents/Resources/data_sts2_macos_arm64/sts2.dll`，在缺少 arm64 文件时查找 x86_64 版本。其他库位可用 `-p:Sts2Path="/path/to/Slay the Spire 2"` 覆盖，或传 `-p:Sts2DataDir="..."` 指定程序集目录。Windows 已保留模板的 Steam 路径发现。

[Microsoft .NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) · [MegaDot 下载](https://megadot.megacrit.com/) · [模板 Setup](https://github.com/Alchyr/ModTemplate-StS2/wiki/Setup) · [BaseLib v3.4.8](https://github.com/Alchyr/BaseLib-StS2/releases/tag/v3.4.8)

## 构建和打包

Apple Silicon Mac 安装 Steam 游戏后，从仓库根目录运行。

```sh
./scripts/dev-macos.sh setup
./scripts/dev-macos.sh check
./scripts/dev-macos.sh build
./scripts/dev-macos.sh pack
```

`setup` 按固定版本获取或复用 SDK、MegaDot、模板和 BaseLib，并用 SHA 校验下载的 SDK、编辑器与 BaseLib 安装包。ILSpy 从本地工具清单恢复。项目工具放在 `.local/`。运行 `./scripts/dev-macos.sh code` 可安装 Microsoft C# 扩展并在 VS Code 打开 Mod 工程，运行 `./scripts/dev-macos.sh editor` 可打开 MegaDot 项目。用 `./scripts/dev-macos.sh ilspy -t <完整类型名> <程序集路径>` 检查具体类型。

.NET 10 是构建 SDK，游戏内仍由游戏随附的 .NET 9 runtime 运行。当前项目只导出 PCK，已验证使用匹配的 MegaDot Mono 编辑器即可完成。社区指南中的 Rider 是可选 IDE。[.NET 支持周期](https://dotnet.microsoft.com/platform/support/policy/dotnet-core) · [Microsoft C# 扩展](https://github.com/dotnet/vscode-csharp) · [Godot PCK 文档](https://docs.godotengine.org/en/stable/tutorials/export/exporting_pcks.html)

Windows、Intel Mac 或自定义工具安装可使用对应架构的 .NET 10 SDK 与 MegaDot，运行以下命令并替换编辑器路径。Windows 的编译和运行待验证。

```sh
dotnet tool restore
dotnet build SpireLab/SpireLab.sln
dotnet publish SpireLab/SpireLab.csproj -c ExportRelease -p:GodotPath="/path/to/MegaDot"
```

这里的 `publish` 是本地打包。输出目录为 `SpireLab/bin/mods/ExportRelease/SpireLab/`，加载用到 `SpireLab.dll`、`SpireLab.json` 和 `SpireLab.pck`。PDB 可用于调试。编译时引用的 `sts2.dll` 仅来自用户自己的游戏安装。Debug 输出将它提供给编辑器进行反射，部署目录只复制 Mod DLL、PDB 和清单，PCK 包含资源与本地化。

## GitHub 迭代

私有仓库以 `main` 保存验证过的基线。每次迭代从最新的 `main` 建短期分支，完成本地 `check`、`build` 和 `pack` 后提交并推送。涉及运行行为时，在 PR 中记录目标游戏版本和隔离实机结果。依赖本机 Steam 安装的构建在本地执行，游戏程序集、反编译文件、存档、工具和产物均留在忽略目录。[SpireLab 私有仓库](https://github.com/irin818/spirelab-sts2-mod)

```sh
git switch main
git pull --ff-only
git switch -c codex/your-task
```

## 隔离实机与自动化验证

关闭现有的隔离测试实例后，在仓库根目录执行。

```sh
./scripts/smoke-macos.sh
SPIRELAB_SMOKE_WINDOWED=1 ./scripts/smoke-macos.sh witch
SPIRELAB_SMOKE_WINDOWED=1 ./scripts/smoke-macos.sh witch-save
SPIRELAB_SMOKE_WINDOWED=1 ./scripts/smoke-macos.sh witch-campaign
```

脚本构建 DLL 和 PCK，使用 APFS 克隆创建独立游戏副本，关闭 Steam 集成，并在单独的 Application Support 子目录写入测试配置。它把测试 DLL 放在副本内，启动实际游戏的无窗口模式，验证内容注册、资源、卡牌与药水结算、回合钩子、战斗奖励、存档序列化、商店、休息点和简中本地化。成功时日志会打印 `[SpireLabSmoke] PASS`，脚本也会检查运行期间的错误。脚本输出具体日志路径，测试副本和产物放在 `.local/`。

在窗口模式手测时，把 BaseLib 解压得到的 5 个运行文件与 `SpireLab.dll`、`SpireLab.json`、`SpireLab.pck` 分别放在测试副本的同级子目录 `Contents/MacOS/mods/BaseLib/` 与 `Contents/MacOS/mods/SpireLab/`。通过本地测试配置开启 Mod，以 `--force-steam=off` 启动副本。首次出现 **Load Mods** 时点击启用，退出可能出现的自动重启窗口，再用同一条离线命令启动。详见 [测试版验证清单](docs/surveyor-preview.md)。

## 本机安装与回退

关闭游戏后，将 `SpireLab/` 与匹配的 `BaseLib/` 放入游戏的本地 Mod 目录。Mac 目录在 `SlayTheSpire2.app/Contents/MacOS/mods/`，Windows 目录在游戏安装目录下的 `mods/`。已有相同版本 BaseLib 和 Compatibility Helper 时可复用。打开游戏并按提示启用 Mod，在单人模式新开一局并选择「魔女」。

卸载时把新增的 Mod 文件夹移出本地 Mod 目录。创建的测试存档可以保留。原版游戏二进制和 Steam 存档由游戏自己管理。

角色模板来自 Alchyr，NuGet 声明 MIT。测绘师的形象、场景和卡面为本项目生成。测试版复用游戏的通用界面、部分特效与音效。[Alchyr.Sts2.Templates 2.5.3](https://www.nuget.org/packages/Alchyr.Sts2.Templates/2.5.3)
