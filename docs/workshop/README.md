# 魔女 v1.6.0 创意工坊发布准备

这个工作区供确认后使用。包含上传所需的 `workshop.json`、`content/`、`image.png` 和 macOS 实机截图。包内声明 BaseLib 的工坊依赖，手动安装 ZIP 则附带验收用的锁定版本与许可证。

## 更新现有私密测试条目

核对 `mod_id.txt` 与 Steam 作者账户中的现有 PublishedFileId。先在服务器单独将现有条目切换为 **Private**，再用作者会话重新查询。只有查询明确返回 Private 才可上传内容。`workshop.json` 中的 `visibility` 是本地配置，无法证明 Steam 可见性。

`scripts/stage-witch-release.sh` 可接收第三个目录参数，复用已核验的 `mod_id.txt`、`image.png` 与私密配置。应使用实际线上封面并校验 PNG 小于 1 MB。工作区 `previews/` 中的截图供验收。

Mega Crit 0.2.0 上传器会同时写入游戏分支限制、主预览并同步附图和依赖。只更新现有项目的游戏文件时，用 Steamworks 的 `SetItemContent` 保留这些设置，独立确认 Private 后执行 `SubmitItemUpdate`，随后核对作者会话中的可见性和 Steam 实际下载的文件。仅在完整工作区的封面、附图、依赖和分支范围均经核对时使用官方上传器更新现有项目。[Mega Crit 上传实现](https://github.com/megacrit/sts2-mod-uploader/blob/v0.2.0/src/UploadCommand.cs) · [Steamworks Workshop 接口](https://partner.steamgames.com/doc/api/ISteamUGC)

## 经授权创建新条目

1. 在 Steam 客户端登录拥有游戏的账号。确认 Steam Workshop 条款和选定的可见性。
2. 使用 [Mega Crit 官方 Mod Uploader](https://github.com/megacrit/sts2-mod-uploader/releases/tag/v0.2.0) 的 macOS arm64 构建，进入解压后的上传工具目录。
3. 将命令中的路径替换成解压出的 `SteamWorkshop` 目录。模板为 `private`。官方工具给首次上传分配项目 ID，后续更新保留工具写出的 `mod_id.txt`。

```sh
./ModUploader upload -w "/absolute/path/to/SteamWorkshop"
```

工坊依赖为 [BaseLib #3737335127](https://steamcommunity.com/sharedfiles/filedetails/?id=3737335127)。正式发布前可通过 Steam 客户端确认依赖可订阅，并用无本地重复安装的环境订阅一次。上游格式、预览图和可见性以 [Mega Crit 工作区文档](https://github.com/megacrit/sts2-mod-uploader/blob/v0.2.0/template/README.md) 为准。
