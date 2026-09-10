# MechrevoBatteryManager

## 中文说明

这是面向机械革命/同方机型的电池充电阈值管理工具，目标框架为 **.NET Framework 4.8**，不需要 .NET 8。

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

使用步骤：

1. 以管理员身份运行 `MechrevoBatteryManager.exe`。
2. 点击 **Read EC**，确认 `0x07B9/0x07D0` 读数正常。
3. 设置阈值，应用并保存。
4. 点击 **Install service**。

## English

This tool manages battery charge and recharge thresholds on supported Mechrevo/Tongfang machines. It targets **.NET Framework 4.8** and does not require .NET 8.

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

Usage:

1. Run `MechrevoBatteryManager.exe` as Administrator.
2. Click **Read EC** and verify `0x07B9/0x07D0`.
3. Set thresholds, enable startup application, and save.
4. Click **Install service**.

To fully uninstall the service after the original GUI or source files are gone, run `Uninstall-MechrevoBatteryManager.cmd` as Administrator from the fixed service directory.
