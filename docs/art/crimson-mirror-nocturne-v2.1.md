# SpireLab — The Witch V2.1《绯镜夜想曲》（Crimson Mirror Nocturne）

适用于 SpireLab V2.1（基线 V2.0 `gemini/witch-v2-cards` / PR #19，分支 `gemini/witch-v2-visual-upgrade`，目标环境 macOS Apple Silicon / STS2 `v0.111.1` / MegaDot `4.5.1-m.14` / BaseLib `3.4.8`）。

本轮严格遵循 **Art Direction Lock（美术方向锁定）**、**V2.0 玩法零修改** 与 **非阻塞流畅优先** 原则：
- **零修改原始正式美术**（`witch_sprite.png`、`witch_mirror_form.png`、`r10.png`、`c01.png` 全部 0 字节改动）。
- **零 Harmony 补丁**，零额外战斗等待，零更改 V2.0 数值/反奏/反射率/五欲望/魔法启迪/旧存档/血色终镜 V4 逻辑。
- **尊重用户交互偏好**：按用户在同轮对话中的明确要求，手牌、牌库、命运选牌、奖励界面与放大查看（`NInspectCardScreen`）均保持 **100% 原版行为**；动态卡面（`R10` 与 `C01` 两张样板）仅在**实际打出卡牌进入出牌区**时自动呈现，结算后立即卸载回收。未在审核前批量改造剩余 76 张奖励牌。

---

## 1. 六大系统实现总览

### 1.1 战斗人物 Live2D 级动态绑定（[WitchLiveRig.cs](../../SpireLab/SpireLabCode/Witch/Mechanics/WitchLiveRig.cs)）
- 采用与 Live2D 同源的 **2D 像素锁定权重遮罩 + GPU 运动学变形 Shader + C# 60 FPS 阻尼弹簧物理**（零原生第三方二进制依赖，macOS/Windows 创意工坊全兼容）：
  - 遮罩脚本：[build-witch-live-rig-masks.py](../../scripts/build-witch-live-rig-masks.py) → `witch_rig_masks.png`、`mirror_rig_masks.png`（`.import` 固定 `process/fix_alpha_border=false`）。
  - 魔女 Shader：[witch_live.gdshader](../../SpireLab/SpireLab/images/witch/live/witch_live.gdshader)（脚踝以下锚死防滑步、胸腔呼吸、颈部铰接偏头点头、精确眼眶坐标自然上眼睑单/双眨眼、后发大卷束/弯尖帽梢红宝石/分层哥特裙摆弹簧惯性波纹、红宝石与眼瞳发光、流派轮廓边光）。
  - 镜中形态 Shader：[witch_mirror_live.gdshader](../../SpireLab/SpireLab/images/witch/live/witch_mirror_live.gdshader)（外层华丽银框保持锐利、镜内倒影异步呼吸、冷银斜向扫光、`R10` 镜像觉醒、`U21` 反光刻印镜框铭文发光——**零新增节点**）。
  - 四种可复用人物施法姿态：`Gesture.PotionCast`（抬手施法）、`Gesture.MirrorSlice`（指尖切割）、`Gesture.GrimoireManipulate`（翻书操纵）、`Gesture.MirrorResonance`（镜像共鸣）；连续受击叠加速度不重置回原点。

### 1.2 统一非阻塞战斗 VFX 调度与音频预算（[WitchCombatVfx.cs](../../SpireLab/SpireLabCode/Witch/Mechanics/WitchCombatVfx.cs) + [WitchFeedback.cs](../../SpireLab/SpireLabCode/Witch/Mechanics/WitchFeedback.cs)）
- **严格并发与降级**：完整档上限 18 活跃节点、简化档上限 8；饱和时仅丢弃 P3 装饰效果，绝不遮挡 P0 数值/血条/手牌；`CombatEnded` 立即清空所有瞬态轨迹。
- **音频策略**：已按用户要求移除 V2.1 新增的 7 组合成特效音效，仅保留原有 5 组经典魔女音效（`desire / energy / enlighten / mirror / reflect.wav`）与血色终镜 V4 音轨，并对既有音效施加声道并发上限（5）与 `65ms` 节流防叠音。
- **全语义事件适配**：严格由真实战斗结算驱动（真实命中段数、真实反射、真实生命支付、真实提纯消耗、真实抽/弃/耗牌与 `CombatEnded`）。

### 1.3 五组代表样板与特色卡牌实现

| 体系 / 主题 | 代表牌与已落地表现 |
| --- | --- |
| **血色炼金（魔药）** | `C01` 药剂投掷（旋转红液玻璃瓶抛物线 + 碎玻璃与红液飞溅 + `C01` 动态卡面）、`C06` 浓缩火焰（掌心压缩→定向红焰射流）、`U10` 沸腾回响（按真实 `AfterDamageGiven` 触发 1→2→3 段递进爆裂）、`R08` 终末蒸馏（红丝汇聚成红宝石晶体→短促内爆碎裂） |
| **银镜折光（镜子）** | `C11` 银幕（半透明冷银哥特镜面展开与斜向折光）、`U18` 镜面迷宫（4 面错位冷银镜片棱镜空间）、`U21` 反光刻印（镜框发光铭文随基础反射率递增，零额外节点）、`R10` 镜中人（觉醒冲击波 + 镜中人异步呼吸 + `R10` 动态卡面）、`R16` 镜狱（按真实 2 段命中交替左右斜斩）、通用格挡冷银镜盾与真实反射银红折射光束 |
| **禁书秘仪（魔法书）** | `C19` 书页飞刃（羊皮纸书页扇形展开→银墨飞刃直击）、`U05` 提纯（真实消耗成功时虚影折叠压缩成炼金符印→跃起能量星点）、抽牌（合并翻页虚影）、弃牌（浅灰墨页坠落）、消耗（细红丝线解离化灰，与弃牌清晰区分） |
| **人格侵染（欲望）** | `U37` 原初贪婪（镜影洗牌涡旋，不遮挡真实手牌）、`U38` 怠惰（冷银双镜合拢柔幕，非阻塞不延迟结束回合）、`R25` 堕落自信（酒红契约裂纹法阵与暗红花瓣）、`R26` 灾祸之源（单镜裂作三片带红印碎镜）、`R27` 狂赌之渊（真实生命支付触发血条→掌心红丝汲取，高频连拍自动提速缩短） |
| **反奏接力（Counterpoint）** | 连续追踪上一张法术体系色（魔法书象牙白→镜子冷银→魔药石榴红），真实反奏成立时生成流畅的跨色衔接丝带与双拍共鸣环 |

### 1.4 动态卡面样板（R10 与 C01）
- **`R10` 镜中人**：[r10_masks.png](../../SpireLab/SpireLab/images/witch/cards/animated/r10/r10_masks.png) + [r10_mirror_self.gdshader](../../SpireLab/SpireLab/images/witch/cards/animated/r10/r10_mirror_self.gdshader)（4.2s 无缝循环；左侧真实魔女 0 像素变动）。
- **`C01` 药剂投掷**：[build-witch-c01-masks.py](../../scripts/build-witch-c01-masks.py) → [c01_masks.png](../../SpireLab/SpireLab/images/witch/cards/animated/c01/c01_masks.png) + [c01_potion_toss.gdshader](../../SpireLab/SpireLab/images/witch/cards/animated/c01/c01_potion_toss.gdshader)（3.4s 无缝循环：悬浮玻璃药瓶微幅浮沉、内部红液轻晃、瓶壁冷银扫光、右侧红丝流光；严格保护左侧魔女面部与前伸手指）。

---

## 2. 实机预览成果

- **九宫格《绯镜夜想曲》实机样板总览（C01 / R10 / C11 / C19 / 反奏+U18 / 反奏+U10 / R27 / R08 / 反射+格挡+提纯）**：[crimson-mirror-nocturne-samples.png](witch-v2.1/crimson-mirror-nocturne-samples.png)
- **魔女本体 + 镜中人 Live2D 动态循环 GIF**：[witch-live-rig-loop.gif](witch-v2.1/witch-live-rig-loop.gif)
- **R10 六态像素对照与循环 GIF**：[r10-states-comparison.png](witch-v2.1/r10-states-comparison.png) / [r10-inspect-loop.gif](witch-v2.1/r10-inspect-loop.gif)

---

## 3. 性能基线与压测数据（macOS Apple Silicon / STS2 `v0.111.1` / `1200×675` Retina）

同种子（`WITCHV21CMN`）、同房间、同分辨率实测对比：

| 场景 | 平均 FPS | 1% Low FPS | 平均帧时 | P95 帧时 | P99 帧时 | 活跃 VFX 峰值 / 丢弃 |
| --- | --- | --- | --- | --- | --- | --- |
| **Static 基线（等同 V2.0 关闭 V2.1 动效）** | **59.9 FPS** | **56.4 FPS** | `16.70 ms` | `17.57 ms` | `17.73 ms` | `0 / 0` |
| **V2.1 完整档（魔女 Live2D + 镜中形态常驻）** | **60.0 FPS** | **55.4 FPS** | `16.68 ms` | `17.19 ms` | `18.05 ms` | `0 / 0` |
| **V2.1 10 连高频交替出牌压测（64 次 VFX / 55 次音效）** | **58.4 FPS** | **28.2 FPS** | `17.13 ms` | `17.56 ms` | `35.47 ms` | `6 / 0`（远低于 18 上限） |

- 主线程常规增量接近 0（`16.70 ms → 16.68 ms`）；10 连高压突发期间 P95 仍稳定在 `17.56 ms`（零连续卡顿，零 P3 触发饱和，出牌响应无额外等待）。
- 战斗结束（`CombatEnded`）与返回主菜单后：活跃卡牌控制器 `0`、活跃角色 Rig `0`、活跃 VFX 节点 `0`。
