## v1.6.1：结合本机官方 GCU 静态检查的修订

- 增加“驱动诊断”：只读取 DLL/EXE 文件，检查 x64 PE 架构、ReadEC/WriteEC 导出和版本信息，不加载官方程序、不调用 EC。诊断同时列出已找到的充电/电源灯接口与未确认项。
- 驱动检索直接在 AiStoneService 目录中寻找兼容的 ACPIDriverDll.dll，不再要求先发现 GCUService.exe；加载 DLL 前同样校验架构和导出，拒绝错误文件。
- GUI 与开机服务通过跨会话命名互斥锁协调 EC 操作，最多等待 10 秒，避免本软件自身的读写/备份交错。该锁不约束官方控制台或固件。
- 档位写入后等待 150 毫秒再写准确上限，全部写入后进行即时及延迟 150 毫秒的回读。发现覆盖时尝试恢复之前的档位与上限，回滚失败明确报错。时间间隔是本软件的验证策略，并非从被保护的官方代码中还原的时序；更晚发生的覆盖无法由这次检查保证发现。
- 保留自动阈值联动、下限零写入和一次性服务；未新增充电档位选择栏。
- 根据检查更新充电灯提示：已发现 SetPowerLedStatus(UInt32)，但参数映射仍受代码保护，关闭/变白选项继续禁用。不使用未确认的 NVRAM/消息写入协议。
- 发布包附带 OEM检查说明.md，区分静态确认内容和未知参数。所有自动测试采用模拟 EC；实际充电效果未做硬件写入验证。

## v1.6：充电档位联动与下限零写入

- **不再写入下限 `0x07D0`**：立即应用、开机服务、恢复默认及失败回滚均不写入；旧配置下限加载时归零。为兼容 v1.5，仍保留该寄存器的读取和备份。
- 自动阈值联动：1–60% → 工作站（2），61–80% → 平衡（1），81–100% → 长效（0）。三档官方预设为 60/80/100%，区间映射是本软件用于自定义阈值的策略。只修改 `0x07A6` 的 bits[5:4]，保留其他位；先设置档位，再写 `0x07B9` 的百分比。界面无需选择充电档位，立即应用与开机服务均自动联动；旧配置的手动档位策略被忽略。自定义阈值的实际固件行为尚未验证。
- 上限按完整字节写入及回读，不再将 bit 7 直接屏蔽。0 表示停用本软件的应用操作，不会撤销已经写入的硬件状态；撤销请用“恢复默认”。
- 每次修改前保存完整原值，逐项回读，并在全部写入后再次验证。失败时先恢复档位位域，再恢复原上限；回滚失败会明确报错。读取/写入不是原子操作，官方控制台同时改档仍可能覆盖配置。
- 新备份包含档位；v1.5 `Default.json` 不覆盖，首次补充的档位另存 `Default.json.profile.json`。补充值代表升级时的档位，不能重建 v1.5 首次备份时的档位。恢复默认只恢复充电档位位域和上限，不恢复下限。没有档位备份时仅恢复上限。
- 新增“关闭充电灯”和“达到阈值后变白”两个互斥选项及配置字段，**目前禁用，尚不可实际使用**。已知 `PowerLight_LEDPWM = 2040 = 0x07F8`，但未确认 PWM 值/位域、关闭值、颜色寄存器或白色值。部分机型电源键红色表示充电，绿/蓝/白也用于性能模式，不能从亮度地址推导颜色，更不能通过切换狂暴模式实现白灯。
- 灯光功能启用前需要对应机型的接口/寄存器映射；“达到阈值后变白”若没有固件级规则，还需要电池状态监测。当前服务仍只执行一次，不包含常驻监测。
- 保留官方 GCU 驱动调用：`C:\Program Files\OEM\机械革命电竞控制台\AiStoneService\MyControlCenter\ACPIDriverDll.dll` 的 `ReadEC` / `WriteEC`。没有修改官方文件、安装服务或向真实 EC 写入。
- **回读成功只证明寄存器接受了值，不证明固件实际停止充电。** 40 系等机型原有涓流充电问题是否解决仍需实机观察；本次没有改写固件 gate/ROMID 等未知控制位。

档位位域依据用户提供的信息，并与 [TUXEDO 官方源码](https://github.com/tuxedocomputers/tuxedo-keyboard/blob/master/src/uniwill_keyboard.h) 的 `uw_set_charging_profile` 实现核对。

验证：`.\build.ps1 -Configuration Release`；`.\tests\run.ps1`（模拟 EC，不访问硬件）。

## v1.5：首次备份与恢复 / First-run backup and restore

首次运行 v1.5 时，程序将成功读取的 EC 原始上限和下限保存至
`%USERPROFILE%\MechrevoBatteryManager\Default.json`（例如 C 用户为
`C:\Users\C\MechrevoBatteryManager\Default.json`），已有备份不会被覆盖。
备份失败会阻止应用配置和安装服务；文件缺失或损坏时不能恢复。
这是首次读取时的状态，不一定是出厂值：旧版本已经改过 EC 时，备份会记录修改后的值。
“恢复默认”会写回备份值并验证，随后保存 0/0 停用配置、关闭开机应用；
v1.6 起所有机型均不写入下限寄存器。勾选开机应用后，还需要点击“安装服务”。
服务执行一次后自动停止释放内存。

On first launch, v1.5 saves the raw EC upper/lower bytes to
`%USERPROFILE%\MechrevoBatteryManager\Default.json` without overwriting an existing backup.
Backup failure blocks applying settings and installing the service. A missing or invalid
backup blocks restoration. The snapshot represents the first reading, not necessarily
factory defaults if an earlier version already changed the EC.
Restore writes and verifies the saved values, then saves a disabled 0/0 configuration
and disables startup application. Starting with v1.6, the lower register remains untouched on all systems. Select Install service to enable automatic startup application;
the service stops and releases its memory after one attempt.

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
- 只写入上限 `0x07B9` 和充电档位 `0x07A6` bits[5:4]；下限只读。
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
- Writes are limited to upper threshold `0x07B9` and charging profile bits[5:4] of `0x07A6`; the lower register is read-only.
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
