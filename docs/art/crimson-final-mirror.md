# 血色终镜 · 镜月剧场 V1

魔女在本地单人战斗胜利时播放一次。卡牌、敌人死亡、反伤、生命返还和存档仍走原版流程。

## 分镜

| 秒 | 画面与声音 |
| --- | --- |
| 0.00–0.25 | 战场压暗，银紫镜缝亮起，魔女凝视，猩红眼光一闪。 |
| 0.25–0.80 | 手臂抬起，镜缝从正中向两侧披开，波动的液态镜缘让对称镜廊显影。 |
| 0.80–1.20 | 张掌，银白镜月和独立的玫瑰银镜叠入。掌心法阵以细线牵住镜中灵魂。 |
| 1.20–1.60 | 收指，灵魂、咒线和裂纹向中心收紧。月光转红，镜内形成小型血色焦点。 |
| 1.60–1.85 | 瞬间握拳，主镜的 13 片带原镜纹理的大碎片和 19 枚银红碎屑向外飞散。画面轻推，奏响玻璃与低频冲击。独立的镜月保持完整。 |
| 1.85–2.40 | 放下手臂，碎镜与镜廊渐隐，交还原版胜利奖励。 |

## 素材与追踪

- `SpireLab/SpireLab/images/witch/finisher/` 保存独立舞台画、银镜、银月、通用灵魂和六张对齐的魔女姿态。均为本项目生成的原创 V1 绘画资产。角色姿态以 `docs/art/witch-v2/source/character-bible.png` 和现有战斗立绘为参考。规格提到的另一张会话插画在本机不可用，使用了规格中的文字风格描述。
- `theatre.jpg` 为 1408×792。魔女各姿态为 310×512 透明 WebP，银镜为 512×768 透明 WebP。镜月和灵魂独立裁切，最长边 512 像素。
- 原始生成图留在本机 `.local/crimson/source/`，含四张 PNG 和 `sha256.txt`。运行图由原图裁切、调整 Alpha 并缩放。舞台、银镜、月亮、灵魂和魔女各自可替换，角色各姿态本身为合层位图。
- `SpireLab/SpireLab/audio/witch/finisher/` 的两条合成音轨分别承载仪式铺底和玻璃／低频冲击。本机 `.local/crimson/prepare-art.py` 保留采样和包络参数。播放音量读取游戏主音量和音效设置。
- 下一轮可替换分层头发、袖口、手腕和指节，在保留六个关键姿态的前提下细化次级运动。优先检视收指过渡、舞台两翼的裂纹和真实敌人轮廓覆盖面。

## 运行与兼容性

- 以本机游戏 `v0.111.1` 的 `sts2.dll` 为准。`CombatManager.EndCombatInternal` 先等候 `Hook.AfterCombatEnd`，再调用 `Hook.AfterCombatVictory`，随后标记房间完成并存档、发放奖励。`WitchCombatHooks` 的生命返还仍在 `AfterCombatEnd`。
- `CreatureCmd.Kill` 在移除视觉节点前调用 `Hook.BeforeDeath`。对单帧静态 `Sprite2D`，演出只保留现有资源引用。Spine 等其他敌人用独立绘制的通用灵魂。
- `TheWitch.ExtraAssetPaths` 通过原版 `PreloadManager.GetRunAssetPaths` 预载素材。房间拥有演出图层，独立 Tween 驱动分镜并在房间退出时终止。镜片的轮廓和方向固定。冲击只移动舞台画面。
- V1 给本地单人启用。多人、回放和无窗口模式跳过演出，避免胜利钩子等待本地渲染。存档没有新增字段。

## 复验

在项目根目录执行以下命令。

```bash
./scripts/dev-macos.sh check
./scripts/dev-macos.sh build
./scripts/dev-macos.sh pack
./scripts/smoke-macos.sh witch-finisher
SPIRELAB_SMOKE_RESOLUTION=1600x900 SPIRELAB_SMOKE_TIMING_ONLY=1 ./scripts/smoke-macos.sh witch-finisher
```

脚本从本地 Release 包启动真实游戏的隔离副本，用单独的用户目录和 `--force-steam=off`。脚本打印日志和原生截图的输出路径。测试程序只加载在这个副本里。连续战斗中不采集截图的几场用于观察渲染帧时间。

如需亲自打一场，可打开脚本打印的 `.local/witch-finisher-smoke.*/` 副本，在其中运行 `SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2`，加 `--force-steam=off --windowed`，从主菜单选魔女并赢下一场战斗。

macOS arm64、STS2 v0.111.1 build 26655341 的窗口测试分别通过 72 项和 64 项断言。1600×900 窗口关闭截图采集时，从第一场到重复播放的完整演出耗时为 2.38–2.42 秒，平均帧时间为 16.37–16.67 毫秒，P95 为 17.41–18.75 毫秒。窗口内的动作至图层生成时间包含原版出牌或伤害结算，从 11 毫秒到约 1.33 秒，取决于触发方式。
