# 🌟 童伴守护 (ChildGuard / 守护星) v__VERSION__ 发行商与签名增强发布

> 100% 纯本地、无商业广告、零隐私上云的开源公益级 Windows 青少年数字健康与终端监护系统。

---

### 🚀 v__VERSION__ 关键修复与兼容性加固

- **官方发行商标识确立为【海文娜AI】**：
  - 安装程序与所有二进制可执行文件注入正式公司/作者元数据 `海文娜AI`；
  - 安装包内置【海文娜AI】SHA256 Authenticode 数字签名；
  - 随包附带 `HaiwenNa-CodeSigning.cer` 及 `一键安装海文娜AI受信任证书.bat`，导入测试机后 Windows 彻底消除“未知发布者”及红色安全拦截。
- **彻底解决 Windows 10 (19041) 启动闪退问题**：
  - 移除非托管构建机 System32 DLL 污染；
  - 移除 WinRT 强绑定，采用原生 Win32/托管高兼容实现；
  - 所有工程 TargetFramework 统一为标准 `net9.0-windows`。

---

### 📦 下载推荐 (Downloads)

- **Windows 官方安装包（推荐广大用户）**：ChildGuard-v__VERSION__-Setup.exe
  - 标准 Windows 安装向导，自动创建桌面与开始菜单快捷方式，支持 Windows 控制面板一键卸载。
  - 内置独立完整运行时，无需预先安装任何 .NET 环境。

- **绿色便携版（免安装）**：ChildGuard-v__VERSION__-win-x64-Portable.zip
  - 解压即用，适合喜欢便携绿色的用户或放入 U 盘使用。

---

### 🔐 校验和信息 (SHA256)

- 见下方构建生成清单