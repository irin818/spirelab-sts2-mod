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

- `witch` 实测加载、牌池、英文及简中升级预览、物品和古代路由，再进入固定战斗执行启迪回收、升级附魔复制、欲望、反伤、虚像、生命支付和能量上限的组合。检查低血待机、回血恢复和格挡闪光，随后走游戏的死亡结算、返回主菜单、新开一局和商店付费移除卡牌。
- `witch-save` 在第一进程通过真实地图进入战斗并保存，从第二进程使用游戏读档流程恢复 41 点真实生命、牌组、升级的古代牌和初始遗物。
- `witch-campaign` 在隔离副本使用原版 AutoSlay。种子 `WITCH10004` 以魔女走过三幕并返回主菜单。使用了 AutoSlay 的防护与后期力量强化，结果用于测试跨幕兼容。
- 窗口实机确认独立卡面、魔女立绘、能量表以及真实／镜像生命条。自动模式保留引擎关闭时的 RID 与 Shader 回收诊断。早期无窗口预载曾出现引擎 dummy texture 和 worker task 诊断，窗口模式是本版完整回归的基准。

## 平衡与待验收

- 保留同回合可循环构筑。最小验证牌组只含两张原版无色牌 Finesse 与 Flash of Steel，要求移除其他牌并实际取得这两张牌。构筑按原版抽牌和费用规则循环。魔女自身的狂赌之渊按 40%／50% 向上取整扣真实生命，在 1 点时无法继续，不能用镜像生命支付。
- 在堕落自信生效后实测回血组合。真实生命从 1 经「回复」和「灵药瓶」升到 7，两张牌都进入消耗堆。狂赌之渊随后以 0 费打出两次并净得 4 点能量，真实生命回到 1，第三次无法打出。
- 现有测试覆盖代表性场景和极端组合。无强化整局胜率、全面构筑平衡、Windows、联网多人、正式逐张美术验收和精细演出仍需独立样本。

## 安装包与发布准备

- 私有验证包内含 `SpireLab.dll`、`SpireLab.pck`、`SpireLab.json`、锁定的 BaseLib 及其兼容组件、BaseLib 许可证和双语安装说明。用 `./scripts/dev-macos.sh pack` 重建工程内三件运行文件。ZIP 内的运行文件与实际窗口回归的副本逐一比对；随包提供 SHA-256。
- [创意工坊封面](art/witch/workshop-cover.jpg) 为 1200 × 675 原创图，仓库保留源图和导出脚本。[Steamworks 预览图文档](https://partner.steamgames.com/doc/api/ISteamUGC#SetItemPreview)
- BaseLib 使用上游 MIT 许可证；角色模板的 NuGet 元数据也声明 MIT。魔女代码和原创素材当前在私有库交付，公开许可由项目所有者决定。[BaseLib 许可证](https://github.com/Alchyr/BaseLib-StS2/blob/v3.4.8/LICENSE.txt) · [模板许可信息](https://www.nuget.org/packages/Alchyr.Sts2.Templates/2.5.3)
