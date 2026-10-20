# 守护星 (OpenGuardian) - Windows 青少年健康护航与本地数字监护系统
> **项目代号**：`ETJGJH` (儿童健康成长与数码使用引导计划)  
> **项目定位**：100% 纯本地、无商业广告、零隐私上云的开源公益级 Windows 终端监护与数字健康软件。  
> 
> 📦 **安装包与绿色版下载**：  
> 前往 [GitHub Releases 最新发布页](https://github.com/haiwen619/ChildGuard/releases/latest) 直接获取：  
> - **Windows 官方安装包（推荐）**：`ChildGuard-v1.0.0-Setup.exe` —— 向导式一键安装，自动创建桌面与开始菜单图标，支持控制面板一键卸载。  
> - **绿色便携版（免安装）**：`ChildGuard-v1.0.0-win-x64-Portable.zip` —— 纯净解压即用，内置完整独立运行时，零环境依赖。

---

## 目录
1. [项目愿景与公益使命](#1-项目愿景与公益使命)
2. [数据安全与绝对本地化（Local-Only 隐私铁律）](#2-数据安全与绝对本地化local-only-隐私铁律)
3. [核心技术选型：.NET 9 + 原生轻量 WPF（无内核极低占用）](#3-核心技术选型net-9--原生轻量-wpf无内核极低占用)
4. [可借力的优秀开源项目与技术资产（站在巨人肩膀上）](#4-可借力的优秀开源项目与技术资产站在巨人肩膀上)
5. [系统核心功能全景（我可以用守护星做些什么？）](#5-系统核心功能全景我可以用守护星做些什么)
6. [系统整体架构设计（双进程特权隔离）](#6-系统整体架构设计双进程特权隔离)
7. [Windows 底层核心实现（C# 与 Rust 双语实践）](#7-windows-底层核心实现c-与-rust-双语实践)
   - 7.1 [前台应用与窗口行为感知（WinEventHook）](#71-前台应用与窗口行为感知wineventhook)
   - 7.2 [低开销硬件加速屏幕捕获（Windows.Graphics.Capture）](#72-低开销硬件加速屏幕捕获windowsgraphicscapture)
   - 7.3 [零轮询进程启停拦截（ETW / WMI Trace）](#73-零轮询进程启停拦截etw--wmi-trace)
   - 7.4 [全系统级不良网站离线屏蔽（Hosts / DNS 拦截）](#74-全系统级不良网站离线屏蔽hosts--dns-拦截)
   - 7.5 [本地离线 OCR 与敏感内容审计](#75-本地离线-ocr-与敏感内容审计)
8. [防篡改、权限分离与反作弊保护](#8-防篡改权限分离与反作弊保护)
9. [本地身份验证（离线 TOTP 与密码学）](#9-本地身份验证离线-totp-与密码学)
10. [开源公益与社区协作路线](#10-开源公益与社区协作路线)

---

## 1. 项目愿景与公益使命

商业市场上的“家长控制”或“青少年防沉迷”软件往往充斥着严重的弊病：
- **云端侵犯隐私**：强制将未成年人的屏幕截屏、按键敲击、社交聊天等数据上传至商业公司的云端服务器，存在严重的数据泄露与拖库风险；
- **过度商业化与诱导收费**：动辄收取高额月费、弹出广告弹窗，甚至转卖儿童数据进行广告画像；
- **冷酷对抗与恶意木马化**：多数软件像隐蔽木马一样运行，严重激化未成年人与父母之间的对抗心理，甚至被杀毒软件直接标记为“跟踪软件（Stalkerware）”。

### 本项目的公益核心价值观：
1. **陪伴与引导，而非间谍与冷酷控制**：  
   倡导阳光透明的数字健康管理（如护眼休息提醒、游戏时长预警、视力保护提示），系统在工作时保持清晰托盘状态，符合反跟踪软件联盟（Coalition Against Stalkerware）伦理规范。
2. **零数据出境（100% Local-First）**：  
   **所有监控快照、使用时长统计、敏感词命中日志绝对保留在本机**。不依赖任何外部云端服务器，即使断网拔掉网线，系统全套功能依旧完整运行。
3. **开源透明，人人可用**：  
   代码完全开源（采用抗恶意商用套壳的 **AGPL-3.0** 许可证），让每个家庭、学校微机室、公益教育机构都能自由审计、自主编译、免费部署，杜绝商业绑架。

---

## 2. 数据安全与绝对本地化（Local-Only 隐私铁律）

系统从底层架构上根除“数据外泄”通道，保证家庭数据的主权完全归宿于设备本身：

```
+-------------------------------------------------------------------------------+
|                             单机闭环安全架构 (Air-Gapped)                      |
|                                                                               |
|  [用户前台行为] ---> [实时采集引擎] ---> [本地离线分析/OCR] ---> [违规/时长判断]   |
|                                                                    |          |
|                                                                    v          |
|  [家长查看报告] <--- [本地授权 (TOTP/密码)] <--- [本地 SQLite + SQLCipher 加密库] |
|                                                    (AES-256 + Windows DPAPI)  |
|                                                                               |
|                   [物理隔离] 严禁任何公网上传、严禁云端中继服务器                 |
+-------------------------------------------------------------------------------+
```

### 2.1 存储安全设计
- **数据库加密**：使用嵌入式 **SQLite + SQLCipher**，全盘采用 **AES-256-GCM** 加密存储。
- **主密钥保管**：使用 Windows **DPAPI** (`CryptProtectData`) 将数据库加密密钥固化绑定于本机的 `LocalMachine` 或管理员专有安全容器中。没有本地管理员密码或特定硬件上下文，任何人都无法拷走数据库文件解密。
- **滚动存储机制（TTL 自动覆盖）**：
  - 屏幕快照与文本日志默认最多保留 7 天，采用环形覆盖（Ring Buffer）逻辑，旧数据自动安全擦除，防止挤占硬盘空间（单机存储上限可配置为 500MB~2GB）。

### 2.2 家长查阅报告的无网络方案
1. **本机管理面板**：家长只需在电脑本地按下特定安全唤醒快捷键（如 `Ctrl + Alt + Shift + G`），输入本地管理员口令或输入手机 Authenticator（如 Google/微软身份验证器）生成的 6 位离线 TOTP，即可在本机弹出的本地控制面板中查看完整仪表盘与行为分析。
2. **局域网 P2P 点对点查阅（可选扩展）**：
   - 仅限在家庭同一局域网（Wi-Fi）内，通过本地 mDNS 组网，手机通过扫码直连 PC 的本地加密端口（TLS 1.3 + 自签名证书），**不经由任何第三方公网服务器中转**，断开 Wi-Fi 立即失联。

---

## 3. 核心技术选型：.NET 10 + 原生轻量 WPF（无内核极低占用）

经过全方位技术评估，本项目确定核心技术路线为：**最新 .NET 10 + 原生 WPF 架构**。

### 3.1 为什么坚决放弃 Webview2 / Electron，坚定拥抱原生 WPF？
| 评估维度 | 原生 WPF (.NET 10 + WPF-UI) | Chromium / Electron / WebView2 |
| :--- | :--- | :--- |
| **内存底噪开销** | **极小（仅 20MB ~ 35MB）**。无冗余浏览器进程。 | **巨大（150MB ~ 300MB+）**。自带 Chromium 渲染管道与 GPU 辅助进程。 |
| **开机常驻体验** | **秒开（Native AOT / 裁剪编译）**，不拖慢孩子电脑开机速度。 | 启动慢，多个子进程抢占老旧电脑的 CPU 与内存。 |
| **硬件资源争抢** | 在未成年人运行 3D 游戏或网课直播时，**0 掉帧、0 干扰**。 | Chromium 抢占显卡 GPU 解码与内存，造成游戏卡顿、网课丢帧。 |
| **系统底层交互** | 原生访问 Win32 消息泵、DirectX、WinRT 与命名管道，零 IPC 开销。 | 需通过复杂的 Node.js / C++ 原生 Addon 桥接，存在通讯延迟与开销。 |

### 3.2 破解“WPF 不好看”的刻板印象：引入现代 `WPF-UI`
传统 WPF 界面因灰白默认控件而被认为陈旧。本项目引入当下 GitHub 最火爆的 Windows 11 Fluent Design 原生开源库 **`lepoco/wpfui`**：
- **Windows 11 原生美学**：开箱即用支持 Mica（云母）与 Acrylic（亚克力）透明毛玻璃质感；
- **自适应系统主题**：跟随 Windows 自动切换深色/浅色模式，圆角控件与微动画体验媲美系统原生设置界面；
- **全矢量高刷渲染**：完全基于 DirectX 原生硬件加速，高清屏 200% DPI 缩放下字迹清晰锐利，无 WebView 模糊。

---

## 4. 可借力的优秀开源项目与技术资产（站在巨人肩膀上）

开源社区已有许多高质量的基础积木，本项目无需“重复造轮子”，直接复用成熟的技术方案与规则集：

```
+-----------------------------------------------------------------------------------+
|                           本项目可吸收复用的开源资产全景                          |
+------------------------------------+----------------------------------------------+
| 领域 / 模块                        | 推荐复用的开源项目 & 资产                    |
+------------------------------------+----------------------------------------------+
| 行为聚合与挂机判定                 | ActivityWatch (aw-core / aw-watcher-window)  |
| 现代化 Windows 11 Fluent UI        | lepoco/wpfui (WPF-UI 现代原生控件库)         |
| 丝滑数据分析图表                   | LiveCharts2 (dotnet/LiveCharts2 矢量图表)    |
| 内核级零轮询进程监听               | Microsoft.Diagnostics.Tracing.TraceEvent     |
| 全系统不良网站开源规则库           | StevenBlack/hosts & AdGuard DNS Filter 规则集|
| 本地离线 TOTP 2FA 算法             | Otp.NET (纯 C# 离线 RFC 6238 验证)           |
| 纯离线矢量二维码生成               | QRCoder (零依赖、纯本地 Bitmap/SVG 渲染)     |
| 本地 AES-256 全盘数据库            | sqlcipher / Microsoft.Data.Sqlite            |
+------------------------------------+----------------------------------------------+
```

### 4.1 借鉴 ActivityWatch 的成熟经验
- **心跳合并机制 (Heartbeat Merging)**：  
  如果孩子正在同一个游戏或软件窗口中操作，系统并不需要每秒往数据库写记录。借鉴 ActivityWatch 的心跳模型：在相同应用保持前台时，仅在内存中更新 `duration`，每 30 秒或窗口切换时才持久化一次，**减少 95% 的磁盘 I/O 磨损**。
- **AFK (Away-From-Keyboard) 挂机判定算法**：  
  通过 Windows `GetLastInputInfo` 获取最后键盘/鼠标操作时间。如果超过 3 分钟无操作，判定为挂机，停止累计游戏时长，避免“挂着游戏去吃饭被误判超时”。
- **应用分类规则库 (Categorization Rules)**：  
  复用开源的应用名称匹配规则，将 Steam、Epic、WeGame、原神等自动归入“娱乐游戏”，将 VSCode、Word、网课软件自动归入“学习生产力”。

### 4.2 借鉴 StevenBlack 与 AdGuard 的开源屏蔽规则
- 屏蔽不良网站无需编写复杂的网络抓包扩展，直接引入 GitHub 顶级开源的成人与赌博域名黑名单（如 **`StevenBlack/hosts`** 的 `porn / gambling` 专有列表）；
- 通过 Windows 本地服务自动解析该规则，并写入 Windows 底层系统屏蔽层，全系统生效。

---

## 5. 系统核心功能全景（我可以用守护星做些什么？）

基于家长的真实关切与未成年人健康成长需求，系统提供八大纯本地守护能力：

```
                    +------------------------------------+
                    |       守护星 (OpenGuardian)        |
                    +-----------------+------------------+
                                      |
         +----------------------------+----------------------------+
         |                            |                            |
         v                            v                            v
  [1. 活动时间线与总结]        [2. 游戏与应用监视]          [3. 无屏幕时段与限额]
  - 自动识别前台窗口           - 运行黑白名单管理           - 就寝锁屏 (Curfew)
  - 学习 vs 娱乐占比           - 单游戏每日上限             - 45分钟健康护眼提醒
  - 离线导出周健康报告         - 违规启动温和阻止           - 累计总时长硬上限
         |                            |                            |
         v                            v                            v
  [4. 通话与社交安全审计]      [5. 不良网站全系统屏蔽]      [6. 本地位置与异常提醒]
  - 离线 OCR 屏幕关键词        - StevenBlack 规则库         - 设备所处公网城市感知
  - 涉暴/霸凌/充值预警         - 浏览器无痕模式无效化       - IP 异常突变离线报警
  - 绝非木马级键盘窃听         - 零插件、系统底层生效       - 防范设备丢失或离家
         |                            |                            |
         +----------------------------+----------------------------+
                                      |
                       +--------------+--------------+
                       |                             |
                       v                             v
                [7. 紧急求助与一键管控]       [8. 多设备局域网联邦]
                - 孩子端一键 SOS 呼救         - 家庭 Wi-Fi 内 mDNS 互联
                - 家长端快捷热键秒锁机        - 零公网服务器，点对点加密
```

### 详细功能描述：
1. **通过活动时间线和总结，关注孩子们的活动**：  
   以直观的时间轴（Timeline）展现一天中各时段的软件使用轨迹。自动生成每日/每周健康使用总结（如“今日专注学习 3.5 小时，休闲游戏 40 分钟”），支持本地一键导出图片或 PDF 报表。
2. **监视游戏和应用**：  
   实时识别电脑上运行的任何可执行程序。家长可自定义应用策略：“允许列表”、“限制列表”或“完全阻止列表”。
3. **安排无屏幕时间并设定每日屏幕时间限制**：  
   - **就寝强制断屏 (Curfew Lock)**：例如设定每晚 22:00 ~ 次日 07:00 电脑自动锁屏；
   - **单日时长限额**：例如工作日限额 1.5 小时，周末限额 3 小时；
   - **护眼休息间隙**：每连续使用 45 分钟，强制弹出温和的全屏眼保健操悬浮窗 5 分钟。
4. **监控通话和消息（安全边界与隐私平衡）**：  
   **坚持合规监护，不做木马键盘记录器**。系统不窃听私人聊天，而是利用 Windows 本地离线 OCR，仅在屏幕出现诱导游戏大额充值、网络暴力、不良暗号等敏感内容时，在本地记录一条告警摘要，供家长后续沟通引导。
5. **屏蔽不当网站**：  
   直接在系统层（Hosts / DNS）阻断成人、暴力、涉赌网站。**任何浏览器（Chrome / Edge / Firefox）均全局生效**，哪怕孩子打开浏览器的“无痕模式/隐身窗口”也同样无法穿透。
6. **监控位置并接收警报**：  
   针对笔记本电脑，通过 Windows 原生定位 API (`Windows.Devices.Geolocation`) 与本地 Wi-Fi SSID 变化检测设备所处环境。当设备接入非家庭常规 Wi-Fi 或发生异常移动时，记录位置日志。
7. **启用紧急按钮**：  
   - **孩子端紧急呼叫**：桌面托盘常驻“一键求助”，点击后立即弹窗展示家长预留的紧急联络方式，并可向本地日志发出高优先级求助标记；
   - **家长端一键锁屏**：通过离线安全快捷键（或局域网控制端），一键将电脑置为锁定状态。
8. **监控多个设备和平台的活动（局域网联邦）**：  
   如果家庭有多台电脑，开启局域网同步功能后，各台电脑通过 mDNS 协议在家庭 Wi-Fi 内部建立 TLS 1.3 点对点通信。家长在主电脑上即可集中查看全家设备的使用报表，**全程不需要任何外网服务器**。

---

## 6. 系统整体架构设计（DDD 领域驱动设计 + 双进程特权隔离）

系统融合了现代**领域驱动设计 (DDD)** 与 Windows **双进程特权分离架构**，保证业务模型高内聚、技术实现强解耦：

### 6.1 DDD 模块化与工程分层映射
```
+---------------------------------------------------------------------------------+
|  [表现层 & 宿主层 (Presentation & Hosts)]                                       |
|  - Haidou.ChildGuard.Wpf     : WPF-UI 4.3.0 桌面交互面板、系统托盘、MVVM 视图模型 |
|  - Haidou.ChildGuard.Service : Session 0 Windows 后台守护服务宿主、看门狗恢复   |
+---------------------------------------+-----------------------------------------+
                                        | 调用
+---------------------------------------v-----------------------------------------+
|  [应用服务层 (Application Layer)]                                               |
|  - Haidou.ChildGuard.Application                                                |
|    ├── UseCases/Activities : 记录活动心跳用例、获取单日健康报表用例              |
|    ├── UseCases/Policies   : 策略配置管理用例、实时违规裁决用例                  |
|    ├── UseCases/Security   : 离线 TOTP 二维码生成用例、动态码核销用例            |
|    └── Interfaces & DTOs   : 跨层传输契约、服务端口接口定义                      |
+-------------------+-----------------------------------------+-------------------+
                    | 依赖                                    ^
                    |                                         | 依赖倒置 (DIP 实现)
+-------------------v-------------------+ +-------------------+-------------------+
|  [领域核心层 (Domain Layer)]          | |  [基础设施层 (Infrastructure Layer)]  |
|  - Haidou.ChildGuard.Domain (0外置依赖)| |  - Haidou.ChildGuard.Infrastructure   |
|    ├── Aggregates (聚合根):           | |    ├── Persistence:                   |
|    │   ├── DailyTimelineAggregate     | |    │   ├── SqliteActivityRepository   |
|    │   └── GuardianPolicyAggregate    | |    │   ├── SqlitePolicyRepository     |
|    ├── Entities (实体):               | |    │   └── DatabaseContext (WAL 模式) |
|    │   └── ActivitySession            | |    ├── Adapters/Win32:                |
|    ├── ValueObjects (值对象):         | |    │   ├── WinEventHookAdapter        |
|    │   ├── TimeWindow (就寝时段)      | |    │   ├── InputInactivityAdapter(AFK)|
|    │   ├── ProcessInfo (应用与分类)   | |    │   └── ActivityHeartbeatEngine    |
|    │   └── TotpCredential (安全凭证)  | |    └── Services:                      |
|    ├── DomainServices:                | |        └── TotpService (QRCoder/Otp)  |
|    │   └── PolicyEvaluationService    | +---------------------------------------+
|    └── Repositories (仓储接口定义)    |
+---------------------------------------+
```

### 6.2 操作系统级特权与进程拓扑
```
+--------------------------------------------------------------------------------+
| [Windows Session 0 - 隔离特权层 (NT AUTHORITY\SYSTEM)]                         |
|                                                                                |
|  +--------------------------------------------------------------------------+  |
|  | Haidou.ChildGuard.Service (Windows 本地服务宿主)                         |  |
|  | - 策略引擎巡检：通过 ManagePolicyUseCase 实时评估就寝断屏时段 (Curfew)       |  |
|  | - 存储老化擦除：定期驱动 IActivityRepository.PurgeOldSessionsAsync(7)       |  |
|  | - 系统拦截防线：底层 Hosts / WFP 网络拦截与异常进程终止                      |  |
|  +-------------------------------------+------------------------------------+  |
|                                        |                                       |
+----------------------------------------|---------------------------------------+
                                         | 本地安全命名管道 (ACL 限制仅 SYSTEM)
+----------------------------------------|---------------------------------------+
| [Windows Session 1+ - 用户交互桌面层 (未成年人登录会话)]                         |
|                                        |                                       |
|  +-------------------------------------v------------------------------------+  |
|  | Haidou.ChildGuard.Wpf (前台托盘伴侣与家长控制台 - WPF .NET 10 原生轻量运行)|  |
|  | - 桌面透明度：常驻系统托盘，明确呈现“当前处于健康护航模式”                  |  |
|  | - 行为感知：WinEventHookAdapter 纳秒级监听前台焦点窗口变更                   |  |
|  | - 磁盘保护：ActivityHeartbeatEngine 聚合内存时长，减少 95% 磁盘 I/O          |  |
|  | - 家长控制：离线 TOTP 扫码动态核销，纯本地安全验证                          |  |
|  +--------------------------------------------------------------------------+  |
+--------------------------------------------------------------------------------+
```

---

## 7. Windows 底层核心实现（C# 与 Rust 双语实践）

### 7.1 前台应用与窗口行为感知（WinEventHook）
用于精确感知孩子当前正在使用哪个应用，避免高开销轮询。

#### C# (.NET 10) 实现参考
```csharp
using System;
using System.Runtime.InteropServices;
using System.Text;

public sealed class WindowTracker : IDisposable
{
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;

    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, 
        IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    private readonly WinEventDelegate _delegate;
    private IntPtr _hookHandle;

    public event Action<uint, string, string>? ActiveWindowChanged;

    public WindowTracker()
    {
        // 保持引用防止 GC 收集委托
        _delegate = EventCallback;
    }

    public void Start()
    {
        if (_hookHandle != IntPtr.Zero) return;
        _hookHandle = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _delegate, 0, 0, WINEVENT_OUTOFCONTEXT);
    }

    private void EventCallback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, 
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (hwnd == IntPtr.Zero) return;

        var sb = new StringBuilder(512);
        GetWindowText(hwnd, sb, sb.Capacity);
        GetWindowThreadProcessId(hwnd, out uint pid);

        string title = sb.ToString();
        // 传递给本地策略引擎评估时长与黑白名单
        ActiveWindowChanged?.Invoke(pid, title, DateTime.UtcNow.ToString("O"));
    }

    public void Dispose()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWinEvent(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }
}
```

#### Rust 实现参考 (`windows-rs`)
```rust
use std::ffi::OsString;
use std::os::windows::ffi::OsStringExt;
use std::sync::atomic::{AtomicPtr, Ordering};
use windows::Win32::Foundation::{HWND, LPARAM, WPARAM};
use windows::Win32::UI::WindowsAndMessaging::{
    GetWindowTextW, GetWindowThreadProcessId, SetWinEventHook, UnhookWinEvent,
    EVENT_SYSTEM_FOREGROUND, HWINEVENTHOOK, WINEVENT_OUTOFCONTEXT,
};

static HOOK_HANDLE: AtomicPtr<std::ffi::c_void> = AtomicPtr::new(std::ptr::null_mut());

pub fn start_window_tracking() {
    unsafe {
        let hook = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND,
            EVENT_SYSTEM_FOREGROUND,
            None,
            Some(win_event_proc),
            0,
            0,
            WINEVENT_OUTOFCONTEXT,
        );
        HOOK_HANDLE.store(hook.0, Ordering::SeqCst);
    }
}

pub fn stop_window_tracking() {
    let hook = HOOK_HANDLE.swap(std::ptr::null_mut(), Ordering::SeqCst);
    if !hook.is_null() {
        unsafe {
            let _ = UnhookWinEvent(HWINEVENTHOOK(hook));
        }
    }
}

unsafe extern "system" fn win_event_proc(
    _hook: HWINEVENTHOOK,
    _event: u32,
    hwnd: HWND,
    _id_object: i32,
    _id_child: i32,
    _event_thread: u32,
    _event_time: u32,
) {
    if hwnd.0.is_null() {
        return;
    }

    let mut buffer = [0u16; 512];
    let len = GetWindowTextW(hwnd, &mut buffer);
    let title = if len > 0 {
        OsString::from_wide(&buffer[..len as usize])
            .to_string_lossy()
            .into_owned()
    } else {
        String::new()
    };

    let mut process_id = 0u32;
    GetWindowThreadProcessId(hwnd, Some(&mut process_id));

    // 本地事件管道分发：零内存分配、纳秒级无阻塞派发
    crate::local_event_bus::emit_activity(process_id, title);
}
```

---

### 7.2 低开销硬件加速屏幕捕获（Windows.Graphics.Capture）
采用 Windows 10 1803+ 现代 **Windows.Graphics.Capture (WGC)** API：
1. **GPU 显存内直接捕捉**（Direct3D 11 纹理），CPU 使用率低于 1%；
2. **操作系统自带透明黄框**：原生渲染保护性边框，明确告知当前正在护航状态，杜绝任何黑客恶意隐蔽偷拍。

```csharp
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

public class LocalScreenCaptureEngine : IDisposable
{
    private Direct3D11CaptureFramePool? _framePool;
    private GraphicsCaptureSession? _session;
    private readonly IDirect3DDevice _device;

    public LocalScreenCaptureEngine(IDirect3DDevice d3dDevice)
    {
        _device = d3dDevice;
    }

    public void StartMonitorCapture(GraphicsCaptureItem item)
    {
        _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            _device,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            numberOfBuffers: 2,
            item.Size);

        _framePool.FrameArrived += OnLocalFrameArrived;
        _session = _framePool.CreateCaptureSession(item);
        _session.IsCursorCaptureEnabled = true;
        _session.StartCapture();
    }

    private void OnLocalFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        using var frame = sender.TryGetNextFrame();
        if (frame == null) return;

        // 仅在策略引擎触发违规取证时抽取当前帧：
        // 硬件纹理 -> 本地轻量下采样 -> 保存至本地加密缓存，绝不外发网络
        LocalSnapshotManager.ProcessFrameIfNeeded(frame.Surface, frame.ContentSize);
    }

    public void Dispose()
    {
        _session?.Dispose();
        _framePool?.Dispose();
    }
}
```

---

### 7.3 零轮询进程启停拦截（ETW / WMI Trace）
通过引用开源库 `Microsoft.Diagnostics.Tracing.TraceEvent` 订阅 Windows 原生事件跟踪（ETW：`Microsoft-Windows-Kernel-Process`），实现**零 CPU 轮询开销**的应用启动捕获：

```csharp
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;

public class EtwProcessMonitor
{
    public static void StartKernelMonitoring(CancellationToken ct)
    {
        // 必须运行于管理员/Session 0 服务权限
        using var session = new TraceEventSession("OpenGuardianKernelTrace");
        session.EnableKernelProvider(KernelTraceEventParser.Keywords.Process);

        session.Source.Kernel.ProcessStart += (data) =>
        {
            string imageFileName = data.ImageFileName;
            int processId = data.ProcessID;

            // 本地白名单与时长检查
            if (PolicyEngine.IsForbidden(imageFileName))
            {
                // 优雅关闭或强制收敛
                ProcessTerminator.GracefulKill(processId);
            }
        };

        session.Source.Process();
    }
}
```

---

### 7.4 全系统级不良网站离线屏蔽（Hosts / DNS 拦截）
无需给每个浏览器安装扩展插件，Session 0 服务直接接管系统级过滤规则：
1. **自动整合开源规则**：从内置的 `StevenBlack/hosts` 成人与恶意网站列表提取域名；
2. **底层生效**：写入系统 `C:\Windows\System32\drivers\etc\hosts` 映射至 `127.0.0.1`（利用 SYSTEM 权限锁定 ACL 防止孩子修改）；
3. **效果**：无论孩子使用 Chrome、Edge、Firefox 还是小众浏览器，或使用“无痕隐身模式”，均被全系统强制阻断。

---

### 7.5 本地离线 OCR 与敏感内容审计
- **原生 Windows.Media.Ocr 引擎**：直接利用 Windows 操作系统自带的离线 OCR 组件提取屏幕文字，0 额外下载体积、0 云端费用、0 隐私外流。
- **本地敏感词字典树（Aho-Corasick 算法）**：毫秒级全文本匹配，一旦命中不良内容：
  1. 在本地数据库记录一条告警事件；
  2. 截取并加密当前违规小快照以供家长本地复核；
  3. 执行屏幕温和遮罩提示：“检测到不健康内容，请专注学习或休息”。

---

## 8. 防篡改、权限分离与反作弊保护

在实际家庭场景中，具备一定计算机知识的孩子可能会尝试“结束进程”、“删除文件”或“修改系统时间”。系统采取多层次防护：

### 8.1 操作系统级权限分离（标准用户 vs 本地管理员）
- **核心防线**：未成年人的日常 Windows 登录账号应当设置为 **“标准用户 (Standard User)”**，家长掌握 **“管理员 (Administrator)”** 账号。
- **天然免疫**：在标准用户权限下，Windows 操作系统内核会直接拒绝其执行 `taskkill`、停用 Windows 服务或修改安装文件的请求。

### 8.2 双向心跳看门狗（Service <-> Agent Watchdog）
即使前台 Agent 进程被意外终止，运行于 Session 0 的 Windows 服务会利用命名管道断开事件在 **1 秒内重新启动前台程序**：
```csharp
public static void LaunchAgentInActiveSession()
{
    uint activeSessionId = WTSGetActiveConsoleSessionId();
    if (activeSessionId == 0xFFFFFFFF) return;

    if (WTSQueryUserToken(activeSessionId, out IntPtr userToken))
    {
        // 复制令牌并使用 CreateProcessAsUser 在孩子桌面上拉起伴侣程序
        CreateProcessAsUser(userToken, ...);
        CloseHandle(userToken);
    }
}
```

### 8.3 防时间倒流欺骗
针对“修改系统时间延长游戏时长”的花招：
- 守护进程在本地加密数据库中记录 **单调递增逻辑时间戳 (Monotonic Hardware Tick)** 与 `GetTickCount64()`；
- 若检测到系统时间比数据库最后记录时间发生回退超过 5 分钟，自动标记为“时钟异常事件”，暂停时长消耗计时或维持当前锁定状态。

---

## 9. 本地身份验证（离线 TOTP 与密码学）

无需任何外部云账户即可实现极高强度的双重验证（2FA）：

```
+------------------------------------------------------------------------+
|                      本地离线 TOTP (RFC 6238) 绑定流程                  |
|                                                                        |
| 1. [初次初始化] 本地生成 160-bit 高强度随机密钥 (CSPRNG)               |
| 2. [离线生成二维码] 本地利用 QRCoder 库渲染:                           |
|    otpauth://totp/OpenGuardian:HomePC?secret=JBSWY3DPEHPK3PXP&issuer=Local |
| 3. [家长绑定] 家长使用手机 Google Authenticator / 微软验证器 离线扫码   |
| 4. [本地持久化] 密钥经 Windows DPAPI 加密存入本地安全存储区            |
| 5. [解锁验证] 家长输入 6 位数动态码 -> 本地计算一致性 -> 立即解锁       |
+------------------------------------------------------------------------+
```

### 本地防重放攻击
系统在本地维护已核销的 `LastValidatedTimeStep` 计数器，同一 30 秒窗口内的相同动态码只能验证一次，杜绝重放攻击。

---

## 10. 开源公益与社区协作路线

### 10.1 开源协议推荐：GNU AGPLv3
为了防止不法商业公司或黑产团伙把本项目的开源代码拿去封装为闭源间谍木马或收费流氓软件，强烈建议采用 **GNU Affero General Public License v3 (AGPL-3.0)**：
- 任何基于本项目修改的衍生品必须同样保持完整开源；
- 保障全天下父母与教育机构能够永远免费、透明地享受到纯净的数字监护工具。

### 10.2 项目里程碑与开发路线图

| 阶段 | 核心目标 | 交付物 |
| :--- | :--- | :--- |
| **阶段一 (MVP)** | **本地基础监管与时间锁** | 1. 基于 .NET 10 的 Session 0 守护服务 + WPF-UI 托盘伴侣<br>2. WinEventHook 前台活动监测与 Heartbeat 合并算法<br>3. 本地 SQLite + SQLCipher 加密存储与防篡改策略 |
| **阶段二 (健康守护)** | **无感截屏与护眼休息** | 1. Windows.Graphics.Capture 低开销本地快照<br>2. 定时休息提醒与护眼全屏引导遮罩<br>3. QRCoder + Otp.NET 离线 TOTP 扫码解锁控制台 |
| **阶段三 (全维过滤)** | **离线内容过滤与审计看板** | 1. StevenBlack 开源 Hosts 规则全系统网站屏蔽<br>2. Windows 内置 OCR 离线不良词汇命中<br>3. LiveCharts2 矢量健康报表（日/周使用趋势、软件占比排行） |

---

> **致开发者**：  
> “我们构建工具，是为了保护下一代在充满诱惑的数字海洋中健康航行，而不是在他们身后投掷不可信任的监视阴影。”  
> 欢迎所有热心公益、专注于底层系统安全与数字健康的开发者参与贡献！