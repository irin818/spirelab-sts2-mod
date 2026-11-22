# 血色终镜 V4

V4 复用用户提供的 1672 × 941 魔女原画抠图、眼眉口表情和四段袖手。处刑镜沿用前版，倒悬镜庭和四组透明荆棘素材为本次制作。新素材保留独立 WebP，纹理使用 mipmap 做局部变焦。人物抠图和切片来源见 [V3 素材记录](crimson-final-mirror-v3.md)。

| 演出时间 | 焦点与动作 |
| --- | --- |
| 0.00–0.42 秒 | 推向最后被击败敌人的原生命中框 |
| 0.42–1.05 秒 | 拉开战斗镜头，镜幕沿中央血线向外展开 |
| 1.05–1.55 秒 | 沿处刑镜上缘转向镜后魔女 |
| 1.55–2.15 秒 | 抬眸、偏头、露出笑意，外围荆棘放慢，侧镜延迟显影 |
| 2.15–2.72 秒 | 转焦掌心和灵魂，头发与衣料先消散，脸部最后退场 |
| 2.72–2.95 秒 | 前景握拳与爆裂音效同步，银红碎片散开 |
| 2.95–3.55 秒 | 渐退并恢复原生战斗画面，继续胜利奖励 |

场景使用一条 Tween 驱动各层，战斗背景、镜像、人物、处刑镜和前景手部各有独立深度。袖手 Shader 仅柔化袖口接边。渲染期间保留原版 HUD 排版。音效包含玻璃谐波、压缩低频及末尾碎片尾音，爆裂一刻沿用现有镜碎采样。

## 本地操作

Finder 双击仓库根目录的 `启动魔女本地测试.command`。脚本检查工具链，将本地构建和 BaseLib 装入同一个隔离副本，然后打开可交互的主菜单。测试存档由该副本自己的 Godot 用户目录保存。日志见 `.local/witch-local-play/logs/`。

可重复的实机检查从仓库根目录运行：

```sh
./scripts/dev-macos.sh pack
export SPIRELAB_SMOKE_MOD_SOURCE="$PWD/SpireLab/bin/mods/ExportRelease/SpireLab"
./scripts/smoke-macos.sh witch-greed
./scripts/smoke-macos.sh witch-finisher
SPIRELAB_SMOKE_TIMING_ONLY=1 SPIRELAB_SMOKE_RESOLUTION=1600x900 ./scripts/smoke-macos.sh witch-finisher
```

`witch-greed` 通过原版手动出牌队列复现自身重新抽回和留在抽牌堆，日志含费用、牌堆实体一致性和截图。`witch-finisher` 覆盖致命攻击、反伤、群攻、多段、首领、敌方回合、强制中断及奖励，包含逐帧时间和渲染器内存采样。测试 Mod 位于 `tests/`，正式包发布校验只接受魔女的 DLL、PCK 和 manifest。

血色终镜仅在单人常规遭遇中播放。嵌入事件的战斗保留事件自己的退场与奖励时序。
