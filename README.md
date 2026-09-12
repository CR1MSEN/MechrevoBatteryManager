# 因为机械革命系列充电策略未做好，40系等老款在本软件开启了智能充电依旧可能涓流充电至100

Because the charging strategy for the Mechrevo series wasn't properly set up, Even with smart charging enabled in this software, older models like the 40 series might still trickle charge up to 100.

# 使用前一定要仔细阅读文档内容，避免造成机器损坏！

Make sure to read the documentation carefully before use to avoid damaging the machine!



# MechrevoBatteryManager

此应用是根据C#开发，用于给机械革命系列笔记本以及同方模具如XMG系列笔记本，借助官方控制台操作将官方充电限制功能实现的程序

`注意：此应用也许不能在所有型号上运行，请自行鉴别`

This app is developed in C# and is designed for the Mechrevo series laptops and Tongfang molds like the XMG series laptops, using the official console to implement the official charging limit feature. 

`Warn: This app might not work on all models, please check for yourself`


- Tips：**安装服务比开机自启动更好用**，安装完服务后你就可以直接删掉本文件了，服务会在开机时自动启动，运行完后自动结束，若已有写入则直接结束，办到省心省力，不想用了直接去`C:\Program Files\OEM\BatteryManager`路径删使用`Uninstall-MechrevoBatteryManager.cmd`进行彻底卸载。
- Tips: **Installation service is more convenient than automatic startup at boot**. After installing the service, you can directly delete this file. The service will automatically start when the computer boots up and end automatically after running. If there is already a write, it will end directly. This is hassle-free and effortless. If you don't want to use it anymore, you can go directly to the `C:\Program Files\OEM\BatteryManager` path and use `Uninstall-MechrevoBatteryManager.cmd` to uninstall completely.



``本程序已在翼龙（XMG Core），极光（XMG Neo），旷世（XMG Neo）等机型上验证跑通``

``This program has been verified and runs smoothly on models such as Yilong (XMG Core), Jiguang (XMG Neo), and Kuangshi (XMG Neo)``



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
- 只允许访问 EC `0x07B9`。
- 强制检查 `1 <= 充电上限 <= 100`。
- 写入前读取原值，每次写入后回读验证。
- 服务每次启动只执行一次，不循环写入 EC。

**使用步骤：**

1. 进入`...\MechrevoBatteryManager\dist`路径，以管理员身份运行 `MechrevoBatteryManager.exe`。
<img width="992" height="573" alt="QQ_1789227274193" src="https://github.com/user-attachments/assets/6d654b5a-06ff-4795-bbaf-ca7e3cc24104" />

**（虽然有自动检索，但仍可能需要手动配置DLL文件路径）**

2. 点击 **Read EC**，确认 `充电上限` 读数正常，若无法读数则代表你可能无法使用。
3. 设置阈值，应用并保存，此时会返回你的设置上限值。(保存后你应该可以看到自己的电池图标变为了全智能充电模式)
<img width="351" height="159" alt="QQ_1789151967778" src="https://github.com/user-attachments/assets/0400b41f-1d15-424d-8081-ac5323e43e28" />

4. 点击 **读取EC**，确保 `充电上限` 的读数正常。
5. 点击 **Install service** 可将程序应用注册为系统服务与卸载脚本，实现每次开机静默写入一次，防止被官方控制台或DP电源模式覆盖。

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

Safe behavior:

- Boot-time writing is disabled by default and must be explicitly enabled in the GUI.
- Only access to EC `0x07B9` is allowed.
- Perform a mandatory check to ensure that `1 <= charging upper limit <= 100`.
- Read the original value before writing, and perform readback verification after each write.
- The service is executed only once each time it is started, and does not write to EC repeatedly.

**Usage:**

1. Navigate to the path `...\MechrevoBatteryManager\dist` and run `MechrevoBatteryManager.exe` as an administrator.
<img width="992" height="573" alt="QQ_1789227274193" src="https://github.com/user-attachments/assets/03285806-2a04-4969-a944-9d500fc3186d" />

**(Although it has automatic detection, you may still need to manually set the DLL file path.)**

2. Click **Read EC** to confirm that the reading of 'Charging Upper Limit' is normal. If no reading is available, it indicates that you may not be able to use it. 
3. Set the threshold, apply and save it. At this point, the upper limit value you set will be returned. (After saving, you should see your battery icon change to the full smart charging mode)
<img width="351" height="159" alt="QQ_1789151967778" src="https://github.com/user-attachments/assets/69c09a11-6afe-41e7-aa71-af1a3554f391" />

4. Click **Read EC** to ensure that the reading of 'Charging Upper Limit' is normal.
5. Clicking **Install service** will register the program application as a system service and an uninstallation script, enabling silent writing once every time the computer boots up, thus preventing it from being overwritten by the official console or DP power mode.

To completely uninstall the service after the original GUI or source files disappear, run `Uninstall-MechrevoBatteryManager.cmd` as an administrator in the service installation path. #MechrevoBatteryManager


## GitHub source build / GitHub 源码构建

The repository contains the complete source tree and does not require .NET 8.0. It targets **.NET Framework 4.8**, uses x64 builds, and can be built on Windows with Visual Studio Build Tools or the .NET Framework MSBuild installation.

仓库包含完整源码，项目目标框架为 **.NET Framework 4.8**，使用 x64 架构，可通过 Visual Studio Build Tools 或系统中的 .NET Framework MSBuild 在 Windows 上构建。

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
