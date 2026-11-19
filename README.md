# SpireLab

《杀戮尖塔 2》原创角色 Mod 的 Phase A 工程。实验角色有 70 点初始生命、5 张原版打击、5 张原版防御、1 张校准打击和原版初始遗物 Burning Blood。校准打击耗费 1 点能量，造成 8 点伤害，升级为 11 点。角色外观、图标与战后奖励池均为开发占位。

- [学习同步](LEARNING_SYNC.md)
- [原版机制与原创候选](docs/character-design.md)

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

## 在 Mac 上复现游戏测试

下面的步骤将 Steam 游戏克隆到 `.local/game-smoke/`，在副本中增加 Mod 文件与 Godot 的 `override.cfg`。测试设置与存档写入单独的 macOS Application Support 子目录。仓库内已有一次通过测试的副本，可用下方最后一条命令直接启动。重新创建副本时，请用新的测试目录或保留已有 `override.cfg`，以便保留同一测试存档。

```sh
game="$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2"
mkdir -p .local/game-smoke .local/logs
test -d .local/game-smoke/SlayTheSpire2.app || cp -cR "$game/SlayTheSpire2.app" .local/game-smoke/
test_game="$PWD/.local/game-smoke/SlayTheSpire2.app/Contents/MacOS"
mkdir -p "$test_game/mods/BaseLib"
cp .local/baselib/BaseLib{.dll,.json,.pck,CompatibilityHelper.dll,CompatibilityHelper.json} "$test_game/mods/BaseLib/"
cp -R SpireLab/bin/mods/ExportRelease/SpireLab "$test_game/mods/"
if [ ! -f "$test_game/override.cfg" ]; then
  cat > "$test_game/override.cfg" <<EOF_CONFIG
[application]
config/use_custom_user_dir=true
config/custom_user_dir_name="SpireLabPhaseASmoke-$(date +%s)"
EOF_CONFIG
fi

"$test_game/Slay the Spire 2" --force-steam=off --log-file "$PWD/.local/logs/game-smoke.log"
```

如需获取 `.local/baselib/`，从 v3.4.8 release 下载 `BaseLib.v3.4.8.zip` 并解压。ZIP 预期 SHA-256 为 `ad30d8aa4dee8e5d31150ab75eae6e6b927fa83ce30af93f4b2a144c3b09f82f`。该版同时需要归档里的 Compatibility Helper。首次选择 **Load Mods** 后，游戏可能自动重启且丢失离线参数。若出现 Steam 初始化提示，退出该测试窗口，再用上面的离线命令启动。

1. 检查日志中的 `User Data Directory` 指向独立测试目录、提示 Steam 初始化已跳过，并记录 `Loaded 3 mods (3 total)` 与 `Phase A initializer reached.`。
2. 进入单人模式，选择问号图标的 **SpireLab Test Character**，核对 70 点生命与 Burning Blood，开始一局。
3. 进入第一场战斗。抽到 **Calibration Strike** 后，用其在手牌中的数字快捷键选牌并点击敌人。无伤害修正和格挡时，预期消耗 1 点能量、减少敌人 8 点生命。
4. 击败敌人，检查 Burning Blood 治疗与三张卡牌奖励。日志应有 `playing card SPIRELAB-CALIBRATION_STRIKE` 和实验角色的胜利记录。

确认要在 Steam 安装中使用时，可以把两个 Mod 目录放到原 app 的 `Contents/MacOS/mods/`，重启后从游戏的 Mod 菜单检查启用状态。若已通过创意工坊安装相同的 BaseLib 版本，可复用它。回退时关闭游戏并移出新增的 Mod 目录，保留测试存档和原始 Steam 文件。Windows 的本地目录为游戏安装目录下的 `mods/`。

## 实测结果

- Debug 构建零警告、零错误。MegaDot 本地打包成功，英文和简中 JSON 两套资源进入 PCK。
- macOS 隔离副本加载 Compatibility Helper、BaseLib、SpireLab 三个 Mod。英语角色选单、开局、两次出牌、战后遗物和卡牌奖励均实测成功。
- 对 Nibbit 的第一次校准打击让能量由 3 降至 2，敌方生命由 44 降至 36。第二次抵消 5 格挡并扣除 3 生命。战胜后 Burning Blood 治疗 6 点，成功显示三张奖励卡。
- 升级后的 11 点伤害目前仅通过代码与编译检查。简中运行时显示、存档重载、Windows、Steam 联机和公开测试分支留待各自验证。

模板代码和临时图像来自 Alchyr 的角色模板，NuGet 声明 MIT。原版战斗外观和声音由 BaseLib 的 `PlaceholderCharacterModel` 在本机引用。发布前将确认资源归属并替换开发占位图。[Alchyr.Sts2.Templates 2.5.3](https://www.nuget.org/packages/Alchyr.Sts2.Templates/2.5.3) · [角色模板指南](https://github.com/Alchyr/ModTemplate-StS2/wiki/Creating-a-Character)
