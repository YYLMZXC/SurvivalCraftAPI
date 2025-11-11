# SurvivalCraft-API 生存战争iOS插件版

## 项目简介
SurvivalCraft-API 是生存战争游戏的iOS平台插件开发框架，提供了一套完整的API接口，使开发者能够为iOS版本的生存战争游戏创建和开发插件。

## 系统要求

### Windows 开发机
- Visual Studio 2022
- Xamarin.iOS 开发组件
- .NET 开发环境

### Mac 构建机
- macOS 11.0 或更高版本
- Xcode 13.0 或更高版本
- Apple Developer 账号

## 环境配置

### 1. 资源文件准备
将 Windows 平台的资源文件打包：

```bash
# 将 Survivalcraft.Windows\Content 目录打包为 zip 文件
```

### 2. 项目准备

1. 使用 Visual Studio 2022 打开解决方案文件：
   ```
   SurvivalcraftApi.sln
   ```

2. 生成 Windows 平台调试版本（重要步骤）：
   - 设置 `Survivalcraft.Windows` 为启动项目
   - 选择 Debug 配置和 x86/x64 平台
   - 构建解决方案
   
   *注意：这一步骤是必要的，跳过可能导致 iOS 版本资源文件丢失，出现白屏问题。*

### 3. macOS 环境配置

#### 项目创建与Bundle Identifier设置

1. 在Xcode中创建项目时，需要确保Bundle Identifier设置与Info.plist中的CFBundleIdentifier保持一致
2. 推荐的Bundle Identifier格式：`com.survivalcraft.api.y251026`
3. 请参考以下截图进行配置：

![Bundle Identifier](docs/bundle.png)

#### 证书和描述文件管理

1. 查看当前可用的签名证书：
   ```bash
   security find-identity -p codesigning
   ```

2. 查看本地描述文件：
   ```bash
   ls -lh /Users/[用户名]/Library/MobileDevice/Provisioning\ Profiles/
   ```

3. 查看 Xcode 登录账号缓存：
   ```bash
   ls -lh /Users/[用户名]/Library/Preferences/com.apple.dt.Xcode.plist
   ls -lh /Users/[用户名]/Library/Developer/Xcode/UserData/
   ```

#### 清理旧账号信息（如需切换开发者账号）

1. 删除旧账号的证书（根据 SHA1 值）：
   ```bash
   security delete-identity -Z <旧证书SHA1>
   ```

2. 删除旧描述文件：
   ```bash
   rm -rf /Users/[用户名]/Library/MobileDevice/Provisioning\ Profiles/*
   ```

3. 清除 Xcode 登录信息：
   ```bash
   rm -f /Users/[用户名]/Library/Preferences/com.apple.dt.Xcode.plist
   rm -rf /Users/[用户名]/Library/Developer/Xcode/UserData/Provisioning\ Profiles
   rm -rf /Users/[用户名]/Library/Developer/Xcode/UserData/XcodeCloud
   ```

#### 解决 Visual Studio 与 Xcode 描述文件路径不一致问题

新版本的 Xcode 更改了描述文件的存储位置，为了确保 Visual Studio 能够正确找到描述文件，我们需要创建符号链接：

```bash
# 删除旧文件夹（如果存在）
rm -rf ~/Library/MobileDevice/Provisioning\ Profiles

# 创建符号链接，将旧位置链接到新位置
ln -s ~/Library/Developer/Xcode/UserData/Provisioning\ Profiles ~/Library/MobileDevice/Provisioning\ Profiles

# 验证链接是否创建成功
ls -l ~/Library/MobileDevice/
```

## iOS 项目构建

### 1. 连接 Mac 构建机
在 Visual Studio 中配置 Mac 远程构建：
- 导航至 `工具 > iOS > 配对到 Mac`
- 输入 Mac 的 IP 地址、用户名和密码
- 等待连接成功

### 2. 构建 iOS 项目

1. 设置 `SurvivalCraft.IOS` 为启动项目
2. 选择 Debug 配置和 iPhone Simulator 或 iOS Device 平台
3. 点击构建按钮或按 F5 运行

## 常见问题

### 1. 构建失败，提示缺少描述文件
- 确保已在 Apple Developer Portal 创建了正确的 App ID
- 确保已在 Xcode 中下载了最新的描述文件
- 检查符号链接是否正确创建

### 2. 运行后白屏
- 确保已先生成 Windows 平台版本
- 检查资源文件是否正确打包和加载

### 3. 证书相关错误
- 确认开发者账号有效且未过期
- 检查证书是否已正确安装在 Mac 上
- 尝试清理并重新下载证书和描述文件

## 插件开发指南

要开发 iOS 平台的插件，请参考以下步骤：

1. 在解决方案中创建新的 Class Library 项目
2. 引用必要的 API 程序集
3. 实现插件接口和功能
4. 将编译后的插件放入指定目录

详细的插件开发文档请参考主项目的相关说明。

## 许可证

[根据项目实际许可证填写]

## 联系方式

如有任何问题或建议，请通过以下方式联系项目维护者：

- [项目邮箱或联系方式]
- [项目GitHub Issues页面]