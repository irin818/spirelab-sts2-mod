# 提灯测绘师 v0.2.0

提灯测绘师用陶甲和测量工具勘测塔内断层。辉印是战斗中的自有状态，最多 6 层，由起始遗物、卡牌和药水增加。共鸣释放花费最多 3 层，每层增加本次攻击的 3 点基础伤害。回声提灯在持有者的回合开始时增长辉印。

## 内容样本

| 类别 | 牌或物品 |
| --- | --- |
| 起始牌组 | 卡尺打击 ×4、陶甲防御 ×4、灯光探针、共鸣释放 |
| 普通 | 擦亮陶甲、凿穿岩层、规划路线 |
| 罕见 | 回声提灯、袖中晶片、断层触击 |
| 稀有 | 深层勘测、图谱涌动、击碎岩脊 |
| 战斗生成物 | 勘测晶片是攻击衍生牌，落石尘是临时状态牌，断层欠账是诅咒牌 |
| 遗物 | 勘测提灯、黄铜基准仪 |
| 药水 | 辉印浓剂、断层溶剂 |

覆盖攻击、技能、能力、状态、诅咒五种类型，以及单体、全体、格挡、抽牌、能量、脆弱效果、回合钩子、衍生牌、消耗和升级。战斗生成物在模型中可查，由指定效果生成，普通奖励和商店使用已过滤的职业卡池。早期校准打击的 ID 保留用于 Phase A 存档兼容性。

## 迭代位置

- 规则在 `SpireLab/SpireLabCode/Powers/LumenPower.cs`，卡牌按品级分组在 `SpireLab/SpireLabCode/Cards/`。卡牌和遗物通过 `ModelDb` 注册，其数值和升级绑定 `DynamicVar`。新卡使用 `SpireLabCard` 生成战斗卡牌与执行攻击的已有方法。
- 选角、战斗、休息点和商店场景在 `SpireLab/SpireLab/scenes/`，可在 MegaDot 中编辑。战斗 `AnimationPlayer` 定义待机、攻击、施法、受击和倒下。BaseLib 转换战斗与休息点节点。商店场景使用 `Visuals/SurveyorMerchantVisual.cs` 直接继承 `NMerchantCharacter`，因为 BaseLib 3.4.8 自动转换的根节点仍会走游戏原有的 Spine 初始化。这里覆盖 `_Ready` 来播放自己的待机动画，无需给游戏新增补丁。
- 角色立绘、选角背景和卡面在 `SpireLab/SpireLab/images/surveyor/`。卡牌构造器的最后一个参数指向 `cards/` 下的画面键，共享主题画面可以用新增的独立文件逐张替换。遗物、状态、药水图标用稳定 Model ID 的小写文件名。保留这些 Model ID 可以帮助旧档加载。
- 英文和简中表在 `SpireLab/SpireLab/localization/eng/` 与 `zhs/`。改效果时同步修改两张表，保持变量名与 `DynamicVar` 对应。
- 实现对照本机 `v0.111.1` 的 `sts2.xml` 和 ILSpy 输出中的 `CardCmd.AutoPlay`、`CardPileCmd.AddGeneratedCardToCombat`、`PowerCmd`、`PotionModel.OnUseWrapper` 和 `NCharacterSelectScreen`。角色接入和无脊柱动画参照 BaseLib `v3.4.8` 的 `CustomCharacterModel`、`NCreatureVisualsFactory` 和 `CustomAnimation`。[BaseLib 源码](https://github.com/Alchyr/BaseLib-StS2/tree/v3.4.8) · [社区命令指南](https://github.com/Alchyr/ModTemplate-StS2/wiki/Common-Commands-Cookbook)

## 验证清单

1. 运行 `./scripts/smoke-macos.sh`。成功时显示 26 项断言和 `[SpireLabSmoke] PASS`，测试 Mod 只安装进隔离副本。游戏退出时可能额外报告引擎资源回收警告。
2. 为窗口验证启动独立游戏副本，只加载 BaseLib、Compatibility Helper 和 SpireLab。单人模式选中提灯测绘师，核对背景、72 生命、初始提灯和 10 张初始牌。
3. 在第一场战斗确认开场 2 辉印。打出灯光探针后增加 2，打出共鸣释放后扣除至多 3。攻击能被格挡修正。战斗结束后检查专属奖励。
4. 存档保留游戏自己的角色和卡牌 ID。本版验证了战前存档序列化往返、进入房间、商店待机动画和简中文案加载。独立进程重开并继续对局、商店购物、完整通关、其他 Mod 共存、Windows 和联网多人仍待实测。

## 原创美术记录

用内置 ImageGen 生成三张源图，使用 macOS `sips` 为各场景适配尺寸、裁切卡面和图标。生成源未作为第三方资源引用。正式版可以在保持路径的情况下换成新的插画和动画。

- 战斗立绘提示要求真实透明背景的全身三分之四侧视图，向右站立。主体是灰色陶瓷机械人，三眼琥珀灯头、灰青披风、细黄铜腿与大型测量卡尺。墨线与水粉质感，清楚轮廓，不带地面、文字、标志或边框。工程结果为 `images/surveyor/surveyor.png`。
- 选角背景提示要求 16:9 的地下勘测厅，玄武岩层、粉笔等高线和黄铜工具。测绘师位于右半边，左侧留暗青岩壁供游戏文字叠放。无文字、边框或 UI。工程结果为 `images/surveyor/survey_hall.jpg`。
- 卡面提示要求精确 3×3、等大矩形、无间距无排版的插画图集。以卡尺、护臂、提灯、地图、共鸣、晶片、罗盘、墨瓶、测绘机械人为九个主题，用同一套灰青和琥珀墨线水粉风格。衍生画面在 `images/surveyor/cards/` 和遗物、药水、状态图标目录。
