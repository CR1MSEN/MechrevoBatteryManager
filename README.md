# 使用前一定要仔细阅读文档内容，避免造成机器损坏！

Make sure to read the documentation carefully before use to avoid damaging the machine!



# MechrevoBatteryManager

此应用是根据C#开发，用于给机械革命系列笔记本以及同方模具如XMG系列笔记本，借助官方控制台操作将官方充电限制功能实现的程序

`注意：此应用也许不能在所有型号上运行，请自行鉴别`

This app is developed in C# and is designed for the Mechrevo series laptops and Tongfang molds like the XMG series laptops, using the official console to implement the official charging limit feature. 

`Warn: This app might not work on all models, please check for yourself`


- Tips：安装完服务后你就可以直接删掉本文件了，服务会在开机时自动启动，运行完后自动结束，若已有写入则直接结束，办到省心省力，不想用了直接去`C:\Program Files\OEM\BatteryManager`路径删使用`Uninstall-MechrevoBatteryManager.cmd`进行彻底卸载，仅留下log文件夹可手动删除。但我目前并不知道如何关闭这个充电保护，也许你可以尝试将上下限都改为0，最还原的方法是拿另一个TypeC充电器进行一次DP充电或拿另一个不同的电源适配器接入，就会变为最开始的样子（切记卸载掉服务后这样，否则下一次开机会被重置）
- Tips: After installing the service, you can just delete this file. The service will start automatically at boot, finish running on its own, and if there’s already data written, it will end immediately. It’s hassle-free. If you don’t want it anymore, just go to `C:Program Files\OEM\BatteryManager` and use `Uninstall-MechrevoBatteryManager.cmd` to completely uninstall it. Only the log folder will be left, which you can delete manually. But I don’t currently know how to turn off this charging protection. Maybe you can try setting both the upper and lower limits to 0. The most reliable way to reset it is to use another Type-C charger for a DP charge or plug in a different power adapter, and it will go back to the original state (just make sure to uninstall the service first, otherwise it will reset the next time you turn it on).



``本程序已在翼龙（XMG Core），极光（XMG Neo），旷世（XMG Neo）机型上验证跑通``

``This program has been verified to run on the Yilong(XMG Core), Jiguang(XMG Neo), and Kuangshi(XMG Neo) models.``



## 中文说明

这是面向机械革命/同方机型的电池充电阈值管理工具，目标框架为 **.NET Framework 4.8**

服务文件安装到：

```text
C:\Program Files\OEM\BatteryManager\
```

服务日志：

```text
C:\Program Files\OEM\BatteryManager\log\service.log
```

用户配置位于当前用户目录：

```text
%USERPROFILE%\MechrevoBatteryManager\config.json
```

旧版本的 `C:\ProgramData\MechrevoBatteryManager\config.json` 会被兼容读取；保存后使用新的用户目录。

发布文件：

- `MechrevoBatteryManager.exe`：管理员权限 WinForms 配置界面。
- `MechrevoBatteryManager.Service.exe`：延迟自动启动的 Windows 服务。
- `MechrevoBatteryManager.Core.dll`：OEM EC 调用层。
- `Uninstall-MechrevoBatteryManager.cmd`：服务卸载和目录清理脚本。

安全行为：

- 开机写入默认关闭，必须在 GUI 中明确启用。
- 只允许访问 EC `0x07B9` 和 `0x07D0`。
- 强制检查 `0 <= 复充下限 < 充电上限 <= 100`。
- 写入前读取原值，每次写入后回读验证。
- 服务每次启动只执行一次，不循环写入 EC。
- `0x07A6` 暂不修改，因为其位定义具有机型差异。

**使用步骤：**

1. 进入`...\MechrevoBatteryManager\dist`路径，以管理员身份运行 `MechrevoBatteryManager.exe`。
<img width="992" height="573" alt="99950e73b9dd6c319c684db7fdab2495" src="https://github.com/user-attachments/assets/89a2da7c-9058-4907-ac89-383274e9145e" />


**（虽然有自动检索，但仍可能需要手动配置DLL文件路径）**

1. 点击 **Read EC**，确认 `0x07B9/0x07D0` 读数正常，若无法读数则代表你可能无法使用。
2. 设置阈值，应用并保存。(保存后你应该可以看到自己的电池图标变为了全智能充电模式)
<img width="351" height="159" alt="QQ_1789151967778" src="https://github.com/user-attachments/assets/0400b41f-1d15-424d-8081-ac5323e43e28" />

4. 点击 **Install service** 可将程序应用注册为系统服务与卸载脚本，实现每次开机静默写入一次，防止被官方控制台覆盖。

要在原始 GUI 或源文件消失后完全卸载服务,请在服务安装路径中以管理员身份运行 `Uninstall-MechrevoBatteryManager.cmd` 。# MechrevoBatteryManager

## English

This tool manages battery charge and recharge thresholds on supported Mechrevo/Tongfang machines. It targets **.NET Framework 4.8** .

Service files are installed at:

```text
C:\Program Files\OEM\BatteryManager\
```

Service logs are stored at:

```text
C:\Program Files\OEM\BatteryManager\log\service.log
```

Per-user configuration is stored at:

```text
%USERPROFILE%\MechrevoBatteryManager\config.json
```

An older `C:\ProgramData\MechrevoBatteryManager\config.json` is read for compatibility; saving writes the configuration to the new user directory.

Safety behavior:

- Startup writes are disabled by default and must be explicitly enabled.
- Only EC addresses `0x07B9` and `0x07D0` are writable.
- Values must satisfy `0 <= lower < upper <= 100`.
- Original values are read first and every write is verified by readback.
- The service performs one apply attempt per service start and does not loop.
- EC mode byte `0x07A6` is intentionally untouched because its layout is model-specific.

**Usage:**

1. Enter the path`...\MechrevoBatteryManager\dist`, Run `MechrevoBatteryManager.exe` as an administrator. If you can't get a reading, it probably means you can't use it.
  <img width="992" height="573" alt="99950e73b9dd6c319c684db7fdab2495" src="https://github.com/user-attachments/assets/32c787c6-6ecd-40c0-8cdc-8c411b6087c9" />


**(Although it has automatic detection, you may still need to manually set the DLL file path.)**

3. Click **Read EC** to make sure the `0x07B9/0x07D0` readings are normal.
4. Set the thresholds, then apply and save.(After saving, you should be able to see your battery icon change to full smart charging mode)
<img width="351" height="159" alt="QQ_1789151967778" src="https://github.com/user-attachments/assets/69c09a11-6afe-41e7-aa71-af1a3554f391" />

6. Click **Install service** to register the program as a system service along with the uninstall script, Make it write silently every time the computer starts up to prevent it from being overwritten by the official console.

To completely uninstall the service after the original GUI or source files disappear, run `Uninstall-MechrevoBatteryManager.cmd` as an administrator in the service installation path. #MechrevoBatteryManager

## GitHub source build / GitHub 源码构建

The repository contains the complete source tree and does not require .NET 8.0. It targets **.NET Framework 4.8**, uses x64 builds, and can be built on Windows with Visual Studio Build Tools or the .NET Framework MSBuild installation.

仓库包含完整源码，不依赖 .NET 8.0。项目目标框架为 **.NET Framework 4.8**，使用 x64 架构，可通过 Visual Studio Build Tools 或系统中的 .NET Framework MSBuild 在 Windows 上构建。

From the repository root:

```powershell
.\build.ps1 -Configuration Release
```

构建完成后，GUI、服务、核心 DLL 和卸载脚本会复制到：

```text
MechrevoBatteryManager\dist\
```

The GitHub Actions workflow in `.github/workflows/build.yml` performs the same Release build on `windows-latest`.

`.github/workflows/build.yml` 中的 GitHub Actions 工作流会在 `windows-latest` 上执行相同的 Release 构建。
