# 魔女视觉设定与资源

适用于 SpireLab v1.1.0。角色外观以[用户确认的原始参考](reference/user-visual-reference.png)为准，制作时统一引用[角色母版](source/character-bible.png)。参考图里的文字和卡牌数字仅用于理解视觉布局。卡名、卡牌费用、牌池、五张欲望牌、事件和战斗规则来自 [GDD V1.0](../../../SpireLab_The_Witch_GDD_v1.0.md) 及其双语实现。

## 角色设定

- **轮廓与比例**　身高视觉约 145 cm，区间为 142–150 cm。人体从头顶到脚底取约 6–6.5 个头高，帽尖、厚底靴与蓬起的裙摆单独计入剪影。三视图使用同一脚底基线。宽檐帽横向宽于肩部，镜子约与加帽后的角色等高或略高。手持书本约一头高，药瓶小于手掌宽度。
- **头发、眼睛与帽子**　黑色中长发，卷束在颊边和背部形成清楚的层次。暗红挑染只在少量边缘发束。深红瞳孔保留锐角高光。宽檐黑色弯尖帽有象牙白蕾丝内沿、黑玫瑰、象牙白蝴蝶结、银托石榴石。头部参考优先用母版底部各角度和三视图。
- **服装**　从里到外是高领白色蕾丝、黑色束腰、白褶袖口、黑外裙与分层象牙底裙。背面保留大蝴蝶结，黑色短靴保留扣带与厚底。银饰和深红细节点到为止。
- **六种表情**　母版下排依次为冷漠、妖异浅笑、半眯的病娇微笑、镜像狂笑、虚弱和施法专注。镜中人格保留同一年龄外观、五官、服饰与体型，用独立姿态、笑意和玻璃内红光区分。
- **用色**　墨黑 `#1C161D`、木炭灰 `#302832`、象牙 `#EADDE1`、灰白 `#C8B9C1`。玻璃和金属用冷银 `#CDD8E2`，危险提示用酒红 `#78233D` 与局部石榴红 `#CF486A`。
- **材质与光影**　服装用哑光、细收边与成块赛璐璐阴影，蕾丝用重复轮廓与少量高光。银饰有锐利轮廓和窄冷光，玻璃有高反差透射和薄反光。背景控制明度和细节，画框以内靠形状、眼神和动作引导视线。

## 视觉主题与缩略图

- 魔药以玻璃瓶、书页、药液与暗红环线表现储能和转化，代表作 R01「魔力之巅」。
- 镜子以冷银边、破裂玻璃和不同步的倒影表现防御和逆转，代表作 R10「镜中人」。
- 欲望以酒红、裂缝、契约和镜像表情表现诱惑与危险，代表作 R25「堕落自信」。
- GDD 中的魔法书仍以书页、封蜡与浅灰银墨图案呈现。四类图徽属于显示用途，只有原先五张欲望牌有现有玩法标签。
- 卡图只画插画，文字、费用、升级、类型和词条由原版游戏排版。徽标沿用 BaseLib 侧边槽位。普通框按稀有度使用灰银、象牙、冷银或酒红，古代框沿用原版全画幅构图。主体眼睛、手势与物件应在约 150 像素宽的普通战斗缩略图上分辨。

## 资源规格与现行路径

下表以 macOS Steam v0.111.1、BaseLib 3.4.8 的现有游戏入口为基准。运行资源根目录为 `SpireLab/SpireLab/images/witch/`，在 MegaDot 内等同 `res://SpireLab/images/witch/`。

| 资源 | 规格与透明区 | 加载路径及数量 |
| --- | --- | --- |
| 选角 | 1600 × 900 JPG，满幅，左侧为原版面板留空间 | `witch_select.jpg`，由 `select.tscn` 读取 |
| 肖像、锁定肖像 | 128 × 128 PNG | `witch_portrait*.png`，2 个 |
| 战斗、商店、休息 | 391 × 438 RGBA PNG，透明轮廓，脚底对齐 | `witch_sprite.png`，由三个场景共享 |
| 镜中人 | 238 × 438 RGBA PNG，透明镜框外围，脚底与立绘一致 | `witch_mirror_form.png`，战斗场景独立层，触发后增强 |
| 卡牌 | 常规 500 × 380 PNG，古代 606 × 852 PNG | `cards/{GDD ID 小写}.png`，89 + 2，路径由 `WitchCard` 提供 |
| 遗物 | 94 × 94 PNG 本体与轮廓，256 × 256 PNG 大图，均有透明区 | `relics/` 与 `relics/big/`，9 × 3 |
| 药水 | 256 × 256 PNG 本体与轮廓，均有透明区 | `potions/`，3 × 2 |
| 图徽、能力 | 64 × 64 SVG，透明轮廓 | `suits/` 4 个，`abilities/` 5 个 |
| 地图、头像标识、能量 | 128 × 128 SVG，费用标 96 × 96 | `witch_icon.svg`、`witch_meter.svg`、`witch_energy.svg` |
| 卡框、特效 | 原版卡框尺寸与透明轮廓，自定义材质和 Godot 动画 | `frames/`、`scenes/witch/` 与 `WitchFeedback` |

普通及古代卡图尺寸参考 [Alchyr 角色模板](https://github.com/Alchyr/ModTemplate-StS2/blob/master/content/CharacterModTemplate/CharModCode/Cards/CharModCard.cs)。框、纹理和图徽接口对照 [BaseLib 3.4.8 源码](https://github.com/Alchyr/BaseLib-StS2/tree/v3.4.8)。引擎根据项目 `BasicExport` 导入 PNG、JPG 与 SVG，PCK 包含实际导出纹理、场景和本地化。[Godot 4.5 图片导入文档](https://docs.godotengine.org/en/4.5/tutorials/assets_pipeline/importing_images.html)

## 来源、导出与实机

- `reference/` 保存本次用户提供的视觉参考，`source/` 保存图像工具生成的原图。提示词按同名主文件及 `prompts/cards/` 归档。样例、立绘和原画图集共用同一母版，所有原画均为本任务新制作。音色沿用现有原创合成 WAV。
- 选角截屏后将画面拉远，避免原版背景缩放裁掉巫师帽。运行时使用 `character-select-reframed.png` 导出，原构图保留用于 128 像素肖像。物品图集也经透明边距校正，最终 PNG 图标来自 `source/items.png`。
- `card-source-map.tsv` 将普通卡映射到九宫格图集的零基位置。R01、R10、R25 是单独生成的横向样例，A01、A02 分别来自古代竖画源图的左、右两半。运行 `bash docs/art/witch-v2/export-assets.sh` 复现导出。脚本只做裁切、缩放、透明边距、物品描边和锁定头像处理。
- 以 `./scripts/smoke-macos.sh witch-art` 在隔离的 macOS 游戏副本生成选角、真实手牌和镜中形态截图。对战使用原有卡牌模型和输入路径，选角和手牌的文字来自原版 UI。审核结果见[验证记录](../../witch-validation.md)。
- 实机证据保存在 [选角](screenshots/character-select-macos.jpg)、[手牌](screenshots/card-hand-macos.jpg) 与 [镜中形态](screenshots/mirror-form-macos.jpg)。工作区使用这三张压缩后的截图作为工坊预览图。
