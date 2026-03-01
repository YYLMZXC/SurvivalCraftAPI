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
* `README.md`：给玩家、路人查看的项目基本说明
* `ARCHITECTURE.md`：你正在阅读的该文件
* `SurvivalcraftApi.sln`：解决方案文件
* `UploadNugetPackages.bat`：用于上传 Nuget 包的脚本

> 使用 IDE 打开解决方案（SurvivalcraftApi.sln文件）后，你将看到每个平台有一个专门的目录，里面有 `Engine.平台`、`EntitySystem.平台`、`Survivalcraft.平台` 三个项目，在目录上右键，能非常方便地卸载你不需要的平台的所有项目