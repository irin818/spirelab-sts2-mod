# SpireLab — 魔女 / The Witch

## v1.6.2 · 2026-11-22

- 命运选牌支持费用、体系和类型组合筛选，点击新牌可直接替换选择。卡牌描述使用独立的小号能量图标。
- 镜子反射按每段实际格挡量结算。原初贪婪打出后进入弃牌堆，怠惰免疫在下回合开始时结束，狂赌之渊在同回合获胜时返还本牌支付的真实生命。
- 工坊条目 `3842829456` 经作者会话确认 Private 后，从 `f0d6c20` 更新。只提交 DLL、PCK 和清单，标题、描述、标签、封面及 BaseLib 依赖保持原值。Steam 订阅并安装的三件文件与上传包逐字节一致。[Steamworks 内容更新接口](https://partner.steamgames.com/doc/api/ISteamUGC#SetItemContent)
- 使用 Steam 实际下载的魔女与 BaseLib 3.4.8，在隔离的 macOS 游戏副本中通过 116 项战斗、40 项界面和 9 项跨进程存档检查。游戏版本为 `v0.111.1`（build `26655341`）。界面首轮遇到 `Invalid Task ID` 预载诊断，复跑通过。

V1.6.2 updates Fate Selection, per-hit reflection, and the Desire cards Primal Greed, Sloth, and Abyss of Gambling.

## v1.6.0

- 魔法启迪可在每场战斗开场选择一张临时奖励牌，每回合额外抽一张并可交换实际牌堆实例。
- 更新魔药、镜子、魔法书及五张欲望牌。原初贪婪与怠惰追加低真实生命时的手牌选择。
- 加入原生战斗角标、双语悬停说明和可替换的外部美术资源映射。
- 保留角色、卡牌、遗物与药水 ID。验收基线为 Apple Silicon、STS2 v0.111.1 和 BaseLib 3.4.8。

V1.6 adds a combat-only Fate Selection, real pile exchange, revised cards, and combat state icons. See the V1.6 validation notes for what was tested.
