# SpireLab — The Witch V2.0 卡牌系统规格与验证记录

## 1. 基线与目标

- **起始基线**：`main` @ `4f4a99c`（V1.6.3）
- **目标版本**：`v2.0.0`（开发分支 `gemini/witch-v2-cards`）
- **运行环境**：macOS arm64，STS2 `v0.111.1`，BaseLib `3.4.8`，.NET SDK `10.0.404`（目标框架 `net9.0`），MegaDot `4.5.1-m.14`
- **保留边界**：完整保留魔女角色模型/ID、魔法启迪核心、真实生命/镜像生命/生命逆转、五张欲望牌（`U37 / U38 / R25 / R26 / R27`）原有行为、血色终镜 V4 处决演出、全套卡牌/角色/遗物/药水美术与现有工具链。

## 2. 奖励池精简（91 定义 → 82 启用 / 76 常规奖励）

| 稀有度 | 总定义（存档兼容） | V2.0 启用 | V2.0 暂退 |
|---|---:|---:|---:|
| 基础 | 4 | 4 | 0 |
| 普通 | 20 | 19 | 1 |
| 罕见 | 38 | 32 | 6 |
| 稀有 | 27 | 25 | 2 |
| 古代 | 2 | 2 | 0 |
| **合计** | **91** | **82（含 76 常规奖励）** | **9** |

- **9 张暂退卡牌**：`C17` 映光、`U02` 回复、`U03` 魔力封存、`U17` 残像、`U25` 背影、`U26` 镜牢、`U29` 折射棱晶、`R06` 凝炼深渊、`R22` 时之封缄。
- **实现方式**：`WitchCard.RetiredSpecIds` 与 `WitchCard.IsInRewardPool` 统一驱动 `WitchCardPool.FilterThroughEpochs` 与 `Enlightenment.ChooseOpeningCard`，使战斗奖励、商店、命运选牌与解锁随机生成池均排除 9 张暂退牌，同时 `ModelDb` / `AllCards` / 美术 / 本地化保留全部 91 张定义以保证旧存档零破坏。
- **第二轮平衡复审候选（本轮保持启用）**：`C08`、`C13`、`U11`、`U12`、`U28`、`R14`。

## 3. 核心机制实现

### 3.1 反奏（Counterpoint）

- **纯规则**：`WitchRules.IsCounterpoint(PlayKind? previous, PlayKind current)`，仅在 `Attack → Skill` 或 `Skill → Attack` 时返回 `true`。
- **生命周期**：
  - `WitchCombatHooks.BeforeCardPlayed`（`PlayIndex == 0`）读取 `state.LastPlayedType` 快照至 `state.CurrentPlayWasCounterpoint`。
  - `WitchCombatHooks.AfterCardPlayedLate` 在牌结算完成后更新 `state.LastPlayedType = play.Card.Type` 并触发 `R15` 空镜的每回合首次交替奖励。
  - `WitchCombatState.BeginTurn()` 每回合开始清空 `LastPlayedType`、`CurrentPlayWasCounterpoint`、`MirrorTransferEnergyUsed` 与 `EmptyMirrorTriggered`。
  - 能力牌（含 `R15` 空镜与 `R25` 堕落自信）将 `LastPlayedType` 写为 `CardType.Power`，自然中断反奏链。
  - 无法打出的牌在 `ShouldPlay` 被拦截，不触达 `BeforeCardPlayed` / `AfterCardPlayedLate`。
- **实装反奏卡牌（共 14 张：第四阶段清单 13 张 + 第六阶段 U07 灵药瓶）**：
  - 魔药（5）：`C02` 药雾、`C07` 烈性蒸汽、`C09` 余焰、`U07` 灵药瓶、`U15` 逆流药剂
  - 镜子（4）：`C10` 镜屑、`C14` 冷镜屏障、`C15` 镜面转移、`U35` 暗纱守护
  - 魔法书（4）：`C19` 书页飞刃、`U30` 书页置换、`U32` 魔纹之刃、`U36` 空白契页
  - 稀有核心（1）：`R15` 空镜（能力牌）

### 3.2 统一反射率体系

- **公式**：`WitchRules.Reflect(blocked, totalPercent, fixedDamage)`，其中 `totalPercent = (MirrorForm ? 100 : 0) + BaseReflectPercent + TempReflectPercent + NextReflectPercent`（百分比加法累计，乘以实际格挡的敌方攻击伤害向下取整，再加固定反伤）。
- **临时反射率**（`TempReflectPercent`）：来自 `C10`、`C11`、`U18`、`U19`，持续至下一次玩家回合开始，覆盖有效期内所有实际格挡的敌方攻击。
- **持续反射率**：`R10` 镜中人保持现行 100%；`U21` 反光刻印在每回合首次成功造成反射伤害（`TotalDamage > 0`）后使 `BaseReflectPercent` 增加 25%（升级 50%），采用保守共享上限 `100%`（`WitchRules.InscriptionReflectCap = 100`），多副本共同受该上限约束。
- **兼容性**：保留 `B03` 固定反伤、`R17` 单次 +100% 反射、`R10` 生命逆转与 `R12` 独立免疫反击。未擅自添加 250% 总反射率硬上限。

### 3.3 特殊规则与保守处理说明

- **`U05` 提纯**：使用 `WitchCard.ExhaustOne`（`CardSelectCmd.FromHand` + `CardCmd.Exhaust`，排除自身），目标进入消耗堆而非弃牌堆；无其他手牌时安全返回 `false`、不报错也不加能；升级后先抽 1 再选择消耗。
- **`U34` 半月账本**：在 `AfterCardPlayedLate` 中，于 `play.PlayIndex == play.PlayCount - 1` 时仅当 `play.ResultPile == PileType.Discard && play.Card.Pile?.Type == PileType.Play` 时将攻击牌移入抽牌堆随机位置（利用 STS2 `CardModel.OnPlayWrapper` 仅移动仍在 `Play` 区的牌的原生机制避免重复移动），再抽 1 张；若目标攻击自带消耗（如 `U27` 反身）或特殊去向，则保守保留原消耗去向并仍抽 1 张。
- **`U14` 蓝焰配方**：每回合首次获得额外能量后写入 `PendingSingleHitAttackBonus`，在下张攻击打出时转入 `ActiveSingleHitBonus`，并在 `AfterDamageGiven` 首个受力量加成的攻击伤害事件后立即清零，确保多段攻击（如 `U10` 沸腾回响）仅强化第一段伤害。
- **`R16` 镜狱**：打出时一次性快照 `Math.Max(Owner.Creature.Block, 10)` 作为两段基础伤害，避免段间格挡变化导致重算。

## 4. 候选新卡记录（未加入 V2.0 正式奖励池）

按 V2.0 规格第十一阶段要求，以下候选设计仅登记归档，未注册进奖励池；已否决的「镜藏」机制未加入代码。

| 候选名称 | 拟定定位 | 暂定草案 | 状态 |
|---|---|---|---|
| 碎相 | 镜子消耗临时反射率攻击 | 1 费攻击，造成 8（11）伤害；消耗所有临时反射率，每消耗 25% 额外造成 3（4）伤害 | 高优先级候选（待 V2.1） |
| 魔力追击 | 魔药额外能量进攻 | 待细化 | 候选 |
| 猩红连奏 | 反奏连段终端 | 待细化 | 候选 |
| 命运书签 | 魔法书保留联动 | 待细化 | 候选 |
| 逆光镜 | 镜子反射联动 | 待细化 | 候选 |
| 透支配方 | 魔药生命/能量换取 | 待细化 | 候选 |
| 残页庇护 | 魔法书防御 | 待细化 | 候选 |

## 5. 验证结果摘要

| 验证命令 | 结果 | 关键覆盖 |
|---|---|---|
| `./scripts/dev-macos.sh check` | PASS | STS2 `v0.111.1`、BaseLib `3.4.8`、.NET `10.0.404`、MegaDot `4.5.1-m.14` |
| `./scripts/dev-macos.sh build` | PASS | `0 Warning(s), 0 Error(s)`（含 BaseLib `STS001` 本地化变量校验） |
| `./scripts/dev-macos.sh pack` | PASS | `Verified Workshop Release: SpireLab.dll, SpireLab.pck, SpireLab.json` |
| `.local/dotnet-10/dotnet run --project tests/SpireLab.Witch.Rules` | PASS | 逆转、反射加法、反奏规则矩阵、反光刻印 100% 上限、镜狱 10 下限与 500 条狂赌路径 |
| `python3 -m unittest -v tests/test_witch_release.py` | PASS | 11/11 发布与媒体校验通过 |
| `SPIRELAB_SMOKE_WINDOWED=1 ./scripts/smoke-macos.sh witch` | PASS | 全 91 定义/82 启用/76 常规奖励池、全部 15 项反奏用例、7 组跨体系连段 A–G、商店/奖励过滤、中英文案 |
| `SPIRELAB_SMOKE_WINDOWED=1 ./scripts/smoke-macos.sh witch-save` | PASS | 跨进程冷启动读档恢复，含暂退卡 `C17 映光` 与能力牌化 `R15 空镜` |
| `./scripts/smoke-macos.sh witch-art` | PASS | 选角双语、76 卡命运选牌网格、筛选器、V2.0 状态图标与实机截图 |
| `./scripts/smoke-macos.sh witch-greed` | PASS | 《原初贪婪》完整洗牌、抽回、附魔、灾祸生成与实例守恒回归 |
| `./scripts/smoke-macos.sh witch-finisher` | PASS | 血色终镜 V4 处决动画完整回归 |
| `SPIRELAB_SMOKE_WINDOWED=1 ./scripts/smoke-macos.sh witch-campaign` | PASS | AutoSlay 走完 Act 1–3 Boss 并获胜返回主菜单，零运行错误 |
