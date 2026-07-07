# TrafficLight — Claude Code 系统托盘指示灯

[![.NE](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D4?logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

一个 Windows 系统托盘指示灯，通过颜色圆点实时显示 [Claude Code](https://claude.ai/code) 的运行状态。

| 状态 | 图标 | 含义 |
|------|------|------|
| ⚪ 灰色 | `idle.png` | 空闲 |
| 🔴 红色（闪烁） | `red.png` | 等待确认 |
| 🟡 黄色 | `yellow.png` | 执行中 / 思考中 |
| 🟢 绿色 | `green.png` | 已完成 |

---

## 功能特性

- **实时状态指示** — 通过 HTTP 接口接收状态更新，托盘图标即时切换
- **红色闪烁提醒** — "等待确认" 状态下红色圆点交替闪烁，避免错过
- **60 秒超时保护** — 非空闲状态超过 60 秒无更新自动恢复为空闲
- **双击复位** — 双击托盘图标手动回到空闲状态
- **嵌入图标资源** — PNG 图标编译时嵌入，无需外部文件依赖
- **后台调试日志** — 自动记录到 `%TEMP%\TrafficLight-debug.log`
- **单文件发布** — 发布为单个 `.exe` 文件，即点即用

## 快速开始

### 前置要求

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Windows 操作系统（使用 Windows Forms）

### 构建

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

发布产物位于：
```
bin\Release\net8.0-windows\win-x64\publish\TrafficLight.exe
```

### 运行

直接双击 `TrafficLight.exe` 即可。程序会在系统托盘显示一个灰色圆点。

## 与 Claude Code 集成

本项目通过 **Claude Code 的 Hooks 机制** 自动推送状态更新。将以下配置添加到你的 `settings.json` 中：

```json
{
  "hooks": {
    "UserPromptSubmit": [
      {
        "matcher": "",
        "hooks": [{ "type": "command", "command": "curl", "args": ["-s", "http://localhost:9876/?status=processing"], "async": true }]
      }
    ],
    "PreToolUse": [
      {
        "matcher": "",
        "hooks": [{ "type": "command", "command": "curl", "args": ["-s", "http://localhost:9876/?status=processing"], "async": true }]
      }
    ],
    "PostToolUse": [
      {
        "matcher": "",
        "hooks": [{ "type": "command", "command": "curl", "args": ["-s", "http://localhost:9876/?status=processing"], "async": true }]
      }
    ],
    "PostToolBatch": [
      {
        "matcher": "",
        "hooks": [{ "type": "command", "command": "curl", "args": ["-s", "http://localhost:9876/?status=processing"], "async": true }]
      }
    ],
    "PermissionRequest": [
      {
        "matcher": "",
        "hooks": [{ "type": "command", "command": "curl", "args": ["-s", "http://localhost:9876/?status=confirm"], "async": true }]
      }
    ],
    "TaskCreated": [
      {
        "matcher": "",
        "hooks": [{ "type": "command", "command": "curl", "args": ["-s", "http://localhost:9876/?status=processing"], "async": true }]
      }
    ],
    "SubagentStart": [
      {
        "matcher": "",
        "hooks": [{ "type": "command", "command": "curl", "args": ["-s", "http://localhost:9876/?status=processing"], "async": true }]
      }
    ],
    "TaskCompleted": [
      {
        "matcher": "",
        "hooks": [{ "type": "command", "command": "curl", "args": ["-s", "http://localhost:9876/?status=completed"], "async": true }]
      }
    ],
    "Stop": [
      {
        "matcher": "",
        "hooks": [{ "type": "command", "command": "curl", "args": ["-s", "http://localhost:9876/?status=completed"], "async": true }]
      }
    ],
    "SessionEnd": [
      {
        "matcher": "",
        "hooks": [{ "type": "command", "command": "curl", "args": ["-s", "http://localhost:9876/?status=idle"], "async": true }]
      }
    ]
  }
}
```

### 状态映射说明

| 触发时机 | HTTP 参数 | 图标 |
|----------|-----------|------|
| 用户提交提示 / 开始任务 / 执行工具 | `?status=processing` | 🟡 黄色 |
| 等待用户确认（如工具调用授权） | `?status=confirm` | 🔴 红色（闪烁） |
| 任务完成 | `?status=completed` | 🟢 绿色 |
| 会话结束 | `?status=idle` | ⚪ 灰色 |

## 手动测试

你可以使用 `curl` 手动发送状态来验证指示灯：

```bash
curl "http://localhost:9876/?status=thinking"    # 黄色 - 思考中
curl "http://localhost:9876/?status=processing"  # 黄色 - 执行中
curl "http://localhost:9876/?status=waiting"     # 红色闪烁 - 等待确认
curl "http://localhost:9876/?status=confirm"     # 红色闪烁 - 需确认
curl "http://localhost:9876/?status=completed"   # 绿色 - 已完成
curl "http://localhost:9876/?status=done"        # 绿色 - 已完成
curl "http://localhost:9876/?status=idle"        # 灰色 - 空闲
```

## 自定义图标

将新的 32×32 PNG 文件放入 `images/` 目录，保持文件名一致，重新构建即可：

```
images/
├── idle.png      # 灰色圆点
├── red.png       # 红色圆点
├── yellow.png    # 黄色圆点
└── green.png     # 绿色圆点
```

## 项目结构

```
TrafficLight/
├── Program.cs              # 应用入口 + 核心逻辑（托盘、HTTP、图标切换）
├── TrafficLight.csproj     # .NET 项目文件
├── images/                 # 图标源文件（编译时嵌入）
│   ├── idle.png
│   ├── red.png
│   ├── yellow.png
│   └── green.png
├── settings.json           # Claude Code Hooks 配置（参考）
├── CLAUDE.md               # 项目说明（供 Claude Code 使用）
└── README.md               # 本文件
```

## 工作原理

1. **`TrafficLightContext`** — 继承 `ApplicationContext`，管理应用生命周期和状态
2. **`HttpListener`** — 在 `localhost:9876` 监听 HTTP 请求，解析 `?status=` 参数
3. **`NotifyIcon`** — Windows 系统托盘图标控件
4. **500ms 定时器** — 驱动超时检测和图标刷新（红色闪烁交替），无需跨线程调度
5. **PNG 嵌入** — 图标在编译时作为 `EmbeddedResource` 嵌入，运行时通过 32bpp DIB-in-ICO 格式转换为 `System.Drawing.Icon`，保证透明通道正确

## 调试

日志文件位于 `%TEMP%\TrafficLight-debug.log`，记录每次状态变化和 HTTP 请求：

```
[14:23:15.123] TrafficLight started.
[14:23:15.456] HTTP listener started on port 9876
[14:23:20.789] HTTP request: status='processing'
[14:23:20.789] SetStatus => 'processing'
[14:23:20.790] Icon set to 'Claude Code-执行中'
```

## 许可证

MIT
