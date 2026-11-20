# 魔女 V1.6 集成与私密测试

活动设计以 [V1.6 GDD](../SpireLab_The_Witch_GDD_v1.6.md) 为准。正式安装基线是 Steam STS2 v0.111.1，build 26655341。测试工具为 Apple Silicon macOS、.NET SDK 10.0.404、net9.0、MegaDot 4.5.1-m.14、BaseLib v3.4.8 和 ILSpy 11.2.0.10050。

## 美术验收

逐项解码并检查外部包 139 个 PNG 的尺寸、透明范围和 SHA-256，再对比游戏素材与缩略图。外部名称通过稳定卡牌 ID、实际注册的对象和现用资源路径核对。[完整逐项审查](art/witch-v1.6-source-audit.csv) · [工程映射](../artwork/manifest.csv)

| 范围 | A 可进入本次测试 | B 可作临时备选 | C 保留现版或无可用槽位 |
| --- | ---: | ---: | ---: |
| 91 张卡牌 | 1 | 8 | 82 |
| 48 个其他源文件 | 8 | 31 | 9 |
| 总计 | 9 | 39 | 91 |

- 导入 U31「禁书扉页」和 8 个能力图标，生成正式测试用的 17 张运行纹理。90 张卡图沿用上一版。待精修列表可在逐项审查中按 `category=card`、`decision=keep-current` 筛选。A 级只代表达到本次私密测试门槛。
- 保留现有透明战斗角色、镜像、遗物、药水、体系徽记和特效。新人物抠图仍有残字和边缘问题，一些新遗物及药水仅通过颜色区分。无常驻槽位的镜像生命图标与生命支付图标留作候选，实际反馈继续由血条和战斗演出承担。
- 原始包及旧运行图保存在项目外快照。新增源图约 1.6 MB，导出纹理约 0.5 MB。仓库记录选中的原图和运行纹理，原始总览包与 QA 截图留在本地。

## 机制与状态

91 种卡牌、9 件遗物及 3 种药水继续通过原版模型注册。开场临时牌、真实交换、半血保留、生命逆转、反射与五张欲望牌沿用 V1.6 规则。新增的刷新点紧随能量及格挡结算和起手抽牌，确保选择换牌时角标已经反映已消耗的延迟效果。原版存档在进入战斗时重建战斗状态，临时牌和进度角标不会写入永久牌组。

| 截图 | 触发与观察 |
| --- | --- |
| `status-before-macos.png` | 命运选牌与换牌后能看到魔法启迪，U31 使用新卡图，其他主题通过原版卡框与徽记显示。 |
| `status-active-macos.png` | 打出魔力之巅、镜中人、堕落自信、禁书扉页、染血书签、镜厅、凝炼深渊、魔力泵与反光刻印。血条区分真实和镜像生命。攻击免疫、待反射与延迟能量、格挡、抽牌显示实际值。 |
| `status-after-hit-macos.png` | 首次攻击消耗免疫，未消耗格挡。下次被格挡的攻击消耗反射并排定一次刻印。 |
| `status-next-turn-macos.png` | 新回合消耗延迟量，魔力泵 3 降至 2。第一张技能消耗刻印，持续状态仍在。 |
| `power-tooltip-macos.png` | 验证原版角标的悬停窗口与简中说明。能力集中触发的截图保留原版角色悬停多列提示。 |

怠惰本身的首牌免疫与半血保留由规则实机回归覆盖。截图场景用镜花水月使共享免疫角标保持在画面中，直至脚本触发敌方攻击。个别未明确的规则继续按既有边界实现，例如镜纹斩确认后最多消耗 4 点当前格挡。

## 本地测试结果

| 检查 | 结果 |
| --- | --- |
| `check`、`build`、`pack` | 工具链和游戏引用验证通过，编译 0 错误、0 警告。 |
| `art-sync --dry-run`、`art-sync` | 197 项映射，首次导入 17 张运行纹理。重复运行 0 张变化。不透明能力样本被拒绝，PCK 和旧能力图哈希保持相同。 |
| `dotnet run --project tests/SpireLab.Witch.Rules` | 通过，包含 500 条狂赌真实生命递减路径。 |
| `witch` | 116 项通过，覆盖 91 张注册、费用、升级和双语描述，四场战斗及商店、事件。 |
| `witch-save`、`witch-legacy-save` | 新档跨进程和 V1.0 旧档恢复通过。 |
| `witch-art` | 33 项原生界面检查通过。包括原版命运选择、战斗手牌、角标状态、真实生命与镜像生命显示及悬停。截图目录为 `.local/witch-art-smoke.d28H8V/captures/`。 |
| `witch-campaign` | 使用原版辅助测试模式，种子 `WITCH10051` 完成三幕首领并胜利返回主菜单。较早的 `WITCH10058` 尝试在第三幕正常战败，自动玩家随后等不到奖励并报告超时。 |
| Steam 安装的 BaseLib | 用 Steam 提供的 BaseLib v3.4.8 复跑 116 项通过。归档旧包后重建，新的 DLL、PCK 和清单与候选包一致，重入隔离版的 34 项界面测试通过，包括 64 和 256 两种能力图尺寸。截图为 `.local/witch-art-smoke.AQP87D/captures/`。 |

上述完整流程为原版 AutoSlay 有辅助测试，用于检查长期加载、交互和状态生命周期。手工平衡、Windows 及联网兼容性仍需验证。

## 复现与回滚

1. 运行 `./scripts/dev-macos.sh check`、`art-sync --dry-run`、`art-sync`、`build` 与 `pack`。源图缺省时沿用运行图。图像校验或打包失败时回退本批运行纹理和包。
2. 运行 `./scripts/smoke-macos.sh witch-art`、`witch`、`witch-save`、`witch-legacy-save` 和 `witch-campaign`，需要可视渲染时设置 `SPIRELAB_SMOKE_WINDOWED=1`。用 `SPIRELAB_SMOKE_MOD_SOURCE` 和 `SPIRELAB_SMOKE_BASELIB_SOURCE` 可指定已经构建或由 Steam 安装的载荷。测试副本独立存档，启动时禁用 Steam。
3. 本地回滚引用为 `backup/witch-v1.6-before-artwork`，指向 `d0ebda6`。可在独立 worktree 还原开发基线。要回退安装文件，使用项目外快照中的旧版安装包。工坊回退只能对原项目在已验证 Private 的前提下进行。
4. V1.0 规格和既有角色源图按历史保留。已校验的输出 ZIP 和截图保存在本地，任何 Steam 可见性更改须以作者会话中的服务器查询为依据。[现有条目更新规则](workshop/README.md)

本轮移走三份旧发布工作区和一份可重建 DLL/PCK 导出目录，均为 Git 忽略项目。原始图、Steam 下载的旧三件文件、可见性基线、关联 ID 和 Git bundle 均保存在项目外快照。新的本地工作区为 `.local/release/Witch-v1.6.0.C9NIhD`，`.local/release/latest` 指向该目录。

## Steam 私密更新

- 目标为原有 PublishedFileId `3842829456`、App ID `2868840`、Mod ID `SpireLab`。作者会话最初查到 Public，单独提交可见性后重新查到 Private。该次变更前后的内容文件句柄和字节数相同。
- 确认 Private 后，通过 Steamworks 只提交 `SpireLab.dll`、`SpireLab.pck` 和 `SpireLab.json`。提交返回 OK。2026-11-20 23:40:46（上海时间）的服务器版本仍为 Private。标题、描述哈希、主预览句柄和 BaseLib 依赖 `3737335127` 都通过前后比对。切换为 Private 后，上传前后的无身份请求均无法取得条目数据。
- 作者会话报告条目已订阅且安装完成。Steam 下载的三个文件与已验收工作区逐字节相同，清单版本为 v1.6.0。实际游戏的本地 mods 目录没有额外 SpireLab 副本，Steam 活动路径只有原条目的安装目录。
- 本地证明文件位于 `.local/logs/v16-steam-visibility-only.log`、`v16-steam-private-upload.log`、`v16-steam-private-download.log` 和 `v16-steam-installed-sha256.txt`。两次下载版实机的断言通过，Godot 预载时的 `Invalid Task ID` 使严格日志检查失败。测试入口等待全量菜单资源，并用同一组 Steam 下载文件重跑。最终 `witch-art` 以退出码 0 通过全部 34 项，截图为 `.local/witch-art-smoke.cIB0FO/captures/`。

公开发布由用户另行授权。新外部素材的公开分发许可、Windows、联网与逐卡手测仍需确认。
