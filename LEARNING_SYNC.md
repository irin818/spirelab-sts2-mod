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
- [截图测试](tests/SpireLab.Smoke/WitchVisualPreview.cs) 从原版界面取实际窗口帧。第一次截图发现选角背景缩放会裁掉帽尖，于是重新构图并回到同一场景验证。全量改版后通过 100 项实机回归，包含假商人事件进入与离开。
- [隔离测试入口](scripts/smoke-macos.sh) 把设置和存档写在独立档案。首次提示需要在隔离配置中确认，否则仍会占住原版事件的输入焦点。跨进程读档及 v1.0 魔女档案恢复均通过。
- 自动长测用固定种子区分流程故障和打法结果。`WITCH10058` 在第三幕高血量施放镜中人后正常战败。`WITCH10051` 通过假商人和三幕首领，赢下整局并返回主菜单。
- [发布工作区](docs/workshop/README.md) 由已验证的游戏副本生成，封面和截图独立于运行文件。本机 Steam 安装和两个安装包通过运行文件逐字节核对。旧实验角色及第一版美术留在归档标签，工作版本只包含新版魔女。后续待验证 Windows、联网多人及工坊订阅链路。

## 2026-11-20 · 魔法启迪说明

- 简中与英文的 `characters.json` 在原版选角面板明确展示固有特性。两份 `relics.json` 写明破损梳妆镜每场战斗首次成功换牌给 3 点格挡，双生全身镜改为每回合首次。失去初始遗物仍可使用启迪。
- 本机 `sts2.dll` 中 `NCharacterSelectScreen.SelectCharacter` 分别读取角色描述和 `StartingRelics[0].DynamicDescription`。因此可以用本地化资源清楚区分特性和遗物，战斗钩子、存档和场景布局继续沿用现有实现。
- [实机预览测试](tests/SpireLab.Smoke/WitchVisualPreview.cs) 读取原版面板的渲染文字，验证双语并截图。`witch-art` 通过 14 项检查。`witch` 首次完成 100 项断言，但在启动预载时出现引擎 `Invalid Task ID`，整项复跑以 0 退出并通过全部 100 项。已将实测 PCK 装入本机 Steam Mod 目录。

## 2026-11-20 · 魔女 V1.6 开场与换牌

- [V1.6 规格](SpireLab_The_Witch_GDD_v1.6.md) 和 [差异与验证](docs/witch-v1.6-validation.md) 记录当前基线。命运选牌只在首次抽牌前创建 1 张未升级战斗牌，随机插入抽牌堆。手牌交换用两次原版移动保留实例、升级与附魔。
- [启迪](SpireLab/SpireLabCode/Witch/Mechanics/Enlightenment.cs) 体现随机插入与实例等权采样的区别。[战斗钩子](SpireLab/SpireLabCode/Witch/Mechanics/WitchCombatHooks.cs) 分开处理首次抽牌、回合选择和战斗清理。106 项窗口实机断言通过，下一阶段继续检查角标、美术同步与卡池改动。

## 2026-11-20 · 魔女 V1.6 卡池、状态与打包

- 更新魔药、镜子、魔法书和五张欲望牌的规则与双语描述。[内容索引](docs/witch-content.tsv) 对齐 91 种卡牌定义、费用、资源与源码。正式美术继续使用用户确认的现有资源，28 个新状态角标采用临时线稿。
- [WitchStatuses.Refresh](SpireLab/SpireLabCode/Witch/Mechanics/WitchVisiblePowers.cs) 把一次结算的数据映射到原生 Power。`WitchCombatHooks.AfterFlush` 记录真实保留牌，书签通过实例身份匹配。命运选牌网格需要有玩家归属的战斗牌，确认后清理未入堆的候选。
- [美术导入器](scripts/sync-art.py) 先验证并导出到临时目录，再导入和打包。三张测试源图替换了卡图、遗物和 Power，并从实机 PCK 加载。拒绝不透明能力图时保留原包。缩放与来源约定见 [美术资源包规范](artwork/README.md)。
- 116 项规则与集成窗口断言以及双进程读档通过。实机截图复核了原生选牌与状态栏。测试了 500 条狂赌递减路径。安装与工坊候选 ZIP 已校验，当前发布条目按原状态保留。[验证记录](docs/witch-v1.6-validation.md)
- 后续可用外部美术包替换临时角标，再做完整构筑平衡、Windows 和联网试验。

## 2026-11-20 · V1.6 外部美术验收与整局集成

- 审查外部包全部 139 张 PNG 和双图缩略对照，只导入 U31 与 8 个明确对应 Power 的图标。统一清单记录原图来源、质量和使用许可。被拒图以及旧运行文件保存在可还原快照。[逐项审查](docs/art/witch-v1.6-source-audit.csv)
- [战斗钩子](SpireLab/SpireLabCode/Witch/Mechanics/WitchCombatHooks.cs) 在能量和格挡结算及起手抽牌后刷新状态。下一次可操作窗口看到的是本回合真实数值。原生状态持续由结算状态驱动。
- 工程通过 116 项机制回归、33 项界面测试、新旧档恢复和一次三幕整局 AutoSlay。记录了战败种子与后续通关种子的区别。能力图导入测试覆盖 64 与 256 两种输出，以及透明度错误时的拒绝路径。[集成验收](docs/witch-v1.6-integration.md)
- Steam 作者会话单独确认原条目已设为 Private，再更新三件运行文件。下载后逐字节比对，并在独立游戏副本中复跑 34 项界面和状态检查。预载诊断的失败记录保留，最终严格复测通过。

## 2026-11-21 · V1.6.2 平衡与验收

- 修改欲望牌、反射结算、双语说明、能量小图标和开场选牌。开场网格只显示卡牌定义，按钮确认后才创建一张战斗牌。筛选沿用原版可见行渲染，费用、体系和类型可以组合。
- [战斗钩子](SpireLab/SpireLabCode/Witch/Mechanics/WitchCombatHooks.cs) 使用原版 `DamageResult.BlockedDamage` 做单次反射；加算比例和固定量的先后次序影响取整。[真实生命](SpireLab/SpireLabCode/Witch/Mechanics/WitchLife.cs) 区分伤害和直接支付，胜利清算只读取当前回合的实际支出。
- [实机脚本](scripts/smoke-macos.sh) 将编译中间产物放在单次会话目录，避免并行编译争用共享的 `obj`。在 `v0.111.1` 上合并前通过 116 项战斗、40 项界面、8 项发布防混包检查，以及新档跨进程与 V1.0 旧档恢复。种子 `WITCH10051` 在 AutoSlay 辅助下打完三幕并返回主菜单。[V1.6.2 规则和验证](docs/witch-v1.6-validation.md#v162)
- [发布校验](scripts/verify-witch-release.py) 校验构建标记、文件白名单并逐字节比对正式构建。工坊下载目录恰有三个 Mod 文件。

## 2026-11-21 · V1.6.2 收敛

- 移除实验室启动入口、专用项目和正式代码中的调试追踪。卡牌、资源和版本清单保持原样，保留现有自动化回归及正式发布校验。
- 重新构建 V1.6.2，116 项战斗、40 项界面、8 项发布校验及 500 条狂赌生命路径通过。界面首轮遇到引擎预载诊断，复跑通过。Steam 订阅文件校验值一致。

## 2026-11-22 · 血色终镜 V1

- 在 `WitchCombatHooks.cs` 接入本机 STS2 `v0.111.1` 的 `BeforeDeath` 和 `AfterCombatVictory`，并通过 `TheWitch.ExtraAssetPaths` 提前加载 `finisher/` 的美术、Shader 与音效。`CrimsonFinalMirror.cs` 实现 2.4 秒演出，详细分镜和素材来源见 [镜月剧场笔记](docs/art/crimson-final-mirror.md)。
- 在反编译的 `CombatManager.EndCombatInternal` 中确认 `AfterCombatEnd` 先于胜利演出和存档。《狂赌之渊》在原位置返还真实生命。房间退出时结束演出任务并销毁局部画布。多人和回放跳过本地动画等待。
- 建议读三处。[胜利钩子](SpireLab/SpireLabCode/Witch/Mechanics/WitchCombatHooks.cs#L317)展示玩法与视觉的先后顺序。[演出时间轴](SpireLab/SpireLabCode/Witch/Mechanics/CrimsonFinalMirror.cs#L228)由一个时间值计算揭幕、坍缩、碎片和余韵，碎镜的顶点与 UV 来自同一张纹理。[实机测试](tests/SpireLab.Smoke/WitchFinisherSmoke.cs)通过真实出牌和伤害指令触发胜利，测试绘图和奖励完成状态。
- 实机验证普通攻击、反伤、AOE、多段攻击、毒伤、Boss、战败、连续战斗、缩放、中断清理与奖励。截图采集时先保留帧，退出演出再编码 PNG；帧时间以关闭截图的战斗为准。运行命令为 `./scripts/smoke-macos.sh witch-finisher`，日志与截图保留在本机 `.local/witch-finisher-smoke.*/`。
- 六个角色关键姿态目前是对齐的合层图。后续可细化手指、发束和衣袖的次级运动，并扩大实际敌人轮廓覆盖面。联网多人、回放与 Windows 尚未上机验证。

## 2026-11-22 · 血色终镜 V2 与手动测试入口

- `CrimsonMirrorCamera.cs` 使用真实受击框的归一化坐标、五次缓动与临时镜头变换。原版 `NCombatRoom.SceneContainer`、`BgContainer` 和 `NCombatUi` 的层级通过目标程序集和实机确认。镜界自己的 `StageShot` 接管中后段，并利用材质采样与特写层做视差。
- `CrimsonMirrorPortrait.cs` 管理上半身画面、前后发束、红眼、透视和四个手势。最值得读的是 `Lens.Apply／Restore` 的坐标变换、`Render` 的六段节奏，以及 `Observe` 里对真实游戏镜头、握拳和恢复的断言。真实伤害、死亡、奖励和存档仍由原版负责。
- 首轮带截图时出现冷渲染尖峰并越过镜头探针窗口。纹理仍沿用原版预载，新增首场发牌前的小画布材质预热。实机从首播和重复播放记录帧时间，并用左右槽位与 Boss 检查不同受击位置。
- 根目录 `启动魔女本地测试.command` 调用 `scripts/play-witch-local-macos.sh`。Finder 双击启动真实游戏主菜单，验证 App 副本文件编号和测试用户目录复用。运行时日志与归档截图保留在 `.local/`，详细操作与限制见 [V2 分镜](docs/art/crimson-final-mirror-v2.md)。

## 2026-11-22 · 血色终镜 V3 镜后魔女

- 继续使用目标程序集的致死、战斗结束和胜利钩子，读取真实受击框并保存必要的摄像状态。`CrimsonMirrorCamera.cs` 将原版开镜和镜界镜头分开，新增七段主时间轴、带深度权重的独立图层与弧线抬升。
- `CrimsonMirrorPortrait.cs` 和 `finisher/v3/` 接入用户提供原画的透明半身、直接取自原图并对位的眼眉口表情纹理、前后发束和四格袖手。局部 UV 形变和分区过渡让抬眼、偏头、嘴角与前伸错峰发生。不同纹理利用 mip 层切换焦点。
- `CrimsonFinalMirror.cs` 把灵魂收缩、镜裂、局部光波和预制玻璃音纳入同一时间轴。在掌心与镜核处用各层变换后的坐标连接法术丝线。值得读的三段代码是 `Theatre`、`Render` 中的深度投影，以及 `Observe` 在实机记录的镜头、表情和音效断言。
- 截图回归覆盖普通、反伤、双段、AOE、毒伤中断、Boss、战败、窗口缩放、连续战斗、奖励和生命返还。`.local/crimson-v3/` 保留原图、10 张实机帧和分镜。继续复用既有的 Finder 启动器和独立测试存档。

### 血色终镜 V4 与原初贪婪

- `CrimsonMirrorGarden.cs` 和 `finisher/v4/` 接入倒悬镜庭、荆棘、镜影、袖口柔边与新音轨。查看 `CrimsonMirrorCamera.Theatre` 的镜头连接、`CrimsonMirrorPortrait.Render` 的表情到握拳时间差，以及半身 Shader 如何让头发、衣料、脸部依次溶解。
- 本机 STS2 v0.111.1 的 `CardModel.OnPlayWrapper` 只会移动仍然位于出牌区的牌。`PrimalGreed.OnPlay` 用原版 `CardPileCmd.Add` 在抽五张前移动原实例，沿用原版洗牌顺序 Hook，弃牌只在原版需要补抽时参与重洗。
- `WitchGreedSmoke.cs` 通过原版队列检查支付、抽回和未抽回的两条结算路径，用引用相等比较所有战斗牌堆。`WitchFinisherSmoke.cs` 检查镜头边界、局部退场和握拳同步，并记录帧时间。命令见 [V4 说明](docs/art/crimson-final-mirror-v4.md)。
- Apple Silicon 实机已通过 116 项常规战斗、53 项原初贪婪专项、129 项 V4 演出和 40 项界面检查。围绕原版事件战斗复现了退场时序竞争，改用 `CombatRoom.ParentEventId` 判断事件所属的战斗并保留其原生退场。1600 × 900 无截图回读时，首次正常击杀平均 60.4 FPS，P95 17.31 ms，P99 17.60 ms。
- 同一 AutoSlay 种子通过茂密植被的事件战斗、三幕首领与最终胜利，冷启动读档复测通过。

### 创意工坊宣传素材升级（血色终镜 V4）

- 核对 `ModUploader` v0.2.0 二进制与 `template/README.md`，确认官方上传器强制要求工作区根目录提供小于 1 MB 的 `image.png`，且不支持直接上传 MP4；视频预览需绑定 YouTube ID（`ISteamUGC::AddItemPreviewVideo` 或网页端添加）。
- 在 [export-witch-workshop-media.py](scripts/export-witch-workshop-media.py) 实现可重现管线，直接复用仓库无损 V3/V4 绘画分层与实机录屏 `.local/crimson-v4/evidence/crimson-final-mirror-v4.mp4`，导出 1600×1600 封面母版、800×800 真彩 `< 1 MB` 主封面（943,374 字节）、1920×1080 干净视频封面、10 秒 1080p60 H.264/ProRes 宣传片（含 60 fps 动态片头/片尾与 7.02s 握拳碎镜音画同步）、540×540 12 fps `< 1 MB` 1:1 循环 GIF（770,480 字节）及页面适配审查表。
- [verify-witch-release.py](scripts/verify-witch-release.py) 新增 `media` 发布前校验模式，并在 `workshop` 模式下拒绝伪装为 `image.png` 的非 PNG 文件或超 1 MB 封面；[test_witch_release.py](tests/test_witch_release.py) 增补对应单元测试。
- 已按安全流程将 Steam 创意工坊条目 `3842829456` 先切为 Private，再通过 `ISteamUGC::SetItemPreview` 仅更新主封面为 `cover.gif`（新句柄 `12592709661809387352`）。实测从 Steam CDN 回读确认 `Content-Type: image/gif`、`540×540`、完整保留 29 帧动态且 SHA-256 与本地逐字节一致；`contentSize`（`41331444`）、标题、描述摘要与 BaseLib 依赖 `3737335127` 均未改动，条目保持 Private 待作者验收后公开。

## 2026-11-23 · 魔女 V2.0 卡牌系统迭代

- 基于 V1.6.3（`4f4a99c`）在独立分支 `gemini/witch-v2-cards` 完成 V2.0 增量迭代，完整保留魔法启迪、镜像生命、五张欲望牌、血色终镜 V4 与全套既有美术资源，版本清单升级为 `v2.0.0`。
- 奖励池精简通过 `WitchCard.RetiredSpecIds` / `IsInRewardPool` 统一接入 `WitchCardPool.FilterThroughEpochs` 与 `Enlightenment.ChooseOpeningCard`：9 张暂退卡（`C17 / U02 / U03 / U17 / U25 / U26 / U29 / R06 / R22`）退出战斗奖励、商店与命运选牌，实际活跃卡池为 82 张（76 常规奖励含 5 欲望），同时保留全部 91 定义以确保旧存档兼容。
- 反奏（Counterpoint）在 `WitchRules.IsCounterpoint`、`WitchCombatHooks.BeforeCardPlayed` 与 `AfterCardPlayedLate` 中以单状态位统一实现，覆盖全部 14 张反奏卡（13 张清单卡 + `U07` 灵药瓶）；反射率统一为 `MirrorForm(100%) + BaseReflectPercent + TempReflectPercent + NextReflectPercent` 百分比加法结算，`U21` 反光刻印基础反射成长采用保守共享上限 100%。
- 完成普通 17 张、罕见 19 张、稀有 6 张卡牌重构（含 `C05` 残液去血耗、`U05` 提纯真消耗、`U14` 蓝焰单段伤害强化、`U31` 禁书扉页自选保留、`U34` 半月账本攻击回收、`R15` 空镜能力牌化、`R16` 镜狱 10 伤害下限），同步更新中英 `cards.json`、`powers.json`、`card_selection.json` 与新增可见状态图标。
- 在 Apple Silicon / STS2 `v0.111.1` 上通过 `dev-macos.sh check/build/pack`（零警告零错误）、`SpireLab.Witch.Rules` 单元测试、`test_witch_release.py`（11/11）及全套六项实机回归（`witch` 含 15 项反奏用例与 7 组跨体系连段 A–G、`witch-save` 含暂退卡与 R15 能力牌跨进程读档、`witch-art`、`witch-greed`、`witch-finisher`、`witch-campaign` 三幕通关）。详细记录见 [V2.0 规格与验证记录](docs/witch-v2-validation.md)。

## 2026-11-23 · 魔女 V2.1 动态卡面样板（R10 镜中人）与战斗动态基础

- 基于 V2.0 分支 `gemini/witch-v2-cards` 建立 `gemini/witch-v2-animated-art`，严格执行 Art Direction Lock：**零修改既有卡面与角色立绘原图**，零更改 V2.0 玩法数值与血色终镜 V4 演出；仅交付首张样板卡 `R10` 镜中人与可复用架构，暂不批量改造其余 76 张常规奖励牌。
- 经 ILSpy 确认 `NCard.Reload()` 每次刷新都会重置 `_portrait.Material = null` 再触发 BaseLib `ExtraCardUi` 回调，且 `NCard.Create()` 先于 `NCardHolder.SetCard()` 执行。据此在 [WitchAnimatedCardArt.cs](SpireLab/SpireLabCode/Witch/Cards/WitchAnimatedCardArt.cs) 实现零 Harmony 补丁的惰性控制器，以 `_EnterTree()` + `CallDeferred(RefreshContext)` 兼容节点池复用，区分 Idle（`Material = null` 零开销）、Hover（鼠标/手柄统一聚焦，克制微动）与 Inspect（放大完整 4.2s 循环），并提供 `Full / Reduced / Static` 三档偏好（自动适配 `FastModeType.Instant` 与 headless）及 Animated Art 与未来 Alternate Art 的数据分层。
- [build-witch-r10-masks.py](scripts/build-witch-r10-masks.py) 直接基于未修改的 `r10.png` 生成 4 通道像素锁定遮罩 `r10_masks.png`（R=右镜扫光、G=镜中人深红瞳孔、B=原画既有右下裂纹、A=镜内柔边呼吸区），并在 `.import` 中显式关闭 `process/fix_alpha_border` 避免 Godot 4.5 涂抹零透明区 RGB 数据；配套 `r10_mirror_self.gdshader` 仅使用锁定色板实现 4.2s 无缝错峰循环。同步优化 [combat.tscn](SpireLab/SpireLab/scenes/witch/combat.tscn) 的 3.2s 呼吸重心起伏、错峰镜中形态浮动与出招/受击回落。
- Apple Silicon / STS2 `v0.111.1` 上通过 `dev-macos.sh build/pack`（零警告零错误）、`SpireLab.Witch.Rules`、`test_witch_release.py`（11/11）及六项全量实机回归：新增 `witch-animated-art`（60 项，放大视图 60.0 FPS、`mean=16.66ms`、`p95=17.39ms`；全循环 28 帧对比静止原图确认左侧真实魔女与文字框 `max_diff = 0`）、`witch-finisher`（140 项，V4 处决 60.3–60.4 FPS）、`witch-greed`（53 项）、`witch-art`（39 项）、`witch`（217 项）、`witch-save`（9 项跨进程读档，首轮命中原版引擎 `Common` 预载 `Invalid Task ID`，复跑零错误通过）。样板预览与后续扩展设计见 [V2.1 动态卡面说明](docs/art/witch-v2.1-animated-card-art.md)。
