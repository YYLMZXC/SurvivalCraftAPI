# SurvivalCraft-API 生存战争插件版

## 介绍

生存战争插件版是基于 Candy Rufus Game 开发的 [生存战争 Survivalcraft](https://kaalus.wordpress.com/) 二次开发的支持加载模组的版本

## 用户下载和使用说明

[点击此处](https://gitee.com/SC-SPM/SurvivalcraftApi/releases/latest) 进入发布页来下载

### Android 安卓系统看这里
> 需要 64 位 ARM 架构 CPU，最低 Android 5.0

1. 从 [发布页](https://gitee.com/SC-SPM/SurvivalcraftApi/releases/latest) 下载前缀为`[Android]`，后缀为`.apk`的安装包
2. 安装后运行
3. 第一次运行可能会跳转到标题为`所有文件访问`的授权界面，请授权此 APP（名称：`生存战争2.4 API插件版1.9`），否则此 APP 无法运行

### iOS、iPadOS 系统看这里
> 需要 64 位 ARM 架构 CPU，最低系统版本 16.0

1. 从 [发布页](https://gitee.com/SC-SPM/SurvivalcraftApi/releases/latest) 下载前缀为`[iOS]`，后缀为`.ipa`的安装包
2. 安装包下载后需要使用[爱思助手](https://www.i4.cn/)进行签名
3. 推荐使用`登录自己的Apple ID`方式获取免费签名，签名后的 ipa 包仅自己可用

> **重要** 
> 由于 iOS、iPadOS 系统不支持 JIT 编译（参阅[此处](https://learn.microsoft.com/zh-cn/previous-versions/xamarin/ios/internals/limitations)），因此<font color="red">任何带`dll`文件的模组都不可用！</font>可等待后续完善的 Javascript 方式运行模组的更新

### Windows 系统看这里
> 需要 x64 架构 CPU，最低 Windows 10 版本 1607，显卡驱动需要支持OpenGL ES 3.2 图形 API（对于兼容补丁，需要支持 Direct3D 9 图形 API）

1. 从 [发布页](https://gitee.com/SC-SPM/SurvivalcraftApi/releases/latest) 下载前缀为`[Windows]`，后缀为`.7z`，名称不带`兼容补丁`的压缩包
2. 使用您喜欢的解压缩软件进行解压
3. 运行<font color="red">解压后</font>的`.exe`文件
4. 第一次启动游戏，系统可能会提示您安装 [.NET 桌面运行时 10.0](https://dotnet.microsoft.com/zh-cn/download/dotnet/10.0)，请按提示完成安装并重启您的电脑
5. 如果启动没有任何反应，可能是因为您的 Windows 系统不完整，请尝试手动安装 [.NET 桌面运行时 10.0](https://dotnet.microsoft.com/zh-cn/download/dotnet/10.0)；如果安装后仍然启动没有任何反应，建议重新安装完整的 Windows 系统

6. 如果弹窗提示`你的显卡驱动不支持当前程序使用的图形API，请尝试更新显卡驱动，或使用兼容补丁。`，如果显卡驱动更新后仍然弹窗，请尝试下载名称中有`兼容补丁`的压缩包，然后解压到之前解压到的目录，运行<font color="red">解压后的新的</font>`.exe`文件  
如果使用兼容补丁后仍然弹窗，建议为您的电脑购买并装上五年内发布的显卡
7. 如果弹窗提示`GLFW 窗口平台无法使用。请安装 Microsoft Visual C++ Redistributable，点击"确定"来打开下载页面。`，请按提示完成下载和安装。或者[点击此处](https://learn.microsoft.com/zh-cn/cpp/windows/latest-supported-vc-redist?view=msvc-170#latest-microsoft-visual-c-redistributable-version)打开下载页面

### Linux 系统看这里
> 需要 x64 架构 CPU，最低系统版本详见 [此处](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md#linux)，显卡驱动需要支持 OpenGL ES 3.2 图形 API

1. 从 [发布页](https://gitee.com/SC-SPM/SurvivalcraftApi/releases/latest) 下载前缀为`[Linux]`，后缀为`.7z`的压缩包，之后使用您喜欢的解压缩软件进行解压
2. 安装以下包：
    * **dotnet-runtime-10.0** .NET 运行时 10.0，安装方法详见 [此处](https://learn.microsoft.com/zh-cn/dotnet/core/install/linux?WT.mc_id=dotnet-35129-website)
    * **libopenal-dev** 一个声音API，对于 Ubuntu 系统可运行`sudo apt-get install libopenal-dev`来安装，其他分发版类似
    * **xsel** 一个剪贴板操作API，对于 Ubuntu 系统可运行`sudo apt-get install xsel`来安装

3. 有两种启动方法：
    * 在第 1 步解压出来的目录运行`dotnet Survivalcraft.dll`
    * 同样在解压出来的目录，先运行`chmod +x Survivalcraft`来添加可执行权限（只需要一次），再双击`Survivalcraft`即可

### 网页版看这里
> 需要支持 SharedArrayBuffer、OffscreenCanvas、Origin Private File System 等现代浏览器特性的浏览器，推荐使用最新版的 Chrome 浏览器。

1. 打开 [https://scapiweb.netlify.app/](https://scapiweb.netlify.app/) 即可游玩

说明：完全不支持模组和运行 Javascript

### 常见问题

* 如果游戏打开后语言不是您希望的语言，请点击左下角第二个图标，即可切换语言
* 模组文件的后缀为`.scmod`，安装位置：
    * Android 系统：`/storage/emulated/0/Survivalcraft2.4_API1.9/Mods`
    * 其他系统：`(解压到的目录)/Mods`
    * 在 Android 系统和 Windows 系统，你能在打开后缀为`.scmod`的文件时选择插件版，即可完成模组的安装（还支持打开`.scworld`、`.scbtex`、`.scskin`、`.scfpack`）
* 按上面说明处理后仍然打不开游戏，或者运行遇到任何错误，请尝试移除所有模组，如果问题依旧，可在 [此处](https://gitee.com/SC-SPM/SurvivalcraftApi/issues) 反馈问题
* 安装模组后打不开游戏，或者运行遇到任何错误，请先向模组作者反馈问题，如有必要再由模组作者向本仓库反馈问题
* 如果 Windows 系统上游戏帧数不低但鼠标调整视角感觉卡顿，关闭系统设置-鼠标设置-增强指针精度，即可解决
* 要取消 Windows 系统上的文件关联，游戏设置-设备兼容和日志-文件关联，禁用即可
* 网页版打不开？请尝试更换更好的网络，如果还是不行，请打开 [https://scapiweb.netlify.app/dashboard.html](https://scapiweb.netlify.app/dashboard.html)，检测你的浏览器是否支持网页版所需的功能。这里推荐使用最新版的 Chrome 浏览器。
* 网页版键盘操作没反应？请将输入法切换成英文模式

## 模组开发者引用

1. 首先复制本存储库根目录的`nuget.config`文件到您的解决方案目录（和`.sln`文件同一层级）

2. 有两种常规方式添加引用包 (nupkg)，请选择您喜欢的方式
   
    * **推荐：** 在解决方案目录运行以下命令：

    ```bat
    dotnet add package SurvivalcraftAPI.Survivalcraft
    ```

    * 或者手动在`.csproj`文件的`<Project>...</Project>`中添加以下行（下面的版本号可能不是最新的）

    ```xml
    <ItemGroup>
      <PackageReference Include="SurvivalcraftAPI.Survivalcraft" Version="1.9.0.0"/>
    </ItemGroup>
    ```

3. 不推荐以上方法之外的引用方式，如果网络实在不通畅无法完成 nupkg 的下载，可从 [发布页](https://gitee.com/SC-SPM/SurvivalcraftApi/releases/latest) 下载前缀为`[Nupkgs]`，后缀为`.7z`的压缩包，将其中的所有`nupkg`文件解压到您喜欢的目录，之后按照 [微软官方教程](https://learn.microsoft.com/zh-cn/nuget/hosting-packages/local-feeds) 手动添加
4. 当然还有更麻烦的引用方式，按照上一步提到的方式或其他方式得到 nupkg 后，将其逐一解压，找到其中的`Engine.dll`、`EntitySystem.dll`、`Survivalcraft.dll`，将它们的路径记录下来，在`.csproj`文件的`<Project>...</Project>`中添加以下行（大部分 IDE 支持在图形界面进行该操作，最终达成相同的效果就好）

```xml
<ItemGroup>
  <Reference Include="Engine" HintPath="（在此填写Engine.dll的文件路径，不要括号）" />
  <Reference Include="EntitySystem" HintPath="（EntitySystem.dll的文件路径，不要括号）" />
  <Reference Include="Survivalcraft" HintPath="（Survivalcraft.dll的文件路径，不要括号）" />
</ItemGroup>
```

## 仓库构建说明

1. 首先使用 Git 克隆此仓库

    ```bat
    git clone https://gitee.com/SC-SPM/SurvivalcraftApi.git
    ```

    > 还没有 Git？[官网下载](https://git-scm.com/downloads)

2. 进入此仓库，使用 [Visual Studio](https://visualstudio.microsoft.com/) 或 [Rider](https://www.jetbrains.com/zh-cn/rider/) 打开`SurvivalcraftApi`目录中的`SurvivalCraftApi.sln`
3. 如果只是在 Windows 系统上进行调试，请右键卸载`Survivalcraft.Android`等所有名称没有 Windows 的 项目，之后在`Survivalcraft.Windows`项目上右键，点击`构建所选项目`即可
4. 如果需要生成 Android 系统上的`APK`安装包，请在`Survivalcraft.Android`项目上右键，点击`加载项目`，再点击`归档以用于发布`，之后按提示操作
5. 如果要生成`nupkg`引用包，请运行仓库根目录的`PackNugetPackages.bat`
6. 如果要生成网页版，请右键卸载非 Browser 的 Survivalcraft 项目，并将配置切换到 Browser，之后在`Survivalcraft.Browser`项目上右键，点击`构建所选项目`即可；如果要生成最终用于发布的网页版，请运行仓库根目录的`PublishSurvivalcraftBrowser.bat`
7. 以上过程中，如果报错未安装相应功能，请按提示完成安装

## 仓库结构简介

* `.github/workflows/`：仅用于在 GitHub Actions 中构建 IOS 版安装包
* `build/`：存放`.props`文件，以供`.csproj`引用
* `Engine/`：绝大部分游戏引擎源码，各个平台专属代码通过`#if`区分
  * `Engine/`: 下面目录分类之外的杂项，其中比较重要的是`Window.cs`，负责处理游戏窗口的创建、运行、关闭、重启
  * `Engine.Audio/`：负责处理音频的播放、暂停
  * `Engine.Graphics/`：负责处理游戏画面的绘制
  * `Engine.Input/`：负责接收来自键盘、鼠标、手柄、触摸屏的输入
  * `Engine.Media/`：负责解码图片、音频、模型等媒体资源，能编码并保存部分格式
  * `Engine.Serialization`：负责处理数据序列化和反序列化
  * `Engine.csproj`：只是用于生成 nupkg 包
  * `Engine.props`：供所有引擎项目引用
* `Engine.Android/`、`Engine.Browser/`、`Engine.IOS/`、`Engine.Linux/`、`Engine.Windows/`：各个平台的引擎项目，含有少量平台专属代码
* `EntitySystem/`：含全部实体系统源码
  * `EntitySystem.csproj`：也只是用于生成 nupkg 包
  * `EntitySystem.props`：供所有实体系统项目引用
* `EntitySystem.Android/`、`EntitySystem.Browser/`、`EntitySystem.IOS/`、`EntitySystem.Linux/`、`EntitySystem.Windows/`：各个平台的实体系统项目，无平台专属代码，但因为需要引用引擎项目，不得已也分平台
* `Survivalcraft/`：含全部游戏本体源码
  * `Block/`：全部方块的源码
  * `Component/`：全部实体组件的源码
  * `Content/`：所有游戏内容资源，需要将里面的文件打包成`Content.zip`放到正确的位置
    * `Assets/`
      * `Atlases/`：精灵图集（按钮上的图标、各种小图标汇总在一张图片上）
      * `Audio/`：除了音乐以外的所有音频文件
      * `Dialogs/`：大部分对话框的界面布局文件
      * `Fonts/`：所有字体文件
      * `Langs/`：所有语言字符串文件
      * `Models/`：所有模型文件
      * `Music/`：所有音乐文件
      * `Screens/`：大部分覆盖整个屏幕的界面布局文件
      * `Shaders/`：所有着色器文件（有部分重复的着色器在`Engine.dll`中也有）
      * `Styles/`：所有界面样式文件
      * `Textures/`：所有贴图文件
      * `Widgest/`：大部分控件的界面布局文件
      * `BlocksData.txt`：大部分方块的数据
      * `Clothes.xml`：所有服装数据
      * `Clothes.xsd`：服装数据文件的架构定义文件
      * `CraftingRecipes.xml`：绝大部分合成表数据
      * `CraftingRecipes.xsd`：合成表数据的架构定义文件
      * `Database.xml`：数据库文件，游戏中的子系统、实体、实体的组件都在此注册，并赋予初始值
      * `Datase.xsd`：数据库文件的架构定义文件
      * `NewWorldNames.txt`：新世界名称列表
      * `RecoveryProject.xml`：存档损坏后，用于尝试恢复的文件
    * `icon.webp`：内容包的图标
    * `manifest.json`：内容包的描述文件
  * `ContentProvider/`：全部内容提供器的源码，例如硬盘内容提供器、社区内容提供器等（这里的“内容”和前面的“内容资源”不是一回事）
  * `Dialog/`：全部对话框的源码
  * `ElectricElement/`：全部电路元件的源码
  * `Game/`：其他目录分类之外的杂项的源码，其中最重要的是`Program.cs`，是游戏程序的入口
  * `IContentReader/`：全部内容读取器的源码，用于从`Content.zip`读取指定类型的内容，例如`string`、`Texture2D`类型
  * `Managers/`：原版游戏就有的管理器的源码，例如设置管理器、方块管理器等
  * `ModsManager/`：除了模组管理器之外，还有其他为插件版提供模组支持的各种其他类的源码
  * `Screen/`：全部覆盖整个屏幕的界面的源码
  * `Subsystem/`：全部子系统的源码
  * `Widget/`：全部控件的源码
  * `init.js`：用于初始化 Javascript 脚本支持功能的 Javascript 脚本
  * `Survivalcraft.csproj`：也只是用于生成 nupkg 包
  * `Survivalcraft.props`：供所有游戏项目引用
* `Survivalcraft.Android/`、`Survivalcraft.Browser/`、`Survivalcraft.IOS/`、`Survivalcraft.Linux/`、`Survivalcraft.Windows/`：各个平台的游戏项目，含有从各个平台启动所需的代码
* `.editorconfig`：整个解决方案的代码样式配置文件
* `BuildEngineWindowsWithUseAngle.bat`：用于构建 Angle 兼容包的脚本（仅适用于 Windows）
* `CHANGELOG.md`：更新日志
* `nuget.config`：含有该解决方案使用的私有 Nuget 包的源的配置文件
* `PackNugetPackages.bat`：用于构建该解决方案的 Nuget 包的脚本
* `PublishSurvivalcraftBrowser.bat`：用于 publish 网页版的脚本（不`dotnet publish`无法启用 AOT）
* `README.md`：你正在阅读的该文件
* `SurvivalcraftApi.sln`：解决方案文件
* `UploadNugetPackages.bat`：用于上传 Nuget 包的脚本

> 使用 IDE 打开解决方案（SurvivalcraftApi.sln文件）后，你将看到每个平台有一个专门的目录，里面有 `Engine.平台`、`EntitySystem.平台`、`Survivalcraft.平台` 三个项目，在目录上右键，能非常方便地卸载你不需要的平台的所有项目

## 感谢

* 西班牙语 (Español) 翻译
  * Fire Dragon (Discord: firedragon4095)
  * Kike13 (Discord: .kike.04)
* 越南语 (Tiếng Việt) 翻译
  * Long (Discord: daylong89)
* 罗马尼亚语 (Română) 翻译
  * NBG (Discord: nbgr)
* 俄语 (Русский) 补充翻译
  * Dasyukevich Pavel (VK: pawwel3l) 