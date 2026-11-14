# STS2 v0.111.0-v0.113.0 兼容迁移记录

## 一、背景

2026-11-13，用户报告《杀戮尖塔2》正式版升至v0.111.0、测试版升至v0.113.0。MGR原发布包只提供v0.107.1/v0.111.0载荷，Loader无法为v0.113.0选择主体程序集，因此模组不能加载。本轮目标是滚动淘汰v0.107.1、加入v0.113.0，并核查新接口对玩法语义的影响。

## 二、引用与环境

- v0.111.0原始引用归档：`.tools/sts2_v111.0_original_refs`，commit `41cef1ea`。
- v0.113.0原始引用归档：`.tools/sts2_v113.0_original_refs`，commit `16ac0fe3`，包含`sts2.dll`、`GodotSharp.dll`、`0Harmony.dll`、`Steamworks.NET.dll`、`release_info.json`与`SHA256SUMS.txt`。
- Godot：`D:\Godot\Godot_v4.5.1-stable_mono_win64.exe`，实际完成共享PCK导出。
- .NET：9 SDK，可完成两版Release编译。
- RitsuLib：0.6.12，同时提供v0.111.0与v0.113.0编译/运行变体。

本机Steam分支存在一项外部异常：检查时`public`实际下发v0.107.1/build 23811903，而`public-beta`下发v0.113.0/build 26625880。为了不把本机留在新候选包不再支持的v0.107.1，检查后恢复到`public-beta`；最终安装的`release_info.json`为v0.113.0、commit `16ac0fe3`。这不推翻用户给出的发布目标，但意味着正式版实机回归必须等本机public真正提供v0.111.0或使用另一份真实v0.111.0客户端。

## 三、实施内容

1. 将`MGRMod.json`升至0.8.1，最低游戏版本改为0.111.0，RitsuLib最低版本改为0.6.12。
2. 将项目、manifest、bundle、ZIP与工坊准备脚本的目标统一改为0.111.0/0.113.0，并主动清理旧`lib/0.107.1`。
3. 用`STS2_V111`处理不能共享的遗物override与动态变量override；其余普通调用集中到`MgrCrossVersionApi`。
4. 适配按玩家选牌栈、结束回合撤销、Tween等待返回值、只读`HoverTip.Id`以及v0.113.0动态变量行为。
5. 保留用户此前未提交的多人槽位和视觉调整，只在三个既有Tween等待调用点改用兼容包装；没有重写或回退其余工作。
6. 更新README、双版本维护手册、迁移方案、当前工作项和工坊发布前资料；没有刷新已发布的`MGRWorkshop/content`快照。
7. 修复普通Windows PowerShell终端构建时的退出代码9009：新电脑最初没有用户可见的`pwsh`，此前成功调用的是Codex私有运行时中的PowerShell 7。用户安装PowerShell 7.7.0后，MSBuild改为优先解析winget/MSIX用户别名，再检查MSI系统路径，最后回退Windows PowerShell 5.1；不再依赖PATH中的裸命令。哈希计算继续使用脚本内置的.NET SHA-256实现，避免旧版PowerShell的模块自动加载差异。

## 四、验证结果

- v0.111.0单payload：Release 0警告、0错误。
- v0.113.0单payload：Release 0警告、0错误。
- 完整构建：Loader、两版payload、manifest与Godot PCK全部成功，0警告、0错误。
- PowerShell 7入口：MSBuild解析为`C:\Users\pc\AppData\Local\Microsoft\WindowsApps\pwsh.exe`，实际版本7.7.0；使用该入口执行完整不部署构建再次得到0警告、0错误，两版payload哈希与已部署版本一致。
- 内容校验：94张启用卡、奖励池80张（20/35/25）、11件启用遗物、289个导入UID，warnings-as-errors通过。
- 六文件bundle：只含顶层Loader、JSON、PCK、manifest和两版payload；v0.111.0 payload SHA-256为`1EB8885971611AFB8BB0ABD4E82DFD855ADB39F336BAB4E773BEE11BE3488775`，v0.113.0为`9E0C041DA7A589717930DA47B1679608A0793413F6D2629E99F0F12A67F7BC2A`。
- 音频PCK核验：从导出的PCK反向恢复`MGR.bank`与`GUIDs.txt`，与源文件逐字节哈希一致。`MGR.bank`为253056字节、SHA-256 `90123263393A085A0E6CFE5DE0B2FEFE5B0405C9A97AC6ADE7D1143DA3D6555C`；`GUIDs.txt`为825字节、SHA-256 `2BDE26531CF45CCB1B8D4AD043BE12F84FB3DAB0E53519C724D237C9D7E12317`。

最初的兼容验证只生成本地候选产物。用户随后明确从普通Windows PowerShell执行本地部署构建；修复`pwsh`退出代码9009与`Get-FileHash`模块加载问题后，同一命令以0警告、0错误完成，并把严格六文件包部署到`D:\Steam\steamapps\common\Slay the Spire 2\mods\MGRMod`。安装清单版本为0.8.1，两版payload哈希与manifest一致；尚未启动游戏，也没有准备或上传Steam工坊内容。

## 五、遗留风险与下一步

- v0.111.0正式客户端尚未实机运行；本机Steam public分支异常仍需外部更新或另一台客户端确认。
- v0.113.0虽然真实程序集编译通过，仍需进游戏确认Loader关联、RitsuLib注册、遗物准入、选牌器、Tween取消、动态数字与提示框的运行语义。
- 本轮共享了现有未发布的多人槽位与视觉工作，因此最终上传前还要按当前工作项完成相应多人/视觉回归，不能把“兼容迁移通过”误写成所有未发布改动都已验证。
- 只有在用户明确授权后，才运行`MGRWorkshop/Prepare-Workshop.ps1`刷新工坊内容并执行上传；届时重新记录最终七文件哈希与Steam返回值。

## 六、v0.107.1重新纳入临时发布目标

用户再次核对后确认Steam主分支仍显示v0.107.1，而更新公告写作v0.111.0。为避免依赖公告或分支标签猜测，发布候选改为同时携带v0.107.1、v0.111.0和v0.113.0三份原生payload；Loader继续以宿主`release_info.json`为唯一选择依据。`MGRMod.json`最低版本恢复为0.107.1。

v0.107.1原始引用使用commit `59260271`；RitsuLib 0.6.12工坊包实际提供三套对应变体，现已固定归档到`.tools/ritsulib_v0.6.12_refs`并记录逐文件SHA-256。项目不再让v0.107.1使用旧0.5.13编译包，而是三版都直接绑定与当前运行依赖相同的RitsuLib 0.6.12程序集。

源码重新启用既有`STS2_V107`分支，并把本轮新增的遗物准入、选牌器、撤销回合准备、动态变量与Tween等待兼容层扩展到v0.107.1。三份单payload和三端Loader分别针对真实原版引用完成Release编译，均为0警告、0错误；完整不部署构建也成功生成严格七文件bundle。当前payload SHA-256为：v0.107.1 `313048386DCFBF0724C7FC6575AA113404AFCDBB857263B7290F6693172D0D81`、v0.111.0 `8D73EF8C741CFD514DB61E2BCA05D28807104AE8181F8105D1DC08045E3D9584`、v0.113.0 `05220FF896F071B841AAF1CA3CBF392F643D6D2053881F2A12BF94D595CD298A`。本地七文件ZIP为147452077字节，SHA-256 `9471935F3EE8B3867689B1C83EAA62C366BAEF55DB45C5806EA497FDF06C9BCB`。内容登记、脚本语法、JSON/XML、manifest哈希与十语言字节限制全部通过；本次三版本扩展尚未部署到游戏目录或上传Steam。
