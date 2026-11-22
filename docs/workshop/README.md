# 魔女创意工坊发布准备

这个工作区供确认后使用。包含上传所需的 `workshop.json`、`content/`、`image.png` 和 macOS 实机截图。包内声明 BaseLib 的工坊依赖，手动安装 ZIP 则附带验收用的锁定版本与许可证。

## 正式包校验

从仓库根目录运行 `./scripts/dev-macos.sh pack` 生成带正式标记的 `ExportRelease` 包，再用该包跑隔离实机检查。将这次实机的 `mods` 目录和截图目录传给 `./scripts/stage-witch-release.sh`。它会逐文件核对实测产物，拒绝非正式包。工坊 `content/` 只允许 `SpireLab.dll`、`SpireLab.pck` 和 `SpireLab.json`，三件文件须与本地正式构建完全一致。

实际上传前须复跑校验，直接上传已有工作区和仅提交 `SetItemContent` 的路径都用同一条命令。校验失败时停止上传。

```sh
python3 ./scripts/verify-witch-release.py workshop "/absolute/path/to/SteamWorkshop"
```

## 更新现有私密测试条目

核对 `mod_id.txt` 与 Steam 作者账户中的现有 PublishedFileId。先在服务器单独将现有条目切换为 **Private**，再用作者会话重新查询。只有查询明确返回 Private 才可上传内容。`workshop.json` 中的 `visibility` 是本地配置，无法证明 Steam 可见性。

`scripts/stage-witch-release.sh` 可接收第三个目录参数，复用已核验的 `mod_id.txt`、`image.png` 与私密配置。应使用实际线上封面并校验 PNG 小于 1 MB。工作区 `previews/` 中的截图供验收。

Mega Crit 0.2.0 上传器会同时写入游戏分支限制、主预览并同步附图和依赖。只更新现有项目的游戏文件时，用 Steamworks 的 `SetItemContent` 保留这些设置，独立确认 Private 后执行 `SubmitItemUpdate`，随后核对作者会话中的可见性和 Steam 实际下载的文件。仅在完整工作区的封面、附图、依赖和分支范围均经核对时使用官方上传器更新现有项目。[Mega Crit 上传实现](https://github.com/megacrit/sts2-mod-uploader/blob/v0.2.0/src/UploadCommand.cs) · [Steamworks Workshop 接口](https://partner.steamgames.com/doc/api/ISteamUGC)

## 经授权创建新条目

1. 在 Steam 客户端登录拥有游戏的账号。确认 Steam Workshop 条款和选定的可见性。
2. 使用 [Mega Crit 官方 Mod Uploader](https://github.com/megacrit/sts2-mod-uploader/releases/tag/v0.2.0) 的 macOS arm64 构建，进入解压后的上传工具目录。
3. 从仓库根目录将命令中的路径替换成官方工具和经过本机打包核验的 `SteamWorkshop` 目录。模板为 `private`。官方工具给首次上传分配项目 ID，后续更新保留工具写出的 `mod_id.txt`。

```sh
workshop="/absolute/path/to/SteamWorkshop"
python3 ./scripts/verify-witch-release.py workshop "$workshop" &&
    "/absolute/path/to/ModUploader" upload -w "$workshop"
```

工坊依赖为 [BaseLib #3737335127](https://steamcommunity.com/sharedfiles/filedetails/?id=3737335127)。正式发布前可通过 Steam 客户端确认依赖可订阅，并用无本地重复安装的环境订阅一次。上游格式、预览图和可见性以 [Mega Crit 工作区文档](https://github.com/megacrit/sts2-mod-uploader/blob/v0.2.0/template/README.md) 为准。

## 宣传素材导出与平台限制（血色终镜 V4）

- **官方上传器限制**：本机校验 Mega Crit `ModUploader` v0.2.0 二进制与 `template/README.md`，主封面文件名固定要求为 `image.png` 且严格小于 1 MB；`previews/` 同步附图同样小于 1 MB；不支持直接上传 `.mp4` 作为主封面或预览文件。
- **Steamworks 扩展能力**：`ISteamUGC::SetItemPreview` 底层支持小于 1 MB 的 JPG / PNG / GIF，但官方 `ModUploader` 仅读取 `image.png`；若未来再次执行 `ModUploader upload -w`，非 `image.png` 的主封面会被覆盖。视频预览须先上传至 YouTube，再通过创意工坊网页端或 `ISteamUGC::AddItemPreviewVideo` 绑定，禁止将 MP4 冒充主封面。
- **推荐策略**：默认以 800×800 真彩 PNG（`< 1 MB`）作为主封面以保证最佳画质与 `ModUploader` 全兼容；1920×1080 10 秒宣传片 `trailer.mp4` + `video-cover.png` 用于视频展示位；1:1 动态 `cover.gif`（540×540、12 fps、`< 1 MB`）作为可选动态备选。
- **可重现导出与发布前校验**：

```sh
python3 ./scripts/export-witch-workshop-media.py --out .local/witch-workshop-media/deliverables/final --overwrite
python3 ./scripts/verify-witch-release.py media .local/witch-workshop-media/deliverables/final
```
