# 魔女 v1.6.0 创意工坊发布准备

这个工作区供确认后使用。包含上传所需的 `workshop.json`、`content/`、`image.png` 和 macOS 实机截图。包内声明 BaseLib 的工坊依赖，手动安装 ZIP 则附带验收用的锁定版本与许可证。

## 确认后发布

1. 在 Steam 客户端登录拥有游戏的账号。确认 Steam Workshop 条款和选定的可见性。
2. 使用 [Mega Crit 官方 Mod Uploader](https://github.com/megacrit/sts2-mod-uploader/releases/tag/v0.2.0) 的 macOS arm64 构建，进入解压后的上传工具目录。
3. 将命令中的路径替换成解压出的 `SteamWorkshop` 目录。模板为 `private`，便于先核对商店页与依赖，再用 Steam 改为公开。官方工具会给首次上传分配项目 ID，后续更新保留工具写出的 `mod_id.txt`。

```sh
./ModUploader upload -w "/absolute/path/to/SteamWorkshop"
```

工坊依赖为 [BaseLib #3737335127](https://steamcommunity.com/sharedfiles/filedetails/?id=3737335127)。正式发布前可通过 Steam 客户端确认依赖可订阅，并用无本地重复安装的环境订阅一次。上游格式、预览图和可见性以 [Mega Crit 工作区文档](https://github.com/megacrit/sts2-mod-uploader/blob/v0.2.0/template/README.md) 为准。
