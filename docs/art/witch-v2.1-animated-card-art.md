# 魔女 V2.1：动态卡面与战斗角色动态基础（R10 样板）

适用于 SpireLab V2.1（基线 `gemini/witch-v2-cards` / V2.0，目标环境 macOS Steam STS2 `v0.111.1`、MegaDot `4.5.1-m.14`、BaseLib `3.4.8`）。

本阶段严格遵循 **Art Direction Lock（美术方向锁定）**，**不修改、不覆盖任何既有卡面或角色立绘原图**，亦不触碰 V2.0 任何玩法数值与血色终镜 V4 处决链路。本阶段仅完成：

1. **可复用动态卡面架构**（零 Harmony 补丁，区分 Normal/Static、Hover/Controller Focus、Enlarged Inspect 三状态与 Full / Reduced / Static 三模式，并预留 Animated Art 与未来 Alternate Art 的数据结构分离）。
2. **第一张样板卡 `R10` 镜中人（The Mirror Self）** 的高精度遮罩、无缝循环 Shader 与全界面实机验收。
3. **战斗场景 `combat.tscn` 基础自然动态打磨**（3.2s 呼吸重心起伏、出招/受击顺滑回落、镜中形态不同步浮动；零假骨骼扭曲）。
4. **后续扩展设计案**（第二张样板 `C01` 抛掷魔药规划、五类出牌反馈 VFX 提案、未来角色拆分层资源清单）。

> 未经用户审核 R10 样板前，**不批量改造其余 76 张常规奖励牌**。

---

## 1. 美术方向锁定（Art Direction Lock）

实机制作前逐一复核了既定视觉锚点：
- 角色母版与基准：`docs/art/witch-v2/reference/user-visual-reference.png`、`docs/art/witch-v2/source/character-bible.png`
- 战斗立绘：`SpireLab/SpireLab/images/witch/witch_sprite.png`、`witch_mirror_form.png`
- 代表卡面：`r10.png`（镜子核心）、`r25.png`（欲望核心）、`c01.png`（魔药基础）、`u37.png`（魔法书代表）

锁定色板（Shader 内仅使用此调色板，不引入彩虹霓虹、廉价全息反光或廉价火焰粒子）：
- 墨黑 `#1C161D` / 木炭灰 `#302832`
- 象牙 `#EADDE1` / 冷银 `#CDD8E2`
- 酒红 `#78233D` / 石榴红 `#CF486A`

原画保护证明：`SpireLab/SpireLab/images/witch/cards/r10.png` 保持 0 字节改动；所有动态效果均在运行时通过独立 `ShaderMaterial` + 独立遮罩纹理叠加，静态及关闭动效时直接卸载材质（`Material = null`），像素级还原原画。

---

## 2. 技术架构（零 Harmony 补丁）

经 `ilspycmd` 反编译确认 STS2 `v0.111.1` 与 BaseLib `3.4.8` 生命周期：
- `NCard.Reload()` 每次刷新都会重置 `_portrait.Material = null`，随后调用 BaseLib 注册的 `ExtraCardUi` `onReload` 回调。
- `NCard.Create()` 先执行 `Reload()`，再由 `NCardHolder.SetCard()` 挂载到节点树；因此动态控制器采用 `_EnterTree()` + `CallDeferred(RefreshContext)` 延迟绑定父级上下文，兼容节点池复用。
- `NCardHolder` 通过 `FocusEntered/FocusExited` 与 `Hitbox.Focused/Unfocused` 统一派发鼠标悬停与手柄/键盘焦点。

核心实现位置：[WitchAnimatedCardArt.cs](../../SpireLab/SpireLabCode/Witch/Cards/WitchAnimatedCardArt.cs)，在 [WitchCardAppearance.cs](../../SpireLab/SpireLabCode/Witch/Cards/WitchCardAppearance.cs) 的现有 `ExtraCardUi` 钩子中惰性挂载，并通过 [WitchCharacter.cs](../../SpireLab/SpireLabCode/Witch/WitchCharacter.cs) 的 `TheWitch.ExtraAssetPaths` 预载资源。

### 2.1 三级运行状态与性能约束

| 状态 | 触发条件 | 渲染策略 | 每帧开销 |
| --- | --- | --- | --- |
| **Idle（默认静止）** | 手牌静止、奖励静止、选牌静止、非焦点状态 | `portrait.Material = null`，`SetProcess(false)` | **0**（零 Shader、零 `_Process`） |
| **Hover（克制微动）** | `NCardHolder` 鼠标悬停或手柄/键盘聚焦 | 挂载动态材质，`is_inspect = 0.0`；仅启用克制镜面冷银扫光、微弱裂纹与瞳孔回光，关闭镜内呼吸位移 | 仅当前聚焦单卡运行 |
| **Inspect（放大完整循环）** | 位于可见的 `NInspectCardScreen` 内 | 挂载动态材质，`is_inspect = 1.0`；完整 4.2 秒无缝周期循环（含 `< 1.05 px` 镜内低频呼吸感与微尘） | 仅放大单卡运行 |

### 2.2 动态偏好与降级模式

`WitchAnimatedCardArt.Mode` 支持三档：
- `Full`（默认）：手牌/奖励悬停微动 + 放大完整动态。
- `Reduced`（简化）：自动适配 STS2 `FastModeType.Instant`，禁用手牌/奖励悬停动态，放大视图仅保留低频静态感微光（零呼吸位移、零漂浮微尘）。
- `Static`（完全关闭）：全局 `Material = null`；无头渲染环境（`DisplayServer.GetName() == "headless"`）自动切换。
- 支持环境变量 `SPIRELAB_WITCH_ART_MODE=full|reduced|static` 供测试或玩家显式覆盖。

### 2.3 Animated Art vs Alternate Art 数据分层

`CardArtProfile` 将**动态动画配置**（`ShaderPath`、`MaskPath`、`LoopDurationSeconds`）与 `CardArtVariant`（`Original` / 未来 `Alternate` 异画路径）显式分离，后续为重点卡（如欲望牌或稀有核心）引入独立异画时无需重构控制器或更改卡牌稳定 ID。

---

## 3. 样板卡 `R10` 镜中人（The Mirror Self）

### 3.1 像素锁定四通道遮罩

由 [build-witch-r10-masks.py](../../scripts/build-witch-r10-masks.py) 直接基于未修改的 `r10.png`（`500 × 380`）可重现生成，输出 `SpireLab/SpireLab/images/witch/cards/animated/r10/r10_masks.png`：

| 通道 | 含义 | 精度与边界保护 |
| --- | --- | --- |
| **R** | 镜面玻璃扫光区 | 严格限定在右侧镜框内玻璃区域；对左侧真实魔女施加 82% 脸部保护与零溢出硬裁切，避开华丽银框 |
| **G** | 镜中魔女深红虹膜区 | 精确覆盖右侧镜中人深红眼瞳（41 px，`x=[308..325], y=[130..148]`），真实魔女眼部权重恒为 0 |
| **B** | 原画既有镜面裂纹 | 直接提取原画右下角已绘制银白裂纹高光（913 px），不凭空伪造新裂痕 |
| **A** | 镜内柔边呼吸区 | 深度羽化至镜内深色背景与倒影核心，严格排除镜框、接触手指与裂纹平面，杜绝拉扯锯齿 |

> **Godot 4.5 导入关键点**：在 `r10_masks.png.import` 中显式固定 `process/fix_alpha_border=false`，防止引擎把 RGB 数据通道涂抹到 `A == 0` 的区域（尤其是裂纹与扫光边缘）。

### 3.2 4.2 秒无缝周期错峰编排

实现于 `SpireLab/SpireLab/images/witch/cards/animated/r10/r10_mirror_self.gdshader`（`loop_phase ∈ [0, 1)`，所有分量严格首尾衔接）：
1. **镜面冷银斜向扫光（峰值 `phase ≈ 0.28`）**：薄而锐利的冷银反光自右上滑向左下玻璃内区。
2. **镜中人深红眼眸回光（峰值 `phase ≈ 0.54`）**：仅镜中人的深红虹膜短暂回亮，真实魔女保持冷静静止，突出“镜中倒影有独立意志”的叙事。
3. **既有裂纹银白瞬闪（峰值 `phase ≈ 0.72`）**：沿原画右下裂纹线掠过冷银反光。
4. **极轻微镜内呼吸与微尘（仅 Inspect + Full）**：最大 UV 偏移 `≤ 1.05 px`，搭配 4 枚低对比度冷银/暗红微尘在玻璃内缓慢浮沉。

### 3.3 实机视觉对比与像素锁死证明

所有截图均采自真实 STS2 游戏引擎窗口实测（`./scripts/smoke-macos.sh witch-animated-art`，Retina `2560 × 1440`）：

- **循环动态预览（4.2s 无缝循环 GIF）**：[r10-inspect-loop.gif](witch-v2.1/r10-inspect-loop.gif)
- **六态精细对照（原图静止 vs 起始 / 扫光 / 瞳孔回光 / 裂纹闪光 / 回落）**：[r10-states-comparison.png](witch-v2.1/r10-states-comparison.png)
- **多 UI 场景对照（手牌静止 / 手牌悬停 / 升级中文 `镜中人+` / 奖励界面悬停）**：[r10-ui-contexts.png](witch-v2.1/r10-ui-contexts.png)

全循环 28 帧逐像素对比静止原图（`03-inspect-static-original.png`）结果：
- **左侧真实魔女区域（`x < 1260`）**：28 帧并集 `max_diff = 0`，`changed_px(>1) = 0`（**100% 像素零污染**）。
- **卡面文字与说明框区域**：28 帧并集 `max_diff = 0`，`changed_px(>1) = 0`（**零字迹模糊、零辉光污染**）。
- **右侧镜面玻璃区域（`x ∈ [1273..1585], y ∈ [291..749]`）**：平滑在 `max_diff = 66`（回落）至 `160`（裂纹峰值）间循环。
- **放大视图实时帧率（60 帧连续实测）**：**60.0 FPS**（`mean = 16.66 ms`，`p95 = 17.39 ms`，`p99 = 17.40 ms`）。

---

## 4. 战斗角色自然动态基础与后续拆分层清单

### 4.1 本阶段安全落地（`combat.tscn`）

在不动 `witch_sprite.png`、`witch_mirror_form.png` 与现有 `HealthAnimation`（低血脉冲）的前提下，优化了 [combat.tscn](../../SpireLab/SpireLab/scenes/witch/combat.tscn)：
- **`idle` 升级为 3.2 秒缓入缓出呼吸循环**：轻微重心左右偏移与 1.4% 纵向呼吸缩放，告别原先 1.8s 线性折返感；`MirrorFormVisual` 使用错峰相位（不同步偏移与轻微旋转），与 R10 镜中人“不同步倒影”设定呼应。
- **`attack` / `cast` / `hit` 动作曲线平滑化**：使用缓动过渡与更自然的回落收尾，均通过原有 `ReturnToIdle()` 安全衔接待机。
- 严格不做会扭曲帽子、手掌或五官的网格假骨骼形变。

### 4.2 未来角色骨骼/表情升级所需分层美术清单

若后续阶段计划为战斗立绘增加真实头发/裙摆次级摆动或实时切换母版六种表情，需要美术端提供同脚底基线、统一画布尺寸（建议 `782 × 876` 2× 母版导出至 `391 × 438`）的透明 PNG 拆分层，而非对单张合图强行切块：

1. `back_hair.png`（后发卷束，独立于躯干以支持风动/呼吸延迟）
2. `body_dress.png`（主体束腰、外裙与双靴，作为根骨骼）
3. `front_skirt_lace.png`（前层象牙蕾丝裙摆，用于微幅摆动）
4. `head_base.png` + `hat.png`（宽檐弯尖帽与黑玫瑰/蝴蝶结独立分层，避免点头时帽檐切脸）
5. `face_expressions/`（按角色母版 6 种标准表情对齐：`neutral` / `smirk` / `yandere` / `mirror_laugh` / `weak` / `cast_focus`，以及单独 `eyes_blink.png` 闭眼帧）
6. `arm_front.png` + `sleeve_lace.png`（前臂与白褶袖口，支持施法抬手与持书/魔药动作）
7. `mirror_frame.png` + `mirror_reflection.png`（镜框与镜中倒影分离，使镜面可独立做 Shader 扫光与倒影异步呼吸）

---

## 5. 后续扩展方案（待 R10 样板审核后执行）

### 5.1 第二张样板卡规划：`C01` 抛掷魔药（Potion Toss）

作为“魔药流派”的高频基础攻击牌，`C01` 将验证**液体晃动 + 玻璃反光 + 暗红魔力光晕**的可复用模板：
- **通道分配**：`R` = 瓶内暗红药液区（轻微水平波纹与高光涌动）；`G` = 玻璃瓶身斜向冷银高光；`B` = 瓶口/暗红魔力细丝脉动；`A` = 背景魔药烟雾微动（严格避开魔女手部与轮廓）。
- **周期**：3.4 秒循环；Hover 仅启用玻璃反光与液面微亮，Inspect 启用药液微晃与暗红细丝。

### 5.2 五类出牌差异化反馈 VFX 设计案（仅方案，未改动玩法代码）

延续 [WitchFeedback.cs](../../SpireLab/SpireLabCode/Witch/Mechanics/WitchFeedback.cs) 的非阻塞轻量节点架构，将出牌视觉分为五组可区分语义（均受 `Reduced / Static` 降级模式约束，单次特效时长控制在 `0.28s–0.45s` 以内，绝不拖慢出牌节奏）：

| 类别 | 视觉特征 | 色板 | 触发入口 |
| --- | --- | --- | --- |
| **魔药（Potion）** | 玻璃碎光弧线 + 暗红液滴溅散（攻击向目标抛射；技能在自身周围形成炼金圆环） | `#78233D` / `#CF486A` / `#CDD8E2` | 魔药体系出牌 |
| **镜子（Mirror）** | 冷银多边形镜片展开与折返光墙（格挡时生成短促银镜护盾，反射时向攻击者弹出细锐冷银折射线） | `#CDD8E2` / `#EADDE1` | 镜子体系与反射触发 |
| **魔法书（Grimoire）** | 象牙色残页虚影翻飞 + 银墨符文轨迹（过牌/保留时于抽牌堆或手牌边沿亮起轻盈银印） | `#EADDE1` / `#C8B9C1` | 魔法书体系与保留触发 |
| **欲望（Desire）** | 暗红裂缝脉冲 + 低饱和酒红暗角压迫（仅 0.35s，与五张欲望牌的禁忌感统一，不遮挡血条与手牌） | `#1C161D` / `#78233D` / `#CF486A` | 五张欲望牌打出 |
| **反奏（Counterpoint）** | 双重银红节拍波纹（首圈暗银、0.08s 后外圈石榴红呼应，直观提示“与上一张牌类型不同步触发反奏”） | `#CDD8E2` → `#CF486A` | `WitchCombatHooks` 反奏成立时 |

### 5.3 批量动态分级策略（审核通过后分批推进）

- **Tier 1（精修独立遮罩 + 定制 Shader）**：约 10–12 张核心代表卡（`R10`、`R01`、`R25`、五张欲望牌、两张古代牌 `A01/A02`、高频核心如 `C01`、`U37`）。
- **Tier 2（流派复用 Shader + 自动辅助遮罩 + 人工质检）**：镜子/魔药/魔法书重点罕见与稀有卡（约 20 张）。
- **Tier 3（克制悬停流光模板）**：其余常规牌，严格保持默认静止零开销，避免同质化视觉疲劳。
