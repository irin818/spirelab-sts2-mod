# SpireLab — The Witch v2.1.0 项目版本与 Codex 交接报告

> 本文档作为 **v2.1.0（V2.0 卡牌系统迭代 + V2.1《绯镜夜想曲》战斗美术升级）** 收敛至 `main` 并发布 Steam 创意工坊后的权威基线记录，供 OpenAI Codex 或其他开发代理随时快速接续。

- **项目名称**：SpireLab — The Witch（《杀戮尖塔 2》原创角色 Mod）
- **当前版本**：`v2.1.0`（`SpireLab/SpireLab.json`）
- **目标环境**：Slay the Spire 2 `v0.111.1` / MegaDot `4.5.1-m.14` / .NET `net9.0`（构建 SDK `10.0.404`）/ BaseLib `3.4.8`
- **主分支基线**：`main`（已完整合并 V2.0 与 V2.1 全部已验证改动，清理旧开发分支）
- **Steam 创意工坊条目**：`3842829456`（依赖 BaseLib `3737335127`，保留 540×540 动态 `cover.gif` 封面与双语简介）
- **工作区路径**：`/Users/irin/Documents/ChatGPT/杀戮尖塔2mod`

---

## 1. 当前版本完整能力清单（v2.1.0）

### 1.1 玩法系统（V2.0 完整基线，零破坏）
- **角色与固有能力**：68 生命、魔法启迪（开场命运选牌 + 每回合首次手牌交换）、真实生命 / 镜像生命 / 生命逆转、9 遗物、3 药水、2 古代牌。
- **精简活跃卡池**：模型注册保留全部 **91 张卡牌定义**（100% 兼容旧存档与旧牌型迁移），9 张暂退卡（`C17 / U02 / U03 / U17 / U25 / U26 / U29 / R06 / R22`）通过 `WitchCard.RetiredSpecIds` / `IsInRewardPool` 退出奖励、商店与命运选牌；活跃卡池为 **82 张**（4 基础 / 19 普通 / 32 罕见 / 25 稀有含 5 欲望 / 2 古代，76 张常规奖励牌）。
- **反奏（Counterpoint）**：统一判定于 `WitchRules.IsCounterpoint` 与 `WitchCombatHooks`，覆盖全部 14 张反奏卡（13 张核心清单卡 + `U07` 灵药瓶）。
- **反射率统一结算**：`MirrorForm(100%) + BaseReflectPercent + TempReflectPercent + NextReflectPercent` 百分比加法结算，`U21` 反光刻印成长上限 100%。
- **五张欲望牌与血色终镜 V4**：完整保留（原初贪婪 `U37`、怠惰 `U38`、堕落自信 `R25`、灾祸之源 `R26`、狂赌之渊 `R27`；血色终镜 V4 处决动画与事件战斗兼容逻辑零改动）。

### 1.2 战斗视觉与动态系统（V2.1《绯镜夜想曲》）
- **零 Harmony 补丁，零原始美术修改**：`witch_sprite.png`、`witch_mirror_form.png`、`r10.png`、`c01.png` 及全部 91 张原画均为 0 字节改动；色彩严格锁定哥特色板（`#1C161D / #302832 / #EADDE1 / #CDD8E2 / #78233D / #CF486A`）。
- **魔女本体 & 镜中形态 Live2D 级动态**（`SpireLabCode/Witch/Mechanics/WitchLiveRig.cs` + `images/witch/live/`）：
  - 像素锁定四通道权重遮罩（`scripts/build-witch-live-rig-masks.py`，`.import` 关闭 `process/fix_alpha_border`）。
  - GPU 运动学变形 + C# 60 FPS `Spring1D` 弹簧阻尼物理：双靴锚死地面零滑步、胸腔呼吸、颈部铰接偏头/点头、精确眼眶坐标自然单/双眨眼、后发大卷束/弯尖帽梢红宝石/分层哥特裙摆惯性滞后波纹、红宝石/深红眼瞳微光、4 组流派施法姿态。
  - 镜中形态异步呼吸、冷银斜向扫光、`R10` 觉醒态、以及 `U21` 反光刻印镜框发光铭文（零额外节点）。
- **统一非阻塞哥特战斗特效**（`SpireLabCode/Witch/Mechanics/WitchCombatVfx.cs` + `WitchFeedback.cs`）：
  - 并发上限（Full 18 / Reduced 8，饱和仅降级 P3 装饰效果），`CombatEnded` 即时清理。
  - 覆盖 14+ 张代表牌专属视觉（`C01 / C06 / U10 三段递进 / R08 / C11 / U18 / R16 双段交替 / C19 / U05 提纯折印 / U37 / U38 / R25 / R26 / R27 真实生命汲取`）、通用冷银镜盾与银红反射光束、抽/弃/耗牌清晰区分、以及**连续反奏跨色视觉接力丝带**。
  - **音频收敛**：已移除 V2.1 临时合成的新增特效音，仅保留原有 5 组经典魔女音效（`desire / energy / enlighten / mirror / reflect.wav`）及血色终镜 V4 原有音轨，并保留声道并发（上限 5）与 `65ms` 节流保护。
- **纯出牌触发动态卡面**（`SpireLabCode/Witch/Cards/WitchAnimatedCardArt.cs`）：
  - 按用户要求，手牌、牌库、命运选牌、奖励界面与放大查看保持 **100% 原版行为**（`Material = null`，零空闲开销）。
  - 已完成两张样板卡：**`R10` 镜中人** 与 **`C01` 药剂投掷**（`scripts/build-witch-r10-masks.py`、`scripts/build-witch-c01-masks.py`），仅在实际打出进入 `NCardPlayQueue` / `PlayContainer` 时激活并自动回收。
  - 全局支持 `Full / Reduced / Static` 三档质量模式（自动适配 `FastModeType.Instant` 与 headless，可通过 `SPIRELAB_WITCH_ART_MODE` 覆盖）。

---

## 2. 关键代码与文档索引（供 Codex 快速定位）

| 模块 | 关键路径 |
| --- | --- |
| Mod 清单与入口 | `SpireLab/SpireLab.json`、`SpireLab/SpireLabCode/Witch/WitchCharacter.cs` |
| 卡牌与出牌动态 | `SpireLab/SpireLabCode/Witch/Cards/WitchCard.cs`、`WitchCardAppearance.cs`、`WitchAnimatedCardArt.cs` |
| 战斗钩子与规则 | `SpireLab/SpireLabCode/Witch/Mechanics/WitchCombatHooks.cs`、`WitchRules.cs`、`WitchLife.cs` |
| Live2D 角色与战斗 VFX | `SpireLab/SpireLabCode/Witch/Mechanics/WitchLiveRig.cs`、`WitchCombatVfx.cs`、`WitchFeedback.cs` |
| 血色终镜 V4 | `SpireLab/SpireLabCode/Witch/Mechanics/CrimsonFinalMirror.cs`、`CrimsonMirrorCamera.cs`、`CrimsonMirrorPortrait.cs`、`CrimsonMirrorGarden.cs` |
| 着色器与遮罩 | `SpireLab/SpireLab/images/witch/live/`、`images/witch/cards/animated/{r10,c01}/` |
| 可重现资源脚本 | `scripts/build-witch-live-rig-masks.py`、`scripts/build-witch-r10-masks.py`、`scripts/build-witch-c01-masks.py` |
| 自动化回归与发布校验 | `scripts/dev-macos.sh`、`scripts/smoke-macos.sh`、`scripts/stage-witch-release.sh`、`scripts/verify-witch-release.py`、`tests/SpireLab.Smoke/` |
| 设计与实机验证档案 | `docs/witch-v2-validation.md`、`docs/art/crimson-mirror-nocturne-v2.1.md`、`docs/art/witch-v2.1/`、`LEARNING_SYNC.md` |

---

## 3. 最终发布验收证据（macOS Apple Silicon / STS2 `v0.111.1` / `ExportRelease`）

| 验收项 | 命令 | 结果 |
| --- | --- | --- |
| 环境检查 / 构建 / 正式打包 | `./scripts/dev-macos.sh check && build && pack` | **PASS**（0 Warning, 0 Error，确认 `SpireLab.pck` 零 `audio/witch/vfx` 残留） |
| 纯规则与发布防混包单测 | `SpireLab.Witch.Rules` & `test_witch_release.py` | **PASS**（500 条狂赌路径 + 11/11 发布校验） |
| V2.1 基准 + 九样板 + 10 连压测 | `./scripts/smoke-macos.sh witch-animated-art` | **PASS（0 错误）**；Static 基线 `60.6 FPS (1%Low 58.9)` vs V2.1 完整档 `60.0 FPS (1%Low 58.9, p95 16.84ms)`；10 连压测 `58.4 FPS (p95 17.52ms, peakVfx=6, droppedP3=0)` |
| 界面美术与状态栏 | `./scripts/smoke-macos.sh witch-art` | **PASS（39 项，0 错误）** |
| 原初贪婪专项 | `./scripts/smoke-macos.sh witch-greed` | **PASS（53 项，0 错误）** |
| 血色终镜 V4 处决 | `./scripts/smoke-macos.sh witch-finisher` | **PASS（140 项，0 错误，59.8–60.5 FPS）** |
| 跨进程冷启读档 | `SPIRELAB_SMOKE_WINDOWED=1 ./scripts/smoke-macos.sh witch-save` | **PASS（双进程 9 项，0 错误）** |
| 全战斗与连段回归 | `SPIRELAB_SMOKE_WINDOWED=1 ./scripts/smoke-macos.sh witch` | **PASS（217 项，0 错误）** |
| 三幕整局通关 | `SPIRELAB_SMOKE_WINDOWED=1 ./scripts/smoke-macos.sh witch-campaign` | **PASS（Act 1/2/3 Boss 全胜返回主菜单，0 错误）** |

## 4. 已知边界与后续可扩展方向（Codex 接续建议）

1. **未覆盖平台**：本版本验证均在 macOS Apple Silicon（STS2 `v0.111.1`）完成；Windows 实机与联网多人联机尚未上机验证。
2. **批量出牌动态卡面扩展**：目前仅对 `R10` 与 `C01` 两张代表卡启用出牌动态卡面（其余 76 张常规奖励牌按要求保持静态原图）；后续可按需在 `WitchAnimatedCardArt.Profiles` 中增量注册核心卡（如 `R01`、`R25`、五张欲望牌与两张古代牌），直接复用同一零 Harmony 控制器。
3. **未来真骨骼/多表情立绘升级**：当前 Live2D 级效果基于单张合图 + 像素权重遮罩实现；若未来美术端提供拆分层 PNG（后发、躯干、前裙蕾丝、头帽分离、6 表情切片、前臂袖口），详见 `docs/art/witch-v2.1-animated-card-art.md` 第 4.2 节清单，可直接平滑替换 `combat.tscn` 而无需改动玩法或 VFX 调度层。
4. **原生预载偶发诊断说明**：STS2 原版引擎在多进程冷启预载 `Common` 资源时偶发打印 `worker_thread_pool.cpp:418 ERROR: Invalid Task ID`（早于 Mod 资源加载），属引擎既有现象，复跑即可零错误通过。
5. **Steam 创意工坊更新提醒**：条目 `3842829456` 线上使用动态 `cover.gif` 主封面；后续仅更新 Mod 运行文件时请继续使用 `ISteamUGC::SetItemContent` 路径（避免官方 `ModUploader upload` 覆盖 GIF 封面），并在上传前跑 `python3 ./scripts/verify-witch-release.py workshop <dir>`。
