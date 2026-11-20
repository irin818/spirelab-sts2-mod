# 魔女 v1.1.0 验证

适用基线为 Steam STS2 v0.111.1 build 26655341、BaseLib 3.4.8、Apple Silicon。项目沿用原有 C#、MegaDot 和 macOS 构建流程。魔女角色、牌池和存档沿用 V1.0 的模型 ID。旧实验角色及旧资源保存在 Git 归档 `archive/witch-v1-and-surveyor`。

## 实现位置

- [规格书](archive/SpireLab_The_Witch_GDD_v1.0.md) 和 [内容索引](witch-content.tsv) 将 91 个规格 ID 对应到类、升级、简中/英文和导出 PNG。欲望标记对应 U37、U38、R25、R26、R27。
- [启迪与临时替身](../SpireLab/SpireLabCode/Witch/Mechanics/Enlightenment.cs) 在原版抽牌后处理选择。永久牌组保持实际副本组成，抽样使用游戏随机流，原卡放入 BaseLib 隐藏战斗牌堆，替身继承抽到的永久副本属性。
- [生命和伤害](../SpireLab/SpireLabCode/Witch/Mechanics/WitchCombatHooks.cs) 使用目标版本 `AbstractModel` 和 `CreatureCmd.Damage` 的 Hook。先计算免疫、格挡，再由镜像生命吸收未被格挡的战斗伤害。原版中毒会以无攻击者的伤害调用这条管线，生命支付则直接走真实生命结算。反射分别取整后走非攻击伤害命令。
- [额外能量](../SpireLab/SpireLabCode/Witch/Mechanics/WitchEnergy.cs) 通过 `PlayerCombatState.EnergyChanged` 取实际变化。每回合默认能量重置排除在外，遗物额外能量有明确的递归保护。魔力之巅沿用原生保留能量路径。
- 古代内容使用 BaseLib 的 `ITranscendenceCard`、`ITomeCard` 和 `GetUpgradeReplacement`。D5 只规定基础数值，故原版可正常保存和显示升级标记，效果数值沿用 D5。接口对照 [BaseLib v3.4.8 源码](https://github.com/Alchyr/BaseLib-StS2/tree/v3.4.8)、[模板命令手册](https://github.com/Alchyr/ModTemplate-StS2/wiki/Common-Commands-Cookbook) 和本机程序集。
- [新版美术基准、图集、提示与源文件](art/witch-v2/README.md) 先于逐张卡面制作。战斗立绘、镜中形态、选角场景、91 张 PNG、12 种物品图标和能量表作为实际导入资源打包。

## 复现

```sh
./scripts/dev-macos.sh check
./scripts/dev-macos.sh build
./scripts/dev-macos.sh pack
.local/dotnet-10/dotnet run --project tests/SpireLab.Witch.Rules
SPIRELAB_SMOKE_WINDOWED=1 ./scripts/smoke-macos.sh witch
SPIRELAB_SMOKE_WINDOWED=1 ./scripts/smoke-macos.sh witch-save
SPIRELAB_SMOKE_WINDOWED=1 ./scripts/smoke-macos.sh witch-campaign
./scripts/smoke-macos.sh witch-art
```

- `witch` 实测加载、牌池、英文及简中升级预览、物品和古代路由，再进入固定战斗执行启迪回收、升级附魔复制、欲望、反伤、虚像、生命支付和能量上限的组合。检查低血待机、回血恢复和格挡闪光，随后走游戏的死亡结算、返回主菜单、新开一局和商店付费移除卡牌。
- `witch-save` 在第一进程通过真实地图进入战斗并保存，从第二进程使用游戏读档流程恢复 41 点真实生命、牌组、升级的古代牌和初始遗物。
- `witch-legacy-save` 通过 `SPIRELAB_WITCH_LEGACY_SAVE` 将旧版本 `witch-save` 生成的专用固定夹具复制到独立档案，读档断言与当前版本相同。
- `witch-campaign` 在隔离副本使用原版 AutoSlay。以魔女走过三幕并返回主菜单时，记录实际种子和三幕首领结果。原版 AutoSlay 会施加防护和后期力量强化，用于验证跨幕流程。
- `witch-art` 使用原版选角场景和真实战斗手牌。测试简中和英文的选角面板分别显示「角色特性 · 魔法启迪」和「破损梳妆镜」，并检查随机换牌、遗物第一次成功换牌才给格挡、失去遗物后仍保留特性的说明。在指定副本的 `captures/` 中保存双语选角、三张样始牌与镜中形态截图。
- 窗口实机确认独立卡面、魔女立绘、能量表以及真实／镜像生命条。自动模式保留引擎关闭时的 RID 与 Shader 回收诊断。早期无窗口预载曾出现引擎 dummy texture 和 worker task 诊断，窗口模式是本版完整回归的基准。
- v1.1.0 全新工程目录完成无缓存构建与 PCK 导出。窗口完整回归 100 项通过。新版跨进程读档与 V1.0 魔女固定存档迁移均恢复了 41 点真实生命、角色、已升级古代牌、初始遗物和第六张手牌。
- 原版 AutoSlay 在首次启动提示仍打开时，会在假商人事件等待继续按钮超时。隔离配置预先确认该提示后，定向测试实测假商人按钮可用并返回地图。生产 Mod 的事件代码和本机使用者的设置没有参与此修复。
- v1.1.0 辅助种子 `WITCH10051` 赢下第一幕第 17 层、第二幕第 16 层和第三幕第 15 层的首领战，完成结算并返回主菜单。此局也经过第三幕假商人事件。命令退出码为 0。
- 辅助种子 `WITCH10058` 通过前两幕首领及假商人事件。第三幕第 6 层前一房间存档为 54/54，自动玩家在球头战斗的第 2 回合使用「镜中人」后战败。原版记录为 `GLOBE_HEAD_NORMAL`、失败、0/57，符合高真实生命时逆转有代价的规则。AutoSlay 在败局后仍等待胜利奖励，最终报超时。

## 平衡与待验收

- 保留同回合可循环构筑。最小验证牌组只含两张原版无色牌 Finesse 与 Flash of Steel，要求移除其他牌并实际取得这两张牌。构筑按原版抽牌和费用规则循环。魔女自身的狂赌之渊按 40%／50% 向上取整扣真实生命，在 1 点时无法继续，不能用镜像生命支付。
- 在堕落自信生效后实测回血组合。真实生命从 1 经「回复」和「灵药瓶」升到 7，两张牌都进入消耗堆。狂赌之渊随后以 0 费打出两次并净得 4 点能量，真实生命回到 1，第三次无法打出。
- 现有测试覆盖代表性场景和极端组合。无强化整局胜率、全面构筑平衡、Windows、联网多人、正式逐张美术验收和精细演出仍需独立样本。

## 安装包与发布准备

- 手动安装包内含 Mod 的三个运行文件、锁定的 BaseLib 及其兼容组件、BaseLib 许可证和双语安装说明。用 `./scripts/dev-macos.sh pack` 重建工程内三件运行文件。
- `./scripts/stage-witch-release.sh <已测的 mods 目录> <已测的 captures 目录>` 生成两份 ZIP 与 SHA-256。工作区包括尺寸受限的封面、游戏截图和依赖声明。[工坊准备](workshop/README.md)
- 本机 Steam Mod 目录已切换到 v1.1.0。执行 100 项回归的副本、手动安装包和工坊工作区的三个运行文件均与本机安装逐字节一致。后续长流程副本重新构建了 DLL，经 ILSpy 比对方法指令一致，PCK 和 manifest 哈希一致。旧版安装连同旧安装包在本机 `.local/` 归档，以便回退。
- BaseLib 使用上游 MIT 许可证；角色模板的 NuGet 元数据也声明 MIT。魔女代码和原创素材当前在私有库交付，公开许可由项目所有者决定。[BaseLib 许可证](https://github.com/Alchyr/BaseLib-StS2/blob/v3.4.8/LICENSE.txt) · [模板许可信息](https://www.nuget.org/packages/Alchyr.Sts2.Templates/2.5.3)
