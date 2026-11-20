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
