// Game.ModsManager

using System.Collections.Frozen;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Engine;
using Engine.Serialization;
using Game;
using Game.IContentReader;
using NuGet.Versioning;
using XmlUtilities;
using ZipArchive = Game.ZipArchive;
#if DEBUG
using System.IO.Compression;
#endif
public static class ModsManager {
    public static string ModSuffix = ".scmod";
    public static string APIVersionString = "1.8.2.3";
    public static string ShortAPIVersionString = "1.8";
    public static NuGetVersion APINuGetVersion = new(1, 8, 2, 3);
    public static string GameVersion = "2.4.0.0";
    public static string ShortGameVersion = "2.4";
    public static string ReportLink = "https://gitee.com/SC-SPM/SurvivalcraftApi/issues";
    public static string APILatestReleaseLink_API = "https://gitee.com/api/v5/repos/SC-SPM/SurvivalcraftApi/releases/latest";
    public static string APILatestReleaseLink_Client = "https://gitee.com/SC-SPM/SurvivalcraftApi/releases/latest";
    public static string APIReleasesLink_API = "https://gitee.com/api/v5/repos/SC-SPM/SurvivalcraftApi/releases";
    public static string APIReleasesLink_Client = "https://gitee.com/SC-SPM/SurvivalcraftApi/releases/";
    public static string fName = "ModsManager";

    [Obsolete("使用ApiVersionString")]
    public enum ApiVersionEnum //不准确，弃用
    {
        Version15x = 3,
        Version170 = 17,
        Version180 = 18
    }

    [Obsolete("使用ApiVersionString")] public const ApiVersionEnum ApiVersion = ApiVersionEnum.Version180;

#if !ANDROID
    public static string ExternalPath => "app:";
    public static string DocPath = "app:/doc";
    public static string WorldsDirectoryName = $"{DocPath}/Worlds";
#endif
#if ANDROID
    public static string ExternalPath => EngineActivity.BasePath;
    public static string DocPath = EngineActivity.BasePath;
    public static string WorldsDirectoryName = $"{ExternalPath}/Worlds";
#endif
    public static string ProcessModListPath = $"{ExternalPath}/ProcessModLists";

    public static string ScreenCapturePath { get; } = $"{ExternalPath}/ScreenCapture";

    public static string UserDataPath { get; } = $"{DocPath}/UserId.dat";
    public static string CharacterSkinsDirectoryName { get; } = $"{DocPath}/CharacterSkins";
    public static string FurniturePacksDirectoryName { get; } = $"{DocPath}/FurniturePacks";

    public static string BlockTexturesDirectoryName { get; } = $"{DocPath}/TexturePacks";
    public static string CommunityContentCachePath { get; } = $"{DocPath}/CommunityContentCache.xml";
    public static string OriginalCommunityContentCachePath { get; } = $"{DocPath}/OriginalCommunityContentCache.xml";
    public static string ModsSettingsPath { get; } = $"{DocPath}/ModSettings.xml";
    public static string SettingPath { get; } = $"{DocPath}/Settings.xml";
    public static string ConfigsPath { get; } = $"{DocPath}/Configs.xml";
    public static string LogPath { get; } = $"{ExternalPath}/Bugs";
    public static string ModsPath = $"{ExternalPath}/Mods";
    public static bool IsAndroid => OperatingSystem.IsAndroid();
    //public static bool IsAndroid => VersionsManager.Platform == Platform.Android;

    internal static ModEntity SurvivalCraftModEntity;
    internal static bool ConfigLoaded;

    public class ModSettings {
        public string languageType = string.Empty;
    }

    public class ModHook(string name) {
        public string HookName = name;
        public Dictionary<ModLoader, bool> Loaders = [];
        public Dictionary<ModLoader, string> DisableReason = [];

        public void Add(ModLoader modLoader) {
            if (!Loaders.TryGetValue(modLoader, out _)) {
                Loaders.Add(modLoader, true);
            }
        }

        public void Remove(ModLoader modLoader) {
            Loaders.Remove(modLoader, out _);
        }

        public void Disable(ModLoader from, ModLoader toDisable, string reason) {
            if (Loaders.TryGetValue(toDisable, out _)) {
                if (!DisableReason.TryGetValue(from, out _)) {
                    DisableReason.Add(from, reason);
                }
            }
        }
    }

    static bool AllowContinue = true;
    public static Dictionary<string, string> Configs = [];
    /// <summary>
    /// 所有模组，含禁用的
    /// </summary>
    public static List<ModEntity> ModListAll = [];
    /// <summary>
    /// 所有已启用的模组
    /// </summary>
    public static List<ModEntity> ModList = [];
    /// <summary>
    /// 含所有已启用的模组
    /// </summary>
    public static Dictionary<string, ModEntity> PackageNameToModEntity = [];
    public static List<ModLoader> ModLoaders = [];

    //仅手动禁用的
    public static Dictionary<string, HashSet<string>> DisabledMods = [];

    public static Dictionary<string, ModHook> ModHooks = [];
    public static Dictionary<string, Assembly> Dlls = [];

    public static bool GetModEntity(string packagename, out ModEntity modEntity) {
        modEntity = ModList.Find(px => px.modInfo.PackageName == packagename);
        return modEntity != null;
    }

    public static bool GetAllowContinue() => AllowContinue;

    internal static void Reboot() {
        SettingsManager.SaveSettings();
        SettingsManager.LoadSettings();
        foreach (ModEntity mod in ModList) {
            mod.Dispose();
        }
        ScreensManager.SwitchScreen("Loading");
    }

    /// <summary>
    ///     执行Hook
    /// </summary>
    /// <param name="HookName"></param>
    /// <param name="action"></param>
    public static void HookAction(string HookName, Func<ModLoader, bool> action) //按先加载→后加载模组（先主题模组后辅助模组的顺序）执行
    {
        if (ModHooks.TryGetValue(HookName, out ModHook modHook)) {
            foreach (ModLoader modLoader in modHook.Loaders.Keys) {
                if (TryInvoke(modHook, modLoader, action)) {
                    break;
                }
            }
        }
    }

    public static void HookActionReverse(string HookName, Func<ModLoader, bool> action) //按后加载→先加载模组（先辅助模组后主题模组的顺序）执行
    {
        if (ModHooks.TryGetValue(HookName, out ModHook modHook)) {
            foreach (ModLoader modLoader in modHook.Loaders.Keys.Reverse()) {
                if (TryInvoke(modHook, modLoader, action)) {
                    break;
                }
            }
        }
    }

    public static Dictionary<KeyValuePair<ModHook, ModLoader>, bool> m_hookBugLogged = [];

    public static bool TryInvoke(ModHook modHook, ModLoader modLoader, Func<ModLoader, bool> action) {
        try {
            if (action.Invoke(modLoader)) {
                return true;
            }
            return false;
        }
        catch (Exception ex) {
            KeyValuePair<ModHook, ModLoader> keyValuePair = new(modHook, modLoader);
            if (!m_hookBugLogged.GetValueOrDefault(keyValuePair, false)) {
                Log.Error(ex);
            }
            m_hookBugLogged[keyValuePair] = true;
            return false;
        }
    }

    /// <summary>
    ///     注册Hook
    /// </summary>
    /// <param name="HookName"></param>
    /// <param name="modLoader"></param>
    public static void RegisterHook(string HookName, ModLoader modLoader) {
        if (!ModHooks.TryGetValue(HookName, out ModHook modHook)) {
            modHook = new ModHook(HookName);
            ModHooks.Add(HookName, modHook);
        }
        modHook.Add(modLoader);
    }

    public static void DisableHook(ModLoader from, string HookName, string packageName, string reason) {
        ModEntity modEntity = ModList.Find(p => p.modInfo.PackageName == packageName);
        if (modEntity != null
            && ModHooks.TryGetValue(HookName, out ModHook modHook)) {
            modHook.Disable(from, modEntity.Loader, reason);
        }
    }

    public static T GetInPakOrStorageFile<T>(string filePath, string suffix = "txt") where T : class =>
        //string storagePath = Storage.CombinePaths(ExternelPath, filepath + prefix);
        ContentManager.Get<T>(filePath, suffix);

    public static ModInfo DeserializeJson(string json) {
        ModInfo modInfo = new();
        JsonElement jsonElement = JsonDocument.Parse(json, JsonDocumentReader.DefaultJsonOptions).RootElement;
        if (jsonElement.TryGetProperty("Name", out JsonElement name)) {
            modInfo.Name = name.GetString();
        }
        if (jsonElement.TryGetProperty("Version", out JsonElement version)
            && version.ValueKind == JsonValueKind.String) {
            modInfo.Version = version.GetString()?.Trim();
            if (modInfo.Version != null) {
                NuGetVersion.TryParse(modInfo.Version, out modInfo.NuGetVersion);
            }
        }
        if (jsonElement.TryGetProperty("ApiVersion", out JsonElement apiVersion)
            && apiVersion.ValueKind == JsonValueKind.String) {
            string apiVersionString = apiVersion.GetString()?.Trim();
            modInfo.ApiVersion = apiVersionString;
            if (apiVersionString == "1.80") {
                apiVersionString = "1.8";
            }
            else if (apiVersionString == "1.81") {
                apiVersionString = "1.8.1";
            }
            TryParseVersionRange(apiVersionString, out modInfo.ApiVersionRange);
        }
        if (jsonElement.TryGetProperty("Description", out JsonElement description)
            && description.ValueKind == JsonValueKind.String) {
            modInfo.Description = description.GetString();
        }
        if (jsonElement.TryGetProperty("ScVersion", out JsonElement scVersion)
            && scVersion.ValueKind == JsonValueKind.String) {
            modInfo.ScVersion = scVersion.GetString();
        }
        if (jsonElement.TryGetProperty("Link", out JsonElement link)
            && link.ValueKind == JsonValueKind.String) {
            modInfo.Link = link.GetString();
        }
        if (jsonElement.TryGetProperty("Author", out JsonElement author)
            && author.ValueKind == JsonValueKind.String) {
            modInfo.Author = author.GetString();
        }
        if (jsonElement.TryGetProperty("PackageName", out JsonElement packageName)
            && packageName.ValueKind == JsonValueKind.String) {
            modInfo.PackageName = packageName.GetString();
        }
        /*if (jsonElement.TryGetProperty("Email", out JsonElement Email) && Email.ValueKind == JsonValueKind.String)
        {
            modInfo.Email = packageName.GetString();
        }*/
        if (jsonElement.TryGetProperty("Dependencies", out JsonElement dependencies)) {
            if (dependencies.ValueKind == JsonValueKind.Array) {
                modInfo.Dependencies = dependencies.EnumerateArray()
                    .Where(dependency => dependency.ValueKind == JsonValueKind.String)
                    .Select(dependency => dependency.GetString())
                    .ToList();
                foreach (string dependency in modInfo.Dependencies) {
                    int index = dependency.IndexOf(':');
                    if (index != -1) {
                        string dependencyPackageName = dependency.Substring(0, index);
                        string dependencyVersion = dependency.Substring(index + 1).Trim();
                        if (TryParseVersionRange(dependencyVersion, out VersionRange dependencyVersionRange)) {
                            modInfo.DependencyRanges.Add(dependencyPackageName, dependencyVersionRange);
                        }
                    }
                    else {
                        modInfo.DependencyRanges.Add(dependency, VersionRange.All);
                    }
                }
            }
            else if (dependencies.ValueKind == JsonValueKind.Object) {
                foreach (JsonProperty dependency in dependencies.EnumerateObject()) {
                    if (dependency.Value.ValueKind == JsonValueKind.String) {
                        string dependencyPackageName = dependency.Name;
                        string dependencyVersion = dependency.Value.GetString()?.Trim();
                        if (TryParseVersionRange(dependencyVersion, out VersionRange dependencyVersionRange)) {
                            modInfo.DependencyRanges.Add(dependencyPackageName, dependencyVersionRange);
                        }
                    }
                }
            }
        }
        if (jsonElement.TryGetProperty("LoadOrder", out JsonElement loadOrder)
            && loadOrder.ValueKind == JsonValueKind.Number) {
            modInfo.LoadOrder = loadOrder.GetInt32();
            //Log.Information("获取模组的Order：" + modInfo.LoadOrder);
        }
        if (jsonElement.TryGetProperty("NonPersistentMod", out JsonElement nonPersistentMod)
            && nonPersistentMod.ValueKind == JsonValueKind.True) {
            modInfo.NonPersistentMod = true;
        }
        return modInfo;
    }

    public static void SaveModSettings(XElement xElement) {
        foreach (ModEntity modEntity in ModList) {
            modEntity.SaveSettings(xElement);
        }
    }

    public static void SaveConfigs() {
        XElement element = new("Configs");
        foreach (KeyValuePair<string, string> c in Configs) {
            element.SetAttributeValue(c.Key, c.Value);
        }
        using (Stream stream = Storage.OpenFile(ConfigsPath, OpenFileMode.Create)) {
            XmlUtils.SaveXmlToStream(element, stream, Encoding.UTF8, true);
        }
    }

    public static void LoadConfigs() {
        //加载Config
        try {
            if (Storage.FileExists(ConfigsPath)) {
                using (Stream stream = Storage.OpenFile(ConfigsPath, OpenFileMode.Read)) {
                    XElement xElement = XmlUtils.LoadXmlFromStream(stream, null, true);
                    LoadConfigsFromXml(xElement);
                }
            }
        }
        catch (Exception e) {
            Log.Error($"Load configs failed. Reason: {e}");
            ConfigLoaded = false;
        }
    }

    public static void LoadConfigsFromXml(XElement xElement) {
        try {
            if (xElement.Name != "Configs") {
                return;
            }
            foreach (XAttribute c in xElement.Attributes()) {
                if (!Configs.ContainsKey(c.Name.LocalName)) {
                    SetConfig(c.Name.LocalName, c.Value);
                }
            }
            ConfigLoaded = true;
        }
        catch (Exception e) {
            Log.Error($"Load configs failed. Reason: {e}");
            ConfigLoaded = false;
        }
    }

    public static void LoadModSettings(XElement xElement) {
        foreach (ModEntity modEntity in ModList) {
            modEntity.LoadSettings(xElement);
        }
    }

    public static void SetConfig(string key, string value) {
        if (!Configs.TryAdd(key, value)) {
            Configs[key] = value;
        }
    }

    public static string ImportMod(string name, Stream stream) {
        if (!Storage.DirectoryExists(ModsPath)) {
            Storage.CreateDirectory(ModsPath);
        }
        if (!Storage.DirectoryExists(ProcessModListPath)) {
            Storage.CreateDirectory(ProcessModListPath);
        }
        string realName = name;
        if (!realName.EndsWith(ModSuffix)) {
            realName = realName + ModSuffix;
        }
        string nameWithoutSuffix = Storage.GetFileNameWithoutExtension(realName);
        string path = Storage.CombinePaths(ModsPath, realName);
        int num = 1;
        while (Storage.FileExists(path)) {
            realName = $"{nameWithoutSuffix}({num}){ModSuffix}";
            path = Storage.CombinePaths(ModsPath, realName);
            num++;
        }
        using (Stream fileStream = Storage.OpenFile(path, OpenFileMode.CreateOrOpen)) {
            stream.CopyTo(fileStream);
        }
        return realName;
    }

    public static void ModListAllDo(Action<ModEntity> entity) {
        for (int i = 0; i < ModList.Count; i++) {
            entity?.Invoke(ModList[i]);
        }
    }

    public static void Initialize() {
        if (!Storage.DirectoryExists(ModsPath)) {
            Storage.CreateDirectory(ModsPath);
        }
        ModHooks.Clear();
        ModListAll.Clear();
        ModList.Clear();
        PackageNameToModEntity.Clear();
        ModLoaders.Clear();
        SurvivalCraftModEntity = new SurvivalCraftModEntity();
        ModListAll.Add(SurvivalCraftModEntity);
#if !BROWSER
        if (SettingsManager.SafeMode) {
            return;
        }
        ModEntity FastDebug = new FastDebugModEntity();
        ModListAll.Add(FastDebug);
        GetScmods(ModsPath);
        ModListAll.Sort((x, y) =>
            (x.IsDisabled ? int.MaxValue : x.modInfo?.LoadOrder ?? int.MaxValue).CompareTo(
                y.IsDisabled ? int.MaxValue : y.modInfo?.LoadOrder ?? int.MaxValue
            )
        );
        //float api = float.Parse(APIVersion);
        //读取SCMOD文件到ModListAll列表
        foreach (ModEntity modEntity1 in ModListAll) {
            if (modEntity1.IsDisabled) {
                continue;
            }
            string packageName = modEntity1.modInfo?.PackageName;
            if (packageName == null) {
                continue;
            }
            //ModInfo disabledmod = ToDisable.Find(l => l.PackageName == modInfo.PackageName);
            //if (disabledmod != null && disabledmod.PackageName != SurvivalCraftModEntity.modInfo.PackageName && disabledmod.PackageName != FastDebug.modInfo.PackageName)
            //{
            //	ToDisable.Add(modInfo);
            //	ToRemove.Add(modEntity1);
            //	continue;
            //}
            //float.TryParse(modInfo.ApiVersionString, out float curr);
            //if (curr < api)
            //{//api版本检测
            //    ToDisable.Add(modInfo);
            //    ToRemove.Add(modEntity1);
            //    AddException(new Exception($"[{modEntity1.modInfo.PackageName}]Target version {modInfo.Version} is less than api version {APIVersion}."), true);
            //}
            List<ModEntity> modEntities = ModListAll.FindAll(px => !px.IsDisabled && px.modInfo?.PackageName == packageName);
            if (modEntities.Count > 1) {
                modEntity1.IsDisabled = true;
                modEntity1.DisableReason = ModDisableReason.Duplicated;
                AddException(new Exception($"Multiple mods with PackageName [{packageName}], please keep only one."));
            }
        }
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) => {
            try {
#nullable enable
                Assembly? assembly = Dlls.GetValueOrDefault(args.Name)
                    ?? TypeCache.LoadedAssemblies.FirstOrDefault(asm => asm.GetName().FullName == args.Name);
                return assembly;
#nullable disable
            }
            catch (Exception e) {
                Log.Error($"Load assembly [{args.Name}] failed:{e}");
                Log.Debug(e);
                throw;
            }
        };
#endif
    }

    public static void AddException(Exception e, bool AllowContinue_ = false) {
        LoadingScreen.Error(e.ToString());
        Log.Error(e);
        AllowContinue = !SettingsManager.DisplayLog || AllowContinue_;
    }

    /// <summary>
    ///     获取所有文件
    /// </summary>
    /// <param name="path">文件路径</param>
    public static void GetScmods(string path) {
        foreach (string item in Storage.ListFileNames(path)) {
            string ms = Storage.GetExtension(item).ToLowerInvariant();
            string ks = Storage.CombinePaths(path, item);
            using Stream stream = Storage.OpenFile(ks, OpenFileMode.Read);
            try {
                if (ms == ModSuffix) {
                    Stream keepOpenStream = GetDecipherStream(stream);
                    ModEntity modEntity = new(ks, ZipArchive.Open(keepOpenStream, true));
                    if (modEntity.modInfo == null) {
                        LoadingScreen.Warning($"The modinfo.json is missing or broken from [{Storage.GetFileName(modEntity.ModFilePath)}], and this mod will be disabled.");
                    }
                    else if (modEntity.IsDisabled && modEntity.DisableReason == ModDisableReason.InvalidPackageName) {
                        LoadingScreen.Warning($"The package name [{modEntity.modInfo.PackageName}] of [{Storage.GetFileName(modEntity.ModFilePath)}] is not allowed, and this mod will not be loaded.");
                        continue;
                    }
                    ModListAll.Add(modEntity);
                }
            }
            catch (Exception e) {
                AddException(e);
                stream.Close();
            }
        }
        foreach (string dir in Storage.ListDirectoryNames(path)) {
            GetScmods(Storage.CombinePaths(path, dir));
        }
    }

    public static string StreamToString(Stream stream) {
        stream.Seek(0, SeekOrigin.Begin);
        return new StreamReader(stream).ReadToEnd();
    }

    /// <summary>
    ///     将 Stream 转成 byte[]
    /// </summary>
    public static byte[] StreamToBytes(Stream stream) {
        byte[] bytes = new byte[stream.Length];
        stream.Seek(0, SeekOrigin.Begin);
        stream.ReadExactly(bytes);
        // 设置当前流的位置为流的开始
        return bytes;
    }

    [Obsolete("Use GetSha256 instead.")]
    public static string GetMd5(string input) {
#if BROWSER
        throw new NotSupportedException("MD5 is not supported on browser. Use GetSha256 instead.");
#else
        byte[] data = MD5.HashData(Encoding.Default.GetBytes(input));
        StringBuilder sBuilder = new();
        for (int i = 0; i < data.Length; i++) {
            sBuilder.Append(data[i].ToString("x2"));
        }
        return sBuilder.ToString();
#endif
    }

    public static string GetSha256(string input) {
        byte[] data = SHA256.HashData(Encoding.Default.GetBytes(input));
        StringBuilder sBuilder = new();
        for (int i = 0; i < data.Length; i++) {
            sBuilder.Append(data[i].ToString("x2"));
        }
        return sBuilder.ToString();
    }

    public static bool FindElement(XElement xElement, Func<XElement, bool> func, out XElement elementout) {
        elementout = xElement.Descendants().FirstOrDefault(func);
        return elementout != null;
    }

    public static bool FindElementByGuid(XElement xElement, string guid, out XElement elementout) {
        elementout = xElement.Descendants().FirstOrDefault(e => e.Attribute("Guid")?.Value == guid);
        return elementout != null;
    }

    public static bool HasAttribute(XElement element, Func<string, bool> func, out XAttribute xAttributeout) {
        xAttributeout = element.Attributes()
            .FirstOrDefault(a => func(a.Name.LocalName));
        return xAttributeout != null;
    }

    public static void CombineClo(XElement clothesRoot, Stream toCombineStream) {
        XElement toCombineRoot = XmlUtils.LoadXmlFromStream(toCombineStream, Encoding.UTF8, true);
        foreach (XElement element in toCombineRoot.Elements()) {
            string indexValue = element.Attribute("Index")?.Value;
            if (indexValue == null) {
                clothesRoot.Add(toCombineRoot);
                continue;
            }
            List<XAttribute> newAttributes = [];
            foreach (XAttribute attribute in element.Attributes()) {
                if (attribute.Name.LocalName.StartsWith("new-")) {
                    newAttributes.Add(attribute);
                }
            }
            if (newAttributes.Count > 0
                && FindElement(clothesRoot, e => e.Attribute("Index")?.Value == indexValue, out XElement element1)) {
                foreach (XAttribute newAttribute in newAttributes) {
                    element1.SetAttributeValue(newAttribute.Name.LocalName.Substring(4), newAttribute.Value);
                }
            }
            else if (HasAttribute(element, name => name.StartsWith("r-"), out XAttribute _)
                && FindElement(clothesRoot, e => e.Attribute("Index")?.Value == indexValue, out XElement element2)) {
                element2.Remove();
                element.Remove();
            }
            else {
                clothesRoot.Add(toCombineRoot);
            }
        }
    }

    public static void CombineCr(XElement xElement, Stream cloorcr) {
        XElement MergeXml = XmlUtils.LoadXmlFromStream(cloorcr, Encoding.UTF8, true);
        CombineCrLogic(xElement, MergeXml);
    }

    public static void CombineCrLogic(XElement xElement, XElement needCombine) {
        foreach (XElement element in needCombine.Elements()) {
            if (element.Attribute("Result") != null) {
                if (HasAttribute(element, name => name.StartsWith("new-"), out XAttribute attribute)) {
                    if (FindElement(
                            xElement,
                            ele => { //原始标签
                                foreach (XAttribute xAttribute in element.Attributes()) //待修改的标签
                                {
                                    if (xAttribute.Name == attribute.Name) {
                                        continue;
                                    }
                                    if (ele.Attribute(xAttribute.Name) == null) {
                                        return false;
                                    }
                                }
                                return true;
                            },
                            out XElement element1
                        )) {
                        element1.SetAttributeValue(attribute.Name.LocalName.Substring(4), attribute.Value);
                        element1.SetValue(element.Value);
                    }
                }
                else if (HasAttribute(element, name => name.StartsWith("r-"), out XAttribute attribute1)) {
                    if (FindElement(
                            xElement,
                            ele => { //原始标签
                                foreach (XAttribute xAttribute in element.Attributes()) //待修改的标签
                                {
                                    if (xAttribute.Name == attribute1.Name) {
                                        continue;
                                    }
                                    if (ele.Attribute(xAttribute.Name) == null) {
                                        return false;
                                    }
                                }
                                return true;
                            },
                            out XElement element1
                        )) {
                        element1.Remove();
                        element.Remove();
                    }
                }
                else {
                    xElement.Add(element);
                }
            }
            CombineCrLogic(xElement, element);
        }
    }

    public static void Modify(XElement source, XElement change) {
        if (FindElement(
                source,
                item => item.Name.LocalName == change.Name.LocalName
                    && item.Attribute("Guid") != null
                    && change.Attribute("Guid") != null
                    && item.Attribute("Guid")?.Value == change.Attribute("Guid")?.Value,
                out XElement xElement1
            )) {
            foreach (XElement xElement in change.Elements()) {
                Modify(xElement1, xElement);
            }
        }
        else {
            source.Add(change);
        }
    }

    public class ClassSubstitute: IEquatable<ClassSubstitute> {
        public string PackageName;
        public string ClassName;

        public ClassSubstitute(string packageName, string className) {
            PackageName = packageName;
            ClassName = className;
        }

        public bool Equals(ClassSubstitute other) {
            if (other is null) {
                return false;
            }
            if (ReferenceEquals(this, other)) {
                return true;
            }
            return PackageName == other.PackageName && ClassName == other.ClassName;
        }

        public override bool Equals(object obj) {
            return Equals(obj as ClassSubstitute);
        }

        public override int GetHashCode() => HashCode.Combine(PackageName, ClassName);

        public static bool operator ==(ClassSubstitute left, ClassSubstitute right) => Equals(left, right);

        public static bool operator !=(ClassSubstitute left, ClassSubstitute right) => !Equals(left, right);
    }

    public static FrozenDictionary<string, string> ImportantDatabaseClasses;
    public static Dictionary<string, List<ClassSubstitute>> ClassSubstitutes = [];
    public static Dictionary<string, List<ClassSubstitute>> OldClassSubstitutes = [];
    public static Dictionary<string, ClassSubstitute> SelectedClassSubstitutes = [];

    //对于关键（绑定了API1.7新的ModLoader接口的）组件，对修改行为进行检查报错
    //修饰就是用的internal，不提供其他模组的调用权限
    internal static void InitImportantDatabaseClasses() {
        if (ModList.Count <= 3) {
            return;
        }
        ImportantDatabaseClasses = new KeyValuePair<string, string>[] {
            new("7347a83f-2d46-4fdf-bce2-52677de0b568", "Game.ComponentBody"),
            new("4e14ce27-fdef-46ca-8ea0-26af43c215e5", "Game.ComponentHealth"),
            new("7ecfafc4-4603-424c-87dd-1df59e7ef413", "Game.ComponentPlayer"),
            new("9dc356e5-7dc8-45f6-8779-827ddee9966c", "Game.ComponentMiner"),
            new("6f538db3-f1fe-4e91-8ef5-627c0b1a74ba", "Game.ComponentRunAwayBehavior"),
            new("8b3d07dc-6498-4691-9686-cf4edabb8f3f", "Game.ComponentGui"),
            new("e2636c38-f179-4aa1-b087-ed6920d66e8e", "Game.SubsystemTerrain"),
            new("96e79f99-a082-4190-9ab6-835dc49ebbdd", "Game.SubsystemExplosions"),
            new("dafb8e14-11b9-44b7-a208-424b770aeaa9", "Game.SubsystemProjectiles"),
            new("32d392de-69c1-4d04-9e0b-5c7463201892", "Game.SubsystemPickables"),
            new("54a4f6d5-98dd-4dc3-bf6d-04dfd972c6b7", "Game.SubsystemTime"),
            new("b2e68ecd-49fc-4c05-b784-424da13f8550", "Game.ComponentDispenser"),
            new("f6b020bb-8994-6ae6-289b-a842e3eb9ca5", "Game.ComponentFactors"),
            new("a346c456-5087-48c4-835a-5829b3f35c68", "Game.ComponentLevel"),
            new("1df4e627-c959-4e6a-bfa2-b7ee3ef08c99", "Game.ComponentClothing"),
        }.ToFrozenDictionary();
    }

    public static void CombineDataBase(XElement databaseRoot, Stream toCombineStream) {
        CombineDataBase(databaseRoot, toCombineStream, string.Empty);
    }

    public static void CombineDataBase(XElement databaseRoot, Stream toCombineStream, string modPackageName) {
        XElement toCombineRoot = XmlUtils.LoadXmlFromStream(toCombineStream, Encoding.UTF8, true);
        XElement databaseObjects = databaseRoot.Element("DatabaseObjects");
        foreach (XElement element in toCombineRoot.Elements()) {
            // 为实体添加模组来源信息
            if (!string.IsNullOrEmpty(modPackageName)
                && element.Name.LocalName == "EntityTemplate") {
                string guid = element.Attribute("Guid")?.Value;
                bool isNewEntity = true;
                if (!string.IsNullOrEmpty(guid)) { // 检查是否为新增实体(在原数据库中不存在)
                    isNewEntity = !FindElementByGuid(databaseObjects, guid, out _);
                }
                if (isNewEntity) { // 只为新增的实体添加ModSource
                    XElement parameterElement = new("Parameter");
                    parameterElement.SetAttributeValue("Name", "ModSource");
                    parameterElement.SetAttributeValue("Value", modPackageName);
                    parameterElement.SetAttributeValue("Type", "string");
                    element.Add(parameterElement);
                }
            }
            //处理修改
            if (HasAttribute(element, str => str.StartsWith("new-"), out XAttribute newAttribute)) {
                XAttribute guidAttribute = element.Attribute("Guid");
                if (guidAttribute == null) {
                    continue;
                }
                string guid = guidAttribute.Value;
                if (FindElementByGuid(databaseObjects, guid, out XElement oldElement)) {
                    string newAttributeName = newAttribute.Name.LocalName.Substring(4);
                    if (newAttributeName == "Value"
                        && oldElement.Attribute("Name")?.Value == "Class") {
                        if (ClassSubstitutes.TryGetValue(guid, out List<ClassSubstitute> classSubstitutes)) {
                            classSubstitutes.Add(new ClassSubstitute (modPackageName, newAttribute.Value));
                        }
                        else {
                            ClassSubstitutes.Add(
                                guid,
                                [new ClassSubstitute("survivalcraft", oldElement.Attribute("Value")!.Value), new ClassSubstitute(modPackageName, newAttribute.Value)]
                            );
                        }
                    }
                    else {
                        oldElement.SetAttributeValue(newAttributeName, newAttribute.Value);
                    }
                }
            }
            else {
                Modify(databaseObjects, element);
            }
        }
    }

    public static void DealWithClassSubstitutes() {
        if (ClassSubstitutes.Count > 0) {
            Queue<(string, XElement)> needToSolves = [];
            foreach ((string guid, List<ClassSubstitute> substitutes) in ClassSubstitutes) {
                // 如果有 2 个或更多候选项
                if (substitutes.Count >= 2 && FindElementByGuid(DatabaseManager.DatabaseNode, guid, out XElement element)) {
                    // 如果手动选择过
                    if (SelectedClassSubstitutes.TryGetValue(guid, out ClassSubstitute selected)) {
                        // 如果选择项还能从候选项找到
                        if (substitutes.Any(x => x == selected)) {
                            if (OldClassSubstitutes.TryGetValue(guid, out List<ClassSubstitute> oldSubstitutes)) {
                                // 如果候选项与旧候选项一致，则使用选择项
                                if (substitutes.Count == oldSubstitutes.Count && oldSubstitutes.SequenceEqual(substitutes)) {
                                    element.SetAttributeValue("Value", selected.ClassName);
                                }
                                // 否则需要手动重选
                                else {
                                    SelectedClassSubstitutes.Remove(guid);
                                    needToSolves.Enqueue((guid, element));
                                }
                            }
                            else {
                                element.SetAttributeValue("Value", selected.ClassName);
                            }
                        }
                        else {
                            SelectedClassSubstitutes.Remove(guid);
                            needToSolves.Enqueue((guid, element));
                        }
                    }
                    // 未手动选择过
                    // 当只有两个候选项，且不重要时，直接使用第二个（第一个是原版的）
                    else if (substitutes.Count == 2 && !(ImportantDatabaseClasses?.ContainsKey(guid) ?? false)) {
                        element.SetAttributeValue("Value", substitutes.Last().ClassName);
                    }
                    else {
                        needToSolves.Enqueue((guid, element));
                    }
                }
                else {
                    SelectedClassSubstitutes.Remove(guid);
                }
            }
            if (needToSolves.Count > 0) {
                AllowContinue = false;
                void Handle() {
                    if (needToSolves.TryDequeue(out (string, XElement) tuple)) {
                        DialogsManager.ShowDialog(ScreensManager.RootWidget, new SelectClassSubstituteDialog(tuple.Item1, tuple.Item2, Handle));
                    }
                    else {
                        AllowContinue = true;
                    }
                };
                Handle();
            }
        }
        else {
            SelectedClassSubstitutes.Clear();
        }
    }

    public static bool TryParseVersionRange(string value, out VersionRange versionRange) {
        if (string.IsNullOrEmpty(value)) {
            versionRange = null;
            return false;
        }
        value = value.Trim();
        if (value.Length == 0) {
            versionRange = null;
            return false;
        }
        char firstChar = value[0];
        switch (firstChar) {
            case '=': {
                if (NuGetVersion.TryParse(value.Substring(1), out NuGetVersion nuGetVersion)) {
                    versionRange = new VersionRange(nuGetVersion, true, nuGetVersion, true);
                    return true;
                }
                break;
            }
            case '>': {
                if (value.Length > 1) {
                    if (value[1] == '=') {
                        if (NuGetVersion.TryParse(value.Substring(2), out NuGetVersion nuGetVersion)) {
                            versionRange = new VersionRange(nuGetVersion, true);
                            return true;
                        }
                    }
                    else {
                        if (NuGetVersion.TryParse(value.Substring(1), out NuGetVersion nuGetVersion)) {
                            versionRange = new VersionRange(nuGetVersion, false);
                            return true;
                        }
                    }
                }
                break;
            }
            case '<': {
                if (value.Length > 1) {
                    if (value[1] == '=') {
                        if (NuGetVersion.TryParse(value.Substring(2), out NuGetVersion nuGetVersion)) {
                            versionRange = new VersionRange(null, false, nuGetVersion, true);
                            return true;
                        }
                    }
                    else {
                        if (NuGetVersion.TryParse(value.Substring(1), out NuGetVersion nuGetVersion)) {
                            versionRange = new VersionRange(null, false, nuGetVersion);
                            return true;
                        }
                    }
                }
                break;
            }
            case '^': {
                if (NuGetVersion.TryParse(value.Substring(1), out NuGetVersion nuGetVersion)) {
                    versionRange = new VersionRange(nuGetVersion, true, new NuGetVersion(nuGetVersion.Major + 1, 0, 0, 0));
                    return true;
                }
                break;
            }
            case '~': {
                if (NuGetVersion.TryParse(value.Substring(1), out NuGetVersion nuGetVersion)) {
                    versionRange = new VersionRange(nuGetVersion, true, new NuGetVersion(nuGetVersion.Major, nuGetVersion.Minor + 1, 0, 0));
                    return true;
                }
                break;
            }
            default:
                if (VersionRange.TryParse(value, out versionRange)) {
                    return true;
                }
                break;
        }
        versionRange = null;
        return false;
    }

    public static string HeadingCode = "有头有脸天才少年,耍猴表演敢为人先";
    public static string HeadingCode2 = "修改他人mod请获得原作者授权，否则小心出名！";

    public static Stream GetDecipherStream(Stream stream) {
        MemoryStream keepOpenStream = new();
        byte[] buff = new byte[stream.Length];
        stream.ReadExactly(buff);
        byte[] hc = Encoding.UTF8.GetBytes(HeadingCode);
        bool decipher = true;
        for (int i = 0; i < hc.Length; i++) {
            if (hc[i] != buff[i]) {
                decipher = false;
                break;
            }
        }
        byte[] hc2 = Encoding.UTF8.GetBytes(HeadingCode2);
        bool decipher2 = true;
        for (int i = 0; i < hc2.Length; i++) {
            if (hc2[i] != buff[i]) {
                decipher2 = false;
                break;
            }
        }
        if (decipher) {
            byte[] buff2 = new byte[buff.Length - hc.Length];
            for (int i = 0; i < buff2.Length; i++) {
                buff2[i] = buff[buff.Length - 1 - i];
            }
            keepOpenStream.Write(buff2, 0, buff2.Length);
            keepOpenStream.Flush();
        }
        else if (decipher2) {
            byte[] buff2 = new byte[buff.Length - hc2.Length];
            int k = 0;
            int t = 0;
            int l = (buff2.Length + 1) / 2;
            for (int i = 0; i < buff2.Length; i++) {
                if (i % 2 == 0) {
                    buff2[i] = buff[hc2.Length + k];
                    k++;
                }
                else {
                    buff2[i] = buff[hc2.Length + l + t];
                    t++;
                }
            }
            keepOpenStream.Write(buff2, 0, buff2.Length);
            keepOpenStream.Flush();
        }
        else {
            stream.Position = 0L;
            stream.CopyTo(keepOpenStream);
        }
        stream.Dispose();
        keepOpenStream.Position = 0L;
        return keepOpenStream;
    }

    public static bool StrengtheningMod(string path) {
        Stream stream = Storage.OpenFile(path, OpenFileMode.Read);
        byte[] buff = new byte[stream.Length];
        stream.ReadExactly(buff);
        byte[] hc = Encoding.UTF8.GetBytes(HeadingCode);
        bool decipher = true;
        for (int i = 0; i < hc.Length; i++) {
            if (hc[i] != buff[i]) {
                decipher = false;
                break;
            }
        }
        byte[] hc2 = Encoding.UTF8.GetBytes(HeadingCode2);
        bool decipher2 = true;
        for (int i = 0; i < hc2.Length; i++) {
            if (hc2[i] != buff[i]) {
                decipher2 = false;
                break;
            }
        }
        if (decipher || decipher2) {
            return false;
        }
        byte[] buff2 = new byte[buff.Length + hc2.Length];
        int k = 0;
        int l = hc2.Length;
        for (int i = 0; i < hc2.Length; i++) {
            buff2[i] = hc2[i];
        }
        for (int i = 0; i < buff.Length; i++) {
            if (i % 2 == 0) {
                buff2[k + l] = buff[i];
                k++;
            }
        }
        k = 0;
        l = hc2.Length + (buff.Length + 1) / 2;
        for (int i = 0; i < buff.Length; i++) {
            if (i % 2 != 0) {
                buff2[k + l] = buff[i];
                k++;
            }
        }
        string newPath = $"{path.Substring(0, path.LastIndexOf('.'))}({LanguageControl.Get(fName, 63)}).scmod";
        FileStream fileStream = new(Storage.GetSystemPath(newPath), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
        fileStream.Write(buff2, 0, buff2.Length);
        fileStream.Flush();
        stream.Dispose();
        fileStream.Dispose();
        return true;
    }
}