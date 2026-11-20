# 魔女 V1.0

适用基线为 Steam STS2 v0.111.1 build 26655341、BaseLib 3.4.8、Apple Silicon。项目沿用原有 C#、MegaDot 和 macOS 构建流程。魔女与提灯测绘师使用各自的模型 ID 和卡池。

## 实现位置

- [规格书](../SpireLab_The_Witch_GDD_v1.0.md) 和 [内容索引](witch-content.tsv) 将 91 个规格 ID 对应到类、升级、简中/英文和导出 PNG。欲望标记对应 U37、U38、R25、R26、R27。
- [启迪与临时替身](../SpireLab/SpireLabCode/Witch/Mechanics/Enlightenment.cs) 在原版抽牌后处理选择。永久牌组保持实际副本组成，抽样使用游戏随机流，原卡放入 BaseLib 隐藏战斗牌堆，替身继承抽到的永久副本属性。
- [生命和伤害](../SpireLab/SpireLabCode/Witch/Mechanics/WitchCombatHooks.cs) 使用目标版本 `AbstractModel` 和 `CreatureCmd.Damage` 的 Hook。先计算免疫、格挡，再由镜像生命吸收未被格挡的战斗伤害。原版中毒会以无攻击者的伤害调用这条管线，生命支付则直接走真实生命结算。反射分别取整后走非攻击伤害命令。
- [额外能量](../SpireLab/SpireLabCode/Witch/Mechanics/WitchEnergy.cs) 通过 `PlayerCombatState.EnergyChanged` 取实际变化。每回合默认能量重置排除在外，遗物额外能量有明确的递归保护。魔力之巅沿用原生保留能量路径。
- 古代内容使用 BaseLib 的 `ITranscendenceCard`、`ITomeCard` 和 `GetUpgradeReplacement`。D5 只规定基础数值，故原版可正常保存和显示升级标记，效果数值沿用 D5。接口对照 [BaseLib v3.4.8 源码](https://github.com/Alchyr/BaseLib-StS2/tree/v3.4.8)、[模板命令手册](https://github.com/Alchyr/ModTemplate-StS2/wiki/Common-Commands-Cookbook) 和本机程序集。
- [美术基准、图集、提示与源文件](art/witch/README.md) 先于逐张卡面制作。战斗立绘、选角场景、91 张 PNG、12 种物品图标、能量表和机制音效作为实际导入资源打包。

## 复现

```sh
./scripts/dev-macos.sh check
./scripts/dev-macos.sh build
./scripts/dev-macos.sh pack
.local/dotnet-10/dotnet run --project tests/SpireLab.Witch.Rules
SPIRELAB_SMOKE_WINDOWED=1 ./scripts/smoke-macos.sh witch
SPIRELAB_SMOKE_WINDOWED=1 ./scripts/smoke-macos.sh witch-save
SPIRELAB_SMOKE_WINDOWED=1 ./scripts/smoke-macos.sh witch-campaign
```

- `witch` 实测加载、牌池、英文及简中升级预览、物品和古代路由，再进入固定战斗执行启迪回收、升级附魔复制、欲望、反伤、虚像、生命支付和能量上限的组合。
- `witch-save` 在第一进程通过真实地图进入战斗并保存，从第二进程使用游戏读档流程恢复 41 点真实生命、牌组、升级的古代牌和初始遗物。
- `witch-campaign` 在隔离副本使用原版 AutoSlay。种子 `WITCH10004` 以魔女走过三幕并返回主菜单。使用了 AutoSlay 的防护与后期力量强化，结果用于测试跨幕兼容。
- 窗口实机确认独立卡面、魔女立绘、能量表以及真实／镜像生命条。自动模式保留引擎关闭时的 RID 与 Shader 回收诊断。早期无窗口预载曾出现引擎 dummy texture 和 worker task 诊断，窗口模式是本版完整回归的基准。

## 平衡与待验收

- 保留同回合可循环构筑。最小验证牌组只含两张原版无色牌 Finesse 与 Flash of Steel，要求移除其他牌并实际取得这两张牌。构筑按原版抽牌和费用规则循环。魔女自身的狂赌之渊按 40%／50% 向上取整扣真实生命，在 1 点时无法继续，不能用镜像生命支付。
- 现有测试覆盖代表性场景和极端组合。无强化整局胜率、全面构筑平衡、Windows、联网多人、正式逐张美术验收和精细演出仍需独立样本。
