# 魔女 V1.6 实现与验证

V1.6 的规则与美术交付以 [综合规格](../SpireLab_The_Witch_GDD_v1.6.md) 为准。V1.0 与旧版本验证记录用于历史复现。

## 基线与差异

基线为 Git `1288dc7`。Apple Silicon 本机核对 Steam 正式版 v0.111.1、BaseLib 3.4.8、MegaDot 4.5.1-m.14、.NET SDK 10.0.404 和 `net9.0`。开始时工作区干净，GitHub 没有打开的 PR。

| 范围 | 基线 | V1.6 工作 |
| --- | --- | --- |
| 魔法启迪 | 每回合抽 6 张，并从永久牌组抽样生成替身 | 开场从 85 张奖励牌临时加 1 张，之后每回合可交换实际战斗牌 |
| 持续状态 | 战斗字段、零散动画和镜像生命条 | 按真实状态展示原版能力角标、次数、时序与双语提示 |
| 美术 | 分版本源图与单用途导出工具 | 统一稳定 ID、外部源图、尺寸映射、增量导入和失败回退 |
| 卡池 | 91 种牌、9 件遗物和 3 种药水，V1.1.0 逻辑 | 保留 ID 与内容规模，同步 V1.6 数值和卡组管理规则 |

## 魔法启迪验证

- 本机 `sts2.dll` 的 `Player.PopulateCombatState` 先随机化初始牌组。`CombatManager` 再于首次抽牌前调用 `AbstractModel.BeforeHandDraw`。命运选牌从 `CardSelectCmd.FromSimpleGrid` 取得结果，用 `CardPilePosition.Random` 插入一个均匀随机位置。该命令使用原生 `Rng.Shuffle`。初始化队列和此随机插入组合等价于对加入新牌的牌组随机排列。
- 实体交换在 `AfterAutoPrePlayPhaseEnteredEarly` 用 `CardSelectCmd.FromHand` 选择换出的牌，按抽牌堆与弃牌堆中每个实际实例等概率采样。先用原版 `CardPileCmd.Add` 腾出手牌槽，再移入随机牌。失败时不计为成功。双生药剂走同一交换规则并独立于固有次数。
- `SPIRELAB_SMOKE_WINDOWED=1 ./scripts/smoke-macos.sh witch` 实际构建、PCK 打包及 106 项窗口断言通过。覆盖整个开场池、起手抽牌、同回合次数、实例身份与附魔、遗物触发、无候选、跨战斗和永久存档边界。日志为 `.local/witch-smoke.hx8GAW/game.log`。

## 状态与画面

- `WitchCombatState` 统一记录结算用数据。`WitchStatuses` 把这些值显示为 28 种 BaseLib `CustomPowerModel`。模型通过 `PowerCmd.Apply`、`PowerModel.SetAmount` 和 `PowerCmd.Remove` 更新，原生面板负责排版和悬停。图标模型禁用自身的战斗钩子，避免显示刷新重复执行效果。
- 实机首次发现镜像生命说明覆盖角标。调整到原生血条上方后重新截图，真实和镜像生命仍使用不同颜色。验证使用命运选牌、手牌交换与状态收集的真实窗口流程。
- 已保存中英文选角、命运选牌、手牌和镜中人截图。正式的新角标插画可以按外部资源清单交付。当前 28 个 SVG 角标为程序化临时图形。

## 内容核对与边界

- [内容索引](witch-content.tsv) 将 91 个 GDD ID 映射到运行时 Model ID、类型、稀有度、费用、双语名称、逻辑文件和插画。窗口回归遍历全部卡牌的模型、费用和升级费用、原图加载及双语描述预览。9 件遗物与 3 种药水的注册和图标也逐项加载。
- 已覆盖镜像生命与真实生命分账、格挡和免疫优先顺序、多段反射、欲望减费下限、狂赌之渊回手和生命预算、半血怠惰与保留、能量上限、实体卡身份及双牌压缩循环。卡牌奖励、古代卡来源、商店、事件和跨进程读档使用原版入口。
- 镜纹斩采用一次确认。确认后消耗当前格挡与 4 的较小值，跳过则不消耗。凝炼深渊升级后的格挡在触发的回合结束时获得，额外能量在下回合获得。
- 堕落自信后，狂赌之渊可连续零费回手。定向测试打至 1 点真实生命时停止，镜像生命未用于支付。稳定的无价抽牌循环需要压缩牌组与特定牌，验证保留这条构筑。当前测试用于规则验证，胜率和未测试的组合仍需平衡数据。

| 最终 macOS 验证 | 结果 |
| --- | --- |
| `./scripts/dev-macos.sh check`、`build`、`pack` | 本机工具链可用，编译无警告，PCK 成功。 |
| `dotnet run --project tests/SpireLab.Witch.Rules` | 通过，包括 500 条狂赌真实生命递减路径。 |
| `SPIRELAB_SMOKE_WINDOWED=1 ./scripts/smoke-macos.sh witch` | 116 项通过。四场战斗和两张牌的循环，完成选牌清理、满手交换、临时魔药洪流消耗、书签返能、三种药水、商店与假商人测试。日志 `.local/witch-smoke.KFmDrL/game.log`。 |
| `./scripts/smoke-macos.sh witch-art` | 22 项通过。中英文选角、开场选牌、状态角标和镜像生命位置已截图复核。悬停能读取原版提示框显示的简中说明。截图 `.local/witch-art-smoke.9PU4KW/captures/`。 |
| `SPIRELAB_SMOKE_WINDOWED=1 ./scripts/smoke-macos.sh witch-save` | 两个进程均通过。保留真实生命、古代牌升级与遗物，开局能力和临时卡牌重新初始化。日志 `.local/witch-save-smoke.Efa3N2/`。 |
| `./scripts/smoke-macos.sh witch-legacy-save` | V1.0 隔离存档可在 V1.6 恢复并进入战斗，旧古代牌升级保持，启用新版命运选牌。日志 `.local/witch-legacy-save-smoke.oVhisZ/game.log`。 |
| `./scripts/stage-witch-release.sh` | 本地安装 ZIP 和工坊准备 ZIP 均通过解包与 SHA-256 校验。归档中的运行文件和实际测试副本逐字节比对。发布包 `.local/release/Witch-v1.6.0.XtbS3h/`。 |

首轮旧档恢复的断言通过，但原版菜单和战斗同时预载时出现 `Invalid Task ID`，因此严格检查失败。测试入口等待原版菜单资源加载完成后重试通过。退出仍有引擎的资源回收诊断。Windows 与联网多人需要对应环境验收。

## 外部美术替换

- `./scripts/dev-macos.sh art-sync --dry-run` 核对 197 个映射，缺失源图均能回落到现有资源。用独立副本和三个测试源文件导出六张运行图，涵盖魔力之巅卡图、破损梳妆镜的本体与描边及大图、魔力之巅能力的小图和大图。第二次检查命中增量记录。
- `SPIRELAB_ART_SYNC_TEST=1 ./scripts/smoke-macos.sh witch-art` 在替换版 PCK 中验证三类资源通过实际 UI 路径加载，截图记录为 `.local/witch-art-smoke.S5wzJ4/captures/`。故意提供无透明通道的能力图后文件校验拒绝导入，现有 PCK 的 SHA-256 保持相同。
- 美术包的文件、来源、缩放规则和替换步骤见 [美术资源包说明](../artwork/README.md)。测试图只存在于隔离副本，交付包使用现有正式图和已明确标注的角标占位图。
