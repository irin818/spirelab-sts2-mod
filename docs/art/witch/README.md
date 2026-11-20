# 魔女美术基准

使用内置 ImageGen 创作，源图位于本目录。先锁定原创人物与镜像，再参考同一设定图制作战斗立绘、选角背景和四类卡面锚点。未引用或复刻现有动漫的角色、服装、镜头和图像素材。

- 黑曜 `#17131B` 和暗红 `#7F182B` 主导服饰，`#CF3044` 留给轮廓与欲望反馈。短中发的深红色发束和高领不对称宫廷裙是剪影锚点。
- 镜框使用冰银 `#B8C2CE`。镜像和本体保持相同发型、领口和首饰，用侧身、手势与笑容区分人格。
- 魔药用暗紫 `#53406E` 和暗金 `#C19A60`。书页用深金和羊皮纸，镜子用冷银和锐利碎玻璃，欲望使用局部高饱和猩红与不对称构图。
- 卡图导出为 500×380 PNG。文字、边框和卡牌费用由游戏绘制。古代牌单独使用符合其画框的画图尺寸。
- 原型共用的四张风格锚点保留为参考，V1.0 已按规格 ID 接入独立卡面。自绘镜面标识、红黑能量表和真实／镜像生命条使用 Godot 原生 UI。
- 待机、施法、攻击、受击和死亡使用 MegaDot 动画。低于一半真实生命时，角色立绘叠加独立的猩红色呼吸动画。格挡使用银镜闪光，启迪、逆转、反射、欲望与额外能量有五个原创音色和对应脉冲。四张原创能力图标随脉冲淡出，额外能量使用红晶能量图标。精细骨骼与长演出留给后续美术精修。
- [创意工坊封面](workshop-cover.jpg) 采用原创选角背景与双语标题，从仓库根目录运行 `swift docs/art/witch/export-workshop-cover.swift` 可重新导出。

## 提示词

### 角色三视图与镜像

> Use case: stylized-concept. Original animation model sheet for an adult female gothic witch game character, approved visual brief. One polished landscape reference sheet: consistent full-body front, profile and back orthographic on the left, right side two closeups of the same narrow-eyed face with different expressions (aloof controlled vs mischievous dangerous mirror self), and her antique broken full-length standing mirror with ornate silver-black frame and spiderweb cracks showing the mischievous reflection in an independent gesture. Black chin-to-shoulder hair with a single muted crimson temple streak, fitted black and burgundy court-gothic dress with high collar, bell cuffs and asymmetric layered hem, tall boots, dignified silhouette, a small deep-gold grimoire and dark-violet potion. Distinct mature original Japanese 2D anime dark fantasy, angular clean linework, hard-edge painted cel shadows, sharp crimson rim light, muted ink environment, polished cinematic materials and expressions. Neutral slate background, all limbs visible, palette swatches charcoal #17131B burgundy #7F182B crimson #CF3044 silver #B8C2CE gold #C19A60 violet #53406E. No text, no logos, no existing characters, costume or scenes.

### 战斗立绘

> Create a transparent game battle idle sprite from the attached ORIGINAL character reference (identity and outfit anchor). Full-body witch in three-quarter view facing right, feet stable at bottom, complete asymmetric charcoal and burgundy gothic dress and short black hair with a crimson streak. Antique full-length silver-black cracked standing mirror next to and slightly behind her with the SAME witch in the mirror subtly smirking and making a different hand gesture. A compact readable shape for a side-view card-battle game; strong hard-edged 2D anime cel shading, thin crimson rim, gunmetal glass. Keep everything within frame. Do not draw ground, background, panels, any letters or shadows outside the subjects. Genuine transparency outside witch and mirror.

### 卡面风格锚点

> Create four distinct rectangular game-card illustration anchors in a single precise equal 2x2 atlas without gutters, margins, overprinted typography or overlapping tiles. Top-left: a purple court-alchemist phial breaks across bronze-dark violet flames, center. Top-right: high-contrast ice-silver shards and a small silver cracked standing mirror showing sinister eye, center. Bottom-left: antique gold grimoire on black shadow, floating sweeping nonsensical golden sigils, center. Bottom-right: mature original witch from reference and her more dangerous reflection in a crimson red eye close-up, center. Use cohesive original Japanese 2D cel-anime fantasy, hard-edge shadow, midnight plum and restrained saturated red rim, readable focal points. These are card illustrations only, no text, logos, borders or cropped quadrant encroachment.

### 选角背景

 > Original character selection background, widescreen 16:9, cinematic 2D Japanese cel anime. Use attached original witch model sheet as a strict identity and costume reference. At the right side in the middle distance, cool aloof adult witch in charcoal/burgundy court gothic dress stands beside her fractured silver-black floor mirror, which reveals her same face in a distinctly amused independent pose. No figure on left half; textured dark graphite wall and deep ink shadows with generous negative space for opaque white game menu text. A smoky abandoned vaulted obsidian hall, angled crimson window light, one thin desaturated silver reflection, controlled hard-edge shadows. Entire mirror and both heads clearly within the central safe image area. No words, UI, logo, watermarks or existing series imagery.

## 批量资产

直接按规格书 ID 命名，`B01` 到 `R27` 的图集按行从左到右裁切。古代两张使用独立竖构图。六张卡面源图、古代源图与物品透明源图附在本目录。导出使用 `bash docs/art/witch/export-portraits.sh`，保持普通卡 500×380 和古代卡 606×852。物品图标从透明图集导出，描边复用其 alpha，五个原创短音色可通过本目录的合成脚本复现。

### 遗物和药水

> Create exactly 4 columns x 3 rows (12 individual symbols) of original stylized RPG inventory item icons, on genuinely transparent canvas. Equal square cells, well-separated edges, each small object centered with empty padding; no words, frames, backgrounds or grid lines. Use the supplied original Witch reference for her gothic silver filigree, black-red mood, purple elixirs and painted anime cel shading. Exact row-major order: Row 1 cracked silver hand vanity mirror, pair of ornate full-length silver standing mirrors, purple flask with a crack, a detached silver filigree mirror frame. Row 2 faded torn antique recipe parchment, black obsidian distillation condenser with brass tube, black and burgundy double-laced veil, old brass ring with a red gemstone. Row 3 tightly wax-sealed squat bottle, silver-grey cloudy mist potion, two luminous violet twins in a single split bottle, black distillate bottle with a crimson droplet ornament. Every object is legible in silhouette, matches dark-fantasy 2D mature anime, and is completely isolated from all other objects by wide transparent margins. Preserve true alpha.

### 古代卡

> Two distinct full-height portrait-oriented ancient game-card illustrations as a two-column diptych. Exact divider at 50% width, no gutter, no logo, no text, no UI. Each column a separate 5:7 portrait panel with the important content entirely inside that panel and extra safe space at top and bottom. LEFT: Beyond the Glass, original reference witch with aloof black hair and red streak looks back from a cracked baroque silver mirror doorway, dizzying cold silver and muted wine-red highlights. RIGHT: Sevenfold Distillation, seven successive dark violet-to-gold glass vessels in a vertical Gothic lab, with vapor organizing into angular golden alchemy sigils. High-end stylized mature Japanese 2D anime cel shadows and cinematic chiaroscuro, original game art aligned to the witch model sheet. Render both distinctly as two finished scenes.

### 卡面图集 1

> Production sprite sheet for original Witch game card art. Use the supplied ORIGINAL character model sheet for identity and the supplied four style-anchor paintings for cel-animation colors and style. Exactly 4 columns and 4 rows of equally sized illustrations, edge to edge, without any gutters, superimposed grid line, frames, text, card UI or logos. Clear cut at each quarter of canvas. Do not span, blend or merge any two cells. Place each cell's single focal subject near its own center with generous safe margins. Original polished dark-fantasy Japanese cel-anime illustrations, sharp ink and shadows, low-saturation charcoal and local red highlights. Characters must match supplied witch identity and costume. The subjects in exact left-to-right row-major order, four per row:
> B01 slash of black witch's dagger | B02 forearms crossed beneath translucent guard | B03 a raised thin silver mirror | B04 setting a purple starter flask alight
> C01 hand hurling a glowing potion | C02 purplish potion vapor forming a shield | C03 closeup of a gold distillation tube | C04 burning bottle bursts
> C05 last drops spill from a black vial | C06 concentrated purple fire compressed between hands | C07 jets of corrosive steam from flask | C08 dark gold sediment settles
> C09 isolated flame hanging above a burned bottle | C10 flying silver mirror splinter | C11 arcing silver protective screen | C12 diagonal slice through mirror etching
> IDs are instructions only, never paint them as lettering.

### 卡面图集 2

> Production sprite sheet for original Witch game card art. Use the supplied ORIGINAL character model sheet for identity and the supplied four style-anchor paintings for cel-animation colors and style. Exactly 4 columns and 4 rows of equally sized illustrations, edge to edge, without any gutters, superimposed grid line, frames, text, card UI or logos. Clear cut at each quarter of canvas. Do not span, blend or merge any two cells. Place each cell's single focal subject near its own center with generous safe margins. Original polished dark-fantasy Japanese cel-anime illustrations, sharp ink and shadows, low-saturation charcoal and local red highlights. Characters must match supplied witch identity and costume. The subjects in exact left-to-right row-major order, four per row:
> C13 cracked face glimpsed in glass | C14 giant ice-silver barrier | C15 shield reflected into next pane | C16 sword separates a shadow from the witch
> C17 moonlight caught on a fractured silver surface | C18 glass refracts a gold-red sword beam | C19 sharp gold grimoire pages fly | C20 raised sheer black veil
> U01 open hand reaching for an overflowing purple potion | U02 an amber healing draught held to lips | U03 gold seal on purple flask | U04 taboo lab circle converts a bottle to gold sparks
> U05 alchemist strainer drip closeup | U06 overloaded violet boiling flask | U07 small radiant gold elixir phial | U08 gothic brass alchemy pump
> IDs are instructions only, never paint them as lettering.

### 卡面图集 3

> Production sprite sheet for original Witch game card art. Use the supplied ORIGINAL character model sheet for identity and the supplied four style-anchor paintings for cel-animation colors and style. Exactly 4 columns and 4 rows of equally sized illustrations, edge to edge, without any gutters, superimposed grid line, frames, text, card UI or logos. Clear cut at each quarter of canvas. Do not span, blend or merge any two cells. Place each cell's single focal subject near its own center with generous safe margins. Original polished dark-fantasy Japanese cel-anime illustrations, sharp ink and shadows, low-saturation charcoal and local red highlights. Characters must match supplied witch identity and costume. The subjects in exact left-to-right row-major order, four per row:
> U09 violet lightning in a bottle | U10 two boiling flame echoes clash | U11 last three embers shield the witch | U12 air suction through an uncorked bottle
> U13 strange dark roots and alchemical extract | U14 eerie blue flame in a brass vessel | U15 flask fluid climbs upward against gravity | U16 silver-edged sword of mirror glass
> U17 ghostly double behind an empty mirror | U18 maze of overlapping room mirrors | U19 reflective sphere bouncing a silver ray | U20 silver mirror fractures outward
> U21 etched glinting mirror runic sigil | U22 gothic hall of mirrors | U23 fragile mirror bead cracks between fingers | U24 double-sided baroque mirror
> IDs are instructions only, never paint them as lettering.

### 卡面图集 4

> Production sprite sheet for original Witch game card art. Use the supplied ORIGINAL character model sheet for identity and the supplied four style-anchor paintings for cel-animation colors and style. Exactly 4 columns and 4 rows of equally sized illustrations, edge to edge, without any gutters, superimposed grid line, frames, text, card UI or logos. Clear cut at each quarter of canvas. Do not span, blend or merge any two cells. Place each cell's single focal subject near its own center with generous safe margins. Original polished dark-fantasy Japanese cel-anime illustrations, sharp ink and shadows, low-saturation charcoal and local red highlights. Characters must match supplied witch identity and costume. The subjects in exact left-to-right row-major order, four per row:
> U25 backlit rear silhouette of the witch | U26 enemy shadow trapped behind mirrored bars | U27 dark witch turning toward her reflection | U28 ephemeral glass silhouette slips past a blade
> U29 faceted silver prism bends red light | U30 two grimoire pages switch orientation | U31 hand lifting the ominous title page of a dark book | U32 golden magic glyph blade
> U33 witch with pressed finger to lips and gold seal | U34 moonlit bound accounting ledger with arcane marks | U35 grim black hood ward edged gold | U36 blank parchment torn from chained cover
> U37 grasping reflected hands hold a pile of black cards | U38 recumbent mirror self with narrowed eyes | R01 ascending pillar of purple mana lit gold | R02 eruption of radiant golden mana through a flask
> IDs are instructions only, never paint them as lettering.

### 卡面图集 5

> Production sprite sheet for original Witch game card art. Use the supplied ORIGINAL character model sheet for identity and the supplied four style-anchor paintings for cel-animation colors and style. Exactly 4 columns and 4 rows of equally sized illustrations, edge to edge, without any gutters, superimposed grid line, frames, text, card UI or logos. Clear cut at each quarter of canvas. Do not span, blend or merge any two cells. Place each cell's single focal subject near its own center with generous safe margins. Original polished dark-fantasy Japanese cel-anime illustrations, sharp ink and shadows, low-saturation charcoal and local red highlights. Characters must match supplied witch identity and costume. The subjects in exact left-to-right row-major order, four per row:
> R03 distilled floating gold mana droplet | R04 crimson-red catalyst in a lab crucible | R05 flood of violet potions and vapors | R06 obsidian abyss spiraling into a flask
> R07 dark gold pact inscribed with a red droplet | R08 last distillation releases brilliant purple flame | R09 amber dusk alchemical infusion resting on a sill | R10 same witch and independent sinister floor-mirror double
> R11 infinite multitude of cold silver mirrors | R12 pale moon reflected in cracked glass and water | R13 shower of myriad mirror shards | R14 witch's shadow has mirror self's profile
> R15 empty standing silver mirror against deep ink shadow | R16 a cage of razor-edged silver mirrors | R17 silver fragments bury a shadow silhouette | R18 broken crown of cold silver
> IDs are instructions only, never paint them as lettering.

### 卡面图集 6

> Production sprite sheet for original Witch game card art. Use the supplied ORIGINAL character model sheet for identity and the supplied four style-anchor paintings for cel-animation colors and style. Exactly 4 columns and 4 rows of equally sized illustrations, edge to edge, without any gutters, superimposed grid line, frames, text, card UI or logos. Clear cut at each quarter of canvas. Do not span, blend or merge any two cells. Place each cell's single focal subject near its own center with generous safe margins. Original polished dark-fantasy Japanese cel-anime illustrations, sharp ink and shadows, low-saturation charcoal and local red highlights. Characters must match supplied witch identity and costume. The subjects in exact left-to-right row-major order, four per row:
> R19 final chapter opened in a gilded forbidden grimoire | R20 witch turning two floating pages of fate | R21 detailed red-stained velvet bookmark in a grimoire | R22 an antique hourglass sealed by golden runes
> R23 mirrored clockwork rite performed backward | R24 an echo appears over a sealed contract | R25 reflection raises chin with confident crimson gaze | R26 crimson crack erupts from a black mirror
> R27 sinister reflected witch flips a red die over a blood-red abyss | A01 ancient witch mirror glancing back from a silver doorway | A02 seven gold and purple glass alchemical vessels in receding layers | black silver cracked small vanity mirror icon
> twin freestanding silver mirror icon | cracked purple phial icon | obsidian distiller icon | bloodstained gold ring icon
> IDs are instructions only, never paint them as lettering.
