# MGR Steam 创意工坊更新与发布操作指南

本文供维护者和 Codex 执行 MGRMod 的 Steam 创意工坊更新。它覆盖四类操作：十语言标题、十语言简介、Steam 更新说明（Change Note）以及最新三版本游戏包。工坊项目固定为 `3784531815`。

## 一、最重要的发布边界

四类内容必须分清，不能把所有文件交给上传器一次盲传：

| 目标 | 本地来源 | 使用脚本 | 会修改什么 |
|---|---|---|---|
| 十语言标题和简介 | `MGRWorkshop/titles/*.txt`、`MGRWorkshop/descriptions/*.txt` | `Upload-LocalizedMetadata.ps1` | 只修改十语言标题和简介 |
| 最新游戏包，不写更新说明 | `MGRWorkshop/content` | `Upload-ContentOnly.ps1` | 更新游戏内容和主预览图，不修改标题、简介、标签、依赖、可见性 |
| 最新游戏包并写更新说明 | 同上，加一段 Change Note | `Upload-ContentOnly.ps1 -ChangeNote ...` | 更新游戏内容、主预览图和本次 Steam 更新说明 |
| 只在本地编译打包 | 当前 `mgrmod` 源码和资源 | `Prepare-Workshop.ps1` | 只刷新本地发布目录，不联系 Steam |

必须遵守以下原则：

1. 普通游戏包发布默认不更新十语言标题或简介。
2. 更新任意语言的简介时，必须把同语言标题一起提交；本项目固定整组提交十种语言。
3. Steam 更新说明不能通过元数据脚本单独设置，只能随游戏内容上传使用 `-ChangeNote` 提交。只想改更新说明也会重新提交当前游戏包。
4. 不要直接用上传器对完整 `MGRWorkshop` 目录执行普通 `upload`。必须使用本项目的包装脚本，避免误改标题、简介、标签、依赖或可见性。
5. 绝对不要删除或改写 `MGRWorkshop/mod_id.txt`。删除后上传器可能新建第二个工坊项目，而不是更新 `3784531815`。
6. 上传前先确认 `docs/当前工作项.md` 中有哪些尚未发布的代码或资源。所谓“上传最新版本”会包含当前工作区内全部构建输入，不只包含聊天中最后提到的一项改动。
7. 只有用户明确要求上传时才联系 Steam；本地翻译、验证和打包不等于获得上传授权。

## 二、发布电脑的环境准备

先进入实际使用的工作区根目录。例如当前 Codex 工作区为：

```text
C:\Users\pc\.codex\projects\塔2模组
```

发布前确认：

- Steam 客户端已经启动，并登录拥有工坊项目的 `MGRSK` 账号；
- .NET SDK、PowerShell 7 和 Godot/.NET 构建环境可用；
- `MGRWorkshop/mod_id.txt` 的内容为 `3784531815`；
- `MGRWorkshop/image.png` 存在且小于 1 MiB；
- 三套固定游戏引用存在：
  - `.tools/sts2_v107.1_original_refs`
  - `.tools/sts2_v111.0_original_refs`
  - `.tools/sts2_v113.0_original_refs`
- 上传器存在于：
  - `uploader/artifacts/publish/ModUploader/release_win-x64/ModUploader.exe`

如果上传器缺失，可在工作区根目录重新发布：

```powershell
dotnet publish .\uploader\ModUploader.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o .\uploader\artifacts\publish\ModUploader\release_win-x64
```

如果正式版或测试版游戏版本已经变化，不要继续机械使用上面的三个旧引用目录。先按 [`MGR双版本兼容维护手册.md`](MGR双版本兼容维护手册.md) 完成新版本归档、接口核查和三端实机验证，再更新构建目标。

## 三、Codex 开始任务时的固定动作

在任何实质工作前依次读取：

1. 工作区根目录 `AGENTS.md`；
2. `mgrmod/AGENTS.md`；
3. `mgrmod/docs/总览目录.md`；
4. `mgrmod/docs/当前工作项.md`；
5. 本指南；
6. [`Steam工坊本地化维护规则.md`](../localization/Steam工坊本地化维护规则.md)；
7. 涉及构建时再读 [`MGR双版本兼容维护手册.md`](MGR双版本兼容维护手册.md)。

随后执行：

```powershell
git -C .\mgrmod status --short
git -C .\mgrmod diff --check
```

不要覆盖或回滚不属于本次任务的现有修改。需要修改文本、代码、资源、构建或发布状态时，先在 `docs/当前工作项.md` 登记本次工作项。

## 四、更新十语言简介

### 1. 文件与翻译顺序

简体中文是语义权威源：

```text
MGRWorkshop/descriptions/schinese.txt
```

先完成简体中文，再直接翻译到其余九种语言，不要以英语作为中间源。十种 Steam 文件名为：

```text
english.txt
schinese.txt
tchinese.txt
japanese.txt
koreana.txt
french.txt
german.txt
spanish.txt
brazilian.txt
russian.txt
```

游戏内术语、卡牌名和遗物名必须从对应语言的游戏本地化读取：

```text
mgrmod/MGRMod/localization/<语言简写>/*.json
mgrmod/docs/localization/<语言简写>/翻译指导.md
```

原版名词还要从对应版本的原版本地化中核对，不可凭感觉直译。

### 2. 更新日志写法

- 在简介的更新日志顶部新增 `[h2]YYYY-MM-DD[/h2]` 日期块，所有日期保持倒序；
- 同一天的内容放在同一个 `[list]...[/list]` 中；
- 玩家只能看到结果，不写类名、源码路径、哈希或内部实现；
- 普通卡牌数值调整合并为一条“对 A、B、C 进行了平衡性调整”，但必须列全名称；
- 同类动画或特效调整合并为一条表现改进；
- 不删除旧日期。接近字节上限时，只压缩旧日志的重复措辞；
- 俄语可进一步精简，但必须保留日期、最终结果和涉及名称，并尽量维持约 7000 UTF-8 字节或更低。

紧凑格式示例：

```text
[h1]更新日志[/h1]
[h2]2026-11-13[/h2]
[list]
[*]修复……
[*]对……进行了平衡性调整。
[/list]
[h2]上一日期[/h2]
[list]
[*]……
[/list]
[hr][/hr]
```

### 3. 永久排除项

以下内容只保留在简体中文，其他九种语言必须整段删除：

- B站联系方式；
- QQ 联系方式；
- 包含“这句不用翻译”的整行；
- “特别鸣谢”标题及整个段落。

X、Discord、BUG反馈方式及普通内容需要正常翻译。

### 4. 固定链接

每种简介中以下链接各保留一次：

```text
RitsuLib: https://steamcommunity.com/sharedfiles/filedetails/?id=3747602295
一代MGRMod: https://steamcommunity.com/sharedfiles/filedetails/?l=norwegian&id=3024027467
GitHub: https://github.com/MGRSK1960385817/SlayTheSpire2MGRMod
Nexus: https://www.nexusmods.com/slaythespire2/mods/1456?published=1
```

“开源”模块必须位于整个简介最后。

### 5. 字节限制

- 标题：单行纯文本，最多 128 个 UTF-8 字节；
- 简介：Steam 要求严格小于 8000 个 UTF-8 字节；
- 全部文件保存为 UTF-8 无 BOM。

快速检查十份简介大小：

```powershell
Get-ChildItem .\MGRWorkshop\descriptions\*.txt |
  Sort-Object Name |
  ForEach-Object {
    $text = [IO.File]::ReadAllText($_.FullName)
    [pscustomobject]@{
      File = $_.Name
      Utf8Bytes = [Text.Encoding]::UTF8.GetByteCount($text)
    }
  }
```

任何文件达到 `8000` 都不能上传。还要人工核对 BBCode 成对、日期倒序、固定链接、正式译名和永久排除项。

## 五、更新十语言标题

标题位于：

```text
MGRWorkshop/titles/<Steam语言代码>.txt
```

对照表见 `MGRWorkshop/titles/README.md`。标题文件只能有一行，不使用 BBCode。若用户要求给全部语言追加相同含义的版本提示，应分别自然翻译，不能把简体中文原文复制到其他语言。

即使本次只修改简介，也不要遗漏标题文件；元数据脚本会把十种标题和简介成对提交，这是为了避免 Steam 清空非英语本地化标题。

## 六、只发布十语言标题和简介

完成本地校验后，在工作区根目录执行：

```powershell
pwsh -NoProfile -File .\MGRWorkshop\Upload-LocalizedMetadata.ps1
```

该脚本会建立临时工作区，并把以下字段全部设为不提交：

- 游戏内容和预览图；
- 标签；
- 依赖；
- 可见性；
- Steam 更新说明；
- 分支限制。

成功日志必须同时出现十种语言的：

```text
Updated the <language> Workshop title/description metadata.
Verified the online <language> Workshop title and description.
```

最后必须出现：

```text
Successfully updated and verified localized metadata for workshop item 3784531815.
```

上传器会精确验证在线标题，并确认每种在线简介非空。若需人工检查具体内容，可使用下列 URL 切换语言：

```text
https://steamcommunity.com/sharedfiles/filedetails/?id=3784531815&l=english
https://steamcommunity.com/sharedfiles/filedetails/?id=3784531815&l=schinese
https://steamcommunity.com/sharedfiles/filedetails/?id=3784531815&l=japanese
```

把 `l=` 后面的值替换为十种 Steam API 语言代码即可。

## 七、编译并打包最新三版本游戏包

先确认当前工作区中所有会进入发布包的改动都允许发布，再执行：

```powershell
pwsh -NoProfile -File .\MGRWorkshop\Prepare-Workshop.ps1 `
  -V107DataDir ".\.tools\sts2_v107.1_original_refs" `
  -V111DataDir ".\.tools\sts2_v111.0_original_refs" `
  -V113DataDir ".\.tools\sts2_v113.0_original_refs"
```

该脚本会：

1. 以 warnings-as-errors 运行内容登记校验；
2. 构建共享 PCK；
3. 分别构建 v0.107.1、v0.111.0 与 v0.113.0 payload；
4. 构建稳定 Loader；
5. 刷新 `MGRWorkshop/content`；
6. 检查发布目录只能包含七个规定文件；
7. 生成 `MGRWorkshop/SHA256SUMS.txt`；
8. 检查主预览图小于 1 MiB。

最终目录必须严格为：

```text
MGRWorkshop/content/MGRMod.dll
MGRWorkshop/content/MGRMod.json
MGRWorkshop/content/MGRMod.pck
MGRWorkshop/content/mgrmod-variants.manifest
MGRWorkshop/content/lib/0.107.1/MGRMod.dll
MGRWorkshop/content/lib/0.111.0/MGRMod.dll
MGRWorkshop/content/lib/0.113.0/MGRMod.dll
```

构建日志必须是三个目标均 `0 个警告、0 个错误`。顶层 `MGRMod.dll` 的程序集身份应为 `MGRMod.Loader`，三份 payload 应为 `MGRMod`；manifest 中的 SHA-256 必须与实物一致。

构建后再次执行：

```powershell
pwsh -NoProfile -File .\mgrmod\docs\tools\Validate-MgrContent.ps1 -WarningsAsErrors
git -C .\mgrmod diff --check
Get-Content .\MGRWorkshop\SHA256SUMS.txt
```

`Prepare-Workshop.ps1` 本身不会启动上传器，也不会联系 Steam。

## 八、设置 Steam 更新说明并上传游戏包

### 情况 A：不需要更新说明

```powershell
pwsh -NoProfile -File .\MGRWorkshop\Upload-ContentOnly.ps1
```

### 情况 B：需要更新说明

推荐用英文、简短、只写玩家可见内容：

```powershell
$changeNote = @'
Fixed an issue where ...
Adjusted the balance of ...
'@

pwsh -NoProfile -File .\MGRWorkshop\Upload-ContentOnly.ps1 `
  -ChangeNote $changeNote
```

`-ChangeNote` 就是 Steam 工坊“改动说明／Change Notes”中的本次补丁说明。它不是十语言简介的一部分，目前只提交一份文本。没有明确要求时应省略该参数，不要擅自写更新说明。

内容脚本的临时配置会把标题、简介、标签、依赖、可见性和分支限制全部设为 `null`，因此不会改动这些线上字段。普通内容上传会重新提交同一份 `image.png` 主预览图；只要本地图片未变，线上视觉内容不变。

成功日志末尾应出现：

```text
Successfully uploaded '' to the workshop with id 3784531815!
```

若出现需要接受 Workshop Legal Agreement 的提示，应停止宣称发布完成，并让账号持有人在 Steam 页面接受协议后重试。

## 九、一次完成“标题/简介 + 更新说明 + 最新游戏包”

推荐顺序如下：

1. 读取规则、当前工作项和未发布队列；
2. 修改简体中文简介；
3. 按语言指导更新其余九种简介和十种标题；
4. 完成字节、BBCode、日期、链接、术语和排除项检查；
5. 运行 `Prepare-Workshop.ps1`，先把最终发布候选完整构建并验证；
6. 运行 `Upload-LocalizedMetadata.ps1`，上传并在线核验十语言标题和简介；
7. 运行 `Upload-ContentOnly.ps1 -ChangeNote $changeNote`，上传游戏包和本次更新说明；
8. 查询 Steam 公共接口，确认线上大小、内容句柄和更新时间；
9. 更新项目发布记录并输出简短中英文更新报告。

不要在本地候选包尚未成功构建时先更新线上简介，以免出现“简介已经宣布更新、游戏包却构建失败”的半发布状态。

## 十、上传后的线上核验

### 1. 查询线上内容信息

```powershell
$response = Invoke-RestMethod `
  -Method Post `
  -Uri 'https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/' `
  -Body @{
    itemcount = '1'
    'publishedfileids[0]' = '3784531815'
  }

$details = $response.response.publishedfiledetails[0]
$beijing = [TimeZoneInfo]::ConvertTimeBySystemTimeZoneId(
  [DateTimeOffset]::FromUnixTimeSeconds([int64]$details.time_updated),
  'China Standard Time'
)

[pscustomobject]@{
  Result = $details.result
  FileSize = $details.file_size
  ContentHandle = $details.hcontent_file
  UpdatedBeijing = $beijing.ToString('yyyy-MM-dd HH:mm:ss')
  Title = $details.title
}
```

检查标准：

- `Result` 为 `1`；
- `FileSize` 等于本地 `MGRWorkshop/content` 七个文件的总大小；
- 内容上传后 `ContentHandle` 与 `time_updated` 应发生变化；
- 英语标题仍然正确；
- 元数据上传器已经逐语言验证十种标题与简介。

本地总大小：

```powershell
(Get-ChildItem .\MGRWorkshop\content -Recurse -File |
  Measure-Object Length -Sum).Sum
```

Steam 客户端下载有缓存。若需要验证玩家实际收到的文件，应等待客户端刷新后检查工坊下载目录中的时间、大小与程序集/资源，而不能只看本地构建目录。

### 2. 发布记录闭环

发布成功后更新：

- `mgrmod/docs/当前工作项.md`：记录发布状态、北京时间、线上大小和内容句柄；
- `mgrmod/docs/records/MGR错误修复与表现改进记录.md`：只有实际进入线上包的条目才添加 `✅`；
- `MGRWorkshop/PREUPLOAD_CHECKLIST.md`：追加本次元数据和内容发布结果；
- 当前统一发布标记：改为本次真实线上时间和发布工作项。

仍未实机验证的实现可以继续标为“待验证”，但应单独写明“已发布、仍待真实客户端回归”，不能写成“尚未发布”。

## 十一、常见错误与处理

### 非英语标题突然消失

原因通常是只提交了简介。重新确认十种 `titles/*.txt` 与 `descriptions/*.txt` 成对存在，然后运行：

```powershell
pwsh -NoProfile -File .\MGRWorkshop\Upload-LocalizedMetadata.ps1
```

### 简介超过 8000 字节

不要删除日期或关键结果。优先压缩旧日志的背景、重复修饰和具体数值；俄语按长期规则进一步精简。要求是严格小于 8000，不是小于等于。

### Steam 最新包的时间或哈希没有变化

先检查：

1. 是否真的运行了 `Prepare-Workshop.ps1`；
2. `MGRWorkshop/content` 中 DLL/PCK 时间和 SHA-256 是否已变化；
3. 是否运行的是 `Upload-ContentOnly.ps1`，而不是只更新元数据；
4. Steam 公共接口的 `hcontent_file` 与 `time_updated` 是否变化；
5. 是否误用了另一份工作区或错误的 `mod_id.txt`。

### 上传器无法初始化 Steam

确认 Steam 已启动、账号正确、`steam_api64.dll` 与 `steam_appid.txt` 位于上传器发布目录，并关闭可能占用 Steam API 的异常上传器进程后重试。

### 构建成功但不确定是否安全

编译只证明 API 可以绑定。正式发布前仍应在两个真实客户端覆盖角色选择、战斗、音符、演奏、卡牌去向、保存读取、胜负/放弃和多人机制。不要把“0错误”写成“实机验证通过”。

## 十二、可直接交给另一台 Codex 的任务模板

```text
请在当前“塔2模组”工作区中维护 MGRMod。

开始任何操作前，完整阅读：
1. 根目录 AGENTS.md
2. mgrmod/AGENTS.md
3. mgrmod/docs/总览目录.md
4. mgrmod/docs/当前工作项.md
5. mgrmod/docs/project/MGR Steam创意工坊更新与发布操作指南.md
6. mgrmod/docs/localization/Steam工坊本地化维护规则.md
7. mgrmod/docs/project/MGR双版本兼容维护手册.md

本次任务：<在这里写具体改动>。

要求：
- 先登记工作项并检查当前所有未发布代码，不能误发无关半成品；
- 十语言简介以 schinese.txt 为语义源，直接翻译，卡牌/遗物名读取各语言游戏内正式名称；
- 任何简介更新都必须同时提交十种原标题，使用 Upload-LocalizedMetadata.ps1；
- 标题必须单行且不超过128 UTF-8字节，简介必须严格小于8000 UTF-8字节，俄语尽量控制在约7000字节；
- 先运行 Prepare-Workshop.ps1 完成v0.107.1/v0.111.0/v0.113.0三版本构建、七文件清单和哈希校验；
- 只有我明确要求上传时，才运行 Steam 上传脚本；
- 游戏包使用 Upload-ContentOnly.ps1。需要Steam更新说明时，仅通过 -ChangeNote 提交；没有要求就不要写；
- 不修改标签、依赖、可见性、分支限制、主预览图或其他元数据；
- 上传后查询Steam公共接口，确认线上大小、内容句柄和更新时间，并闭环当前工作项、总变更记录和PREUPLOAD_CHECKLIST；
- 最后输出简短中文版和英文版更新报告。
```

