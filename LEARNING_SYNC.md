# 学习同步

## 2026-11-20 · Phase A 已通过，Phase B 待选型

- 在 `SpireLab/` 用 `alchyrsts2charmod 2.5.3` 建立 C#／MegaDot 工程。关键新文件包括 `SpireLab.csproj`、`Sts2PathDiscovery.props`、manifest、角色与卡池、`CalibrationStrike.cs`、中英本地化及模板占位图。根目录 README 记录复现方法，[设计笔记](docs/character-design.md) 汇总原版机制和三个候选。
- 目标为 Apple M5 上 Steam 正式版 `v0.111.1`（build `26655341`）。本地工具为 .NET SDK `9.0.321`、MegaDot `4.5.1-m.14`、BaseLib `3.4.8`。实际构建零警告零错误，PCK 打包通过。隔离副本中三个 Mod 均加载，可选角色开局。
- 在首场战斗中，校准打击让能量 `3→2`、Nibbit 生命 `44→36`。下一回合的同一张牌消掉 5 格挡并扣 3 生命。战胜后 Burning Blood 治疗 6 点，奖励选择正常生成三张卡。日志位于 `.local/logs/game-smoke-combat.log`。
- 学习重点是通过继承声明 Model，通过 `[Pool]` 路由内容，用 `async Task` 等待原版 Command 完成。校准打击用一个 `DamageVar` 同时驱动文案、升级和实战数值。Phase A 的状态由原版管理，设计笔记里的序列、到期账本、战斗标记属于下一阶段的状态模型。
- 原版规则以官方说明、`sts2.xml` 和本机程序集为证据。Mod 接口先查锁定版 BaseLib、模板和开源实例，实机用于验收 Mod。STS001 识别出漏填的本地化键。首次 PCK 导出遇到编辑器反射缺引用及构建目录资源混入，随后分别修复 Debug 引用和导出过滤。运行证据来自独立的测试存档及日志。
- 亲自读三处代码。先看 [角色起始牌组](SpireLab/SpireLabCode/Character/SpireLab.cs)，理解 `ModelDb` 如何组装牌组。再看 [校准打击](SpireLab/SpireLabCode/Cards/CalibrationStrike.cs)，追踪从 `CardPlay` 到 `DamageCmd`。最后看 [构建目标](SpireLab/SpireLab.csproj)，区分引用、编译产物和 PCK。
- 下一步由你在编纹、潮契、测点三个核心中选一个，再一起固定边界与最小卡组。简中实机、升级后实机伤害、存档重载、Windows 及多人尚待验证。

## 2026-11-20 · 工具与 GitHub 基线

- 保留游戏的 `net9.0` 编译目标，构建工具切换到 .NET 10 LTS SDK `10.0.404`。MegaDot 与当前游戏一致，仍用 `4.5.1-m.14`。安装 Microsoft C# 扩展，用本地工具清单升级 ILSpy 到 `11.2.0.10050`。
- [dev-macos.sh](scripts/dev-macos.sh) 复现安装、检查、编译和打包。版本由 `global.json`、工具清单和项目引用管理。SDK 负责构建，目标框架决定代码可用的 API，运行时由游戏提供。
- 干净检出复用已校验的下载缓存，重新安装工具、编译零警告零错误，并导出 PCK。VS Code 的 C# 日志确认使用项目 SDK 加载 `SpireLab.csproj`。ILSpy 成功读取游戏中 `StrikeIronclad.OnPlay` 的签名。
- 私有 GitHub 仓库建立 `main` 基线，工具改动走单独分支和 PR。角色机制选型暂停。

## 2026-11-20 · 提灯测绘师测试版

- 采用简单的辉印机制验证全链路。加入原创角色模型、场景、美术、专属卡池、16 张新牌、2 件遗物、2 种药水、2 个状态与中英本地化。内容、实现位置和美术制作记录见 归档 archive/witch-v1-and-surveyor 的 docs/surveyor-preview.md。
- 每种牌用 `CanonicalVars` 提供运行数值和本地化变量。辉印通过 `PowerCmd` 更新可见状态，生成牌通过 `CardPileCmd` 加入当前战斗。普通奖励使用 `FilterThroughEpochs` 排除战斗衍生物。
- 三处适合亲自阅读的代码是 [辉印与回合钩子](SpireLab/SpireLabCode/Powers/LumenPower.cs)、[起始牌和资源消费](SpireLab/SpireLabCode/Cards/StarterCards.cs)、[真实游戏集成测试](tests/SpireLab.Smoke/SurveyorSmoke.cs)。前两处保留游戏原生 Command，最后一处从隔离游戏启动并断言实战状态。
- Apple Silicon 上实际游戏通过 26 项断言，覆盖辉印、起始牌、两类药水、升级、回合、战斗胜利、专属奖励、存档模型往返、商店动画、休息点和简中文案。窗口实测了角色选择、开局、立绘、手牌和起始辉印。ZIP 已与测试运行文件逐个比对并安装到 Steam 游戏的本地 `mods` 目录。
- 首轮出现过教程阻断、商店 Spine 类型错误和引擎启动预载错误。测试用独立存档跳过教程，商店改为 `NMerchantCharacter` 子类，脚本也捕获引擎直接写出的 `ERROR:`。通过的日志仍有游戏关停时的资源回收诊断，保存在本机 `.local/`。
- 下一版可直接换核心规则和卡面，再用同一套脚本跑回归。跨进程存档恢复、Windows、联网多人及一整局的平衡性仍待验证。

## 2026-11-20 · 魔女 Phase 1–2

- 采用规格书的 68 生命、4 种基础牌、10 张起始卡和 12 张奖励牌，含恰好 5 种欲望牌。保留原有两个 PR 和提灯测绘师。魔女使用独立角色和卡池 ID。
- [Enlightenment.cs](SpireLab/SpireLabCode/Witch/Mechanics/Enlightenment.cs) 从永久牌组副本用游戏 RNG 等权采样。BaseLib 隐藏牌堆保管原卡，游戏插入阶段维持手牌原位，Pile Hook 还原来源。学习点是区分对象身份、永久模型和战斗生命周期。
- [WitchCombatHooks.cs](SpireLab/SpireLabCode/Witch/Mechanics/WitchCombatHooks.cs) 接入原版抽牌、回合、能量、费用和伤害阶段。[WitchRules.cs](SpireLab/SpireLabCode/Witch/Mechanics/WitchRules.cs) 把逆转、支付、反射取整写成纯函数。实机使用 `CreatureCmd.Damage` 在格挡前处理下一击免疫，并在原版扣真实生命之前吸收镜像生命。
- `./scripts/dev-macos.sh check`、构建、打包成功。Apple Silicon 上 STS2 v0.111.1 的原型实机记录 33 项通过，含两场开战、真实回合转换和一次获胜；单元测试覆盖 500 条狂赌生命路径。隔离日志位于本机 `.local/witch-smoke.RfqHpw/game.log`，引擎退出时仍打印资源回收诊断。
- ImageGen 角色三视图先于战斗立绘、选角背景和四个画风锚点，源图和提示词见 归档 archive/witch-v1-and-surveyor 的旧美术目录。独立全卡插画、正式音效、多幕验证和完整池属于下一阶段。

## 2026-11-20 · 魔女 Phase 3

- 角色池扩展为 4 基础、20 普通、38 罕见、27 稀有、2 古代。9 件遗物与 3 种药水已接入。古代由 BaseLib 对接原版 Archaic Tooth、Dusty Tome 和 Touch of Orobas。D5 没有列出升级数值，原版古代来源可照常加升级标记，牌面效果沿用规格值。
- [战斗状态和 Hook](SpireLab/SpireLabCode/Witch/Mechanics/WitchCombatHooks.cs) 记录回合边界、实际获得的能量和格挡次数。「虚像」的固定减伤通过原版乘数 Hook 实现，向上调整比例精度使原生截断得到正确整数。额外能量命令完成后逐项处理触发，避免蒸馏器递归。
- STS2 v0.111.1 的窗口实机回归通过 62 项断言，包括全池注册、两种语言的牌面预览、三场战斗、四来源反伤、生命献祭、贪婪与替身、升级附魔复制、怠惰结束回合与能量上限下 X 费收尾。日志在 `.local/witch-smoke.cWTw55/game.log`。原型中涌现的古代升级崩溃与验证夹具的房间切换回收错误均已在测试中复查。
- 下一阶段接入 91 张独立卡面、检查更多游戏内渲染与跨幕流程。窗口自动战役使用原版 AutoSlay 的辅助强化，只验证流程兼容。

## 2026-11-20 · 魔女 Phase 4–5

- 归档 archive/witch-v1-and-surveyor 保留这一版的源图和提示词。91 张独立卡面由规格 ID 路由，古代牌用 606×852 竖图。界面新增红黑能量表和真实／镜像生命条，战斗窗口中核对了立绘、卡面和 HUD。
- 窗口回归 `witch` 通过 74 项断言。除核心机制、牌池和中英文本之外，验证无起始遗物仍能启迪、不可格挡状态伤害仍会消耗镜像生命，以及同一回合的两牌循环打出 8 次且生命与能量保持原值。原版 AutoSlay 辅助种子 `WITCH10004` 走完三幕并返回主菜单。
- `witch-save` 在两个进程之间验证了真正的读档链路。关键学习点是区分永久牌组、战斗卡牌实例、可序列化升级与只存在于战斗的镜像生命，并通过实际游戏动作队列检查 UI 同步。
- 安装 ZIP 放在本机 `.local/release/`，八个运行文件与已测副本逐一比对。Windows、联机、无辅助胜率及精细动画留给后续验收。完整说明见 [魔女验证记录](docs/witch-validation.md)。

## 2026-11-20 · 魔女 Phase 6

- [战斗演出配置](SpireLab/SpireLab/scenes/witch/combat.tscn) 给施法、攻击和受击接回待机。低血脉冲只写 `SelfModulate`，动作动画仍控制 `Modulate` 与位置，两层可同时播放。
- [实机回归代码](tests/SpireLab.Smoke/WitchSmoke.cs) 让两张消耗型治疗牌和狂赌之渊走原版动作队列，直接观察能量、真实生命与卡堆，而低血、格挡、死亡和重开通过游戏节点验证。商店移除使用实际购买选项。
- 最终安装版通过 94 项窗口实机断言，日志见 `.local/witch-smoke.rkuxEF/game.log`。压测发现原版卡牌动画会在动作队列清空后继续运行，测试夹具现在等卡牌动画结束再跳转房间。发布 ZIP 的运行文件与通过回归的副本逐一比对。
- 新增四张能力图标、双语封面及其可重导出脚本。安装说明、第三方许可归属、复现命令和待验收范围集中在 [魔女验证记录](docs/witch-validation.md)。

## 2026-11-20 · 魔女 v1.1.0 美术替换

- [角色母版与资源表](docs/art/witch-v2/README.md) 记录用户基准、人物比例、三视图和表情、提示词及来源索引。91 张卡图、27 个遗物纹理和 6 个药水纹理均与旧版逐文件比对，尺寸和唯一性通过检查。
- [卡框和侧徽实现](SpireLab/SpireLabCode/Witch/Cards/WitchCardAppearance.cs) 使用 BaseLib 原有显示入口。着色器沿用原卡框的轮廓和透明通道，视觉分类不参与战斗逻辑。镜中人格是场景中的独立 Sprite，现有生命逆转只额外触发演出。
- [截图测试](tests/SpireLab.Smoke/WitchVisualPreview.cs) 从原版界面取实际窗口帧。第一次截图发现选角背景缩放会裁掉帽尖，于是重新构图并回到同一场景验证。全量改版后通过 98 项实机回归。
- [发布工作区](docs/workshop/README.md) 由已验证的游戏副本生成，封面和截图独立于运行文件。旧实验角色及第一版美术留在归档标签，工作版本只包含新版魔女。
