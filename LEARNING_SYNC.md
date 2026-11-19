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
