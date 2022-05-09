using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace OpenTap
{
    class ComponentSettingsContext
    {
        static readonly TraceSource log = Log.CreateSource("Settings");

        readonly Memorizer<Type, ComponentSettings> objectCache;
        readonly Dictionary<string, string> groupDir = new Dictionary<string, string>();
        readonly Queue<TapSerializer> flushQueues = new Queue<TapSerializer>();

        public bool readOnlyContext = false;

        public ComponentSettingsContext()
        {
            objectCache = new Memorizer<Type, ComponentSettings>(Load);
        }

        string settingsDirectoryRoot = Path.Combine(ExecutorClient.ExeDir, "Settings");

        void Invalidate(IList<ComponentSettings> setting)
        {
            // Settings can be co-dependent. Example: Connections and Instruments.
            // So we need to invalidate all the settings,  invoke the event afterwards.
            foreach (var componentSetting in setting)
                objectCache.Invalidate(componentSetting.GetType());
            foreach (var componentSetting in setting)
                componentSetting.InvokeInvalidate();
        }

        public void InvalidateAllSettings()
        {
            var cachedComponentSettings = objectCache.GetResults()
                .Where(x => x != null)
                .ToArray();
            Invalidate(cachedComponentSettings);
        }

        public void SaveAllCurrentSettings()
        {
            foreach (var cacheType in xmlCache.Keys.ToArray())
                GetCurrent(cacheType);
            foreach (var comp in objectCache.GetResults().Where(x => x != null))
                Save(comp);
        }

        public event EventHandler CacheInvalidated;


        /// <summary> Directory root for platform settings. </summary>    
        public string SettingsDirectoryRoot
        {
            get => settingsDirectoryRoot;
            set
            {
                settingsDirectoryRoot = value;
                InvalidateAllSettings();
            }
        }

        public void Reload() => CacheInvalidated?.Invoke(this, new EventArgs());

        public void Invalidate(Type t) => Invalidate(ComponentSettings.GetCurrent(t).AsSingle());

        public string GetSaveFilePath(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (type.DescendsTo(typeof(ComponentSettings)) == false)
                throw new ArgumentException(
                    "Type must inherit from ComponentSettings, otherwise it does not have a settings file.",
                    nameof(type));
            var settingsGroup = type.GetAttribute<SettingsGroupAttribute>();

            bool isProfile = settingsGroup?.Profile ?? false;
            string groupName = settingsGroup == null ? "" : settingsGroup.GroupName;

            // DisplayAttribute.GetFullName() joins the groups with ' \ ', but adding this space makes the save path invalid.
            var disp = type.GetDisplayAttribute();
            var groups = disp.Group.Length == 0 ? new[] { disp.Name } : disp.Group.Append(disp.Name);
            string fullName = string.Join("\\", groups);

            return Path.Combine(GetSettingsDirectory(groupName, isProfile),
                fullName + ".xml");
        }

        public void Save(ComponentSettings setting)
        {
            if (readOnlyContext) throw new Exception("Cannot save a read-only component settings context");
            EnsureSettingsDirectoryExists(setting.GroupName, setting.profile);

            string path = GetSaveFilePath(setting.GetType());
            string dir = Path.GetDirectoryName(path);
            if (dir != "" && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var sw = Stopwatch.StartNew();

            using (var str = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            {
                using (var xmlWriter =
                    System.Xml.XmlWriter.Create(str, new System.Xml.XmlWriterSettings { Indent = true }))
                    new TapSerializer().Serialize(xmlWriter, setting);
            }

            log.Debug(sw, "Saved {0} to {1}", setting.GetType().Name, path);
        }

        public void EnsureSettingsDirectoryExists(string groupName, bool isProfile = true)
        {
            if (!Directory.Exists(SettingsDirectoryRoot))
                Directory.CreateDirectory(SettingsDirectoryRoot);
            if (!Directory.Exists(GetSettingsDirectory(groupName, isProfile)))
                Directory.CreateDirectory(GetSettingsDirectory(groupName, isProfile));
        }

        /// <summary>
        /// The directory where the settings are loaded from / saved to.
        /// </summary>
        /// <param name="groupName">Name of the settings group.</param>
        /// <param name="isProfile">If the settings group uses profiles, we load the default profile.</param>
        /// <returns></returns>
        public string GetSettingsDirectory(string groupName, bool isProfile = true)
        {
            if (groupName == null)
                throw new ArgumentNullException(nameof(groupName));
            if (isProfile == false)
                return Path.Combine(SettingsDirectoryRoot, groupName);
            if (!groupDir.ContainsKey(groupName))
            {
                var file = Path.Combine(SettingsDirectoryRoot, groupName, "CurrentProfile");

                if (File.Exists(file))
                    groupDir[groupName] = File.ReadAllText(file);
                else
                    groupDir[groupName] = "Default";
            }


            return Path.Combine(SettingsDirectoryRoot, groupName, groupDir[groupName]);
        }

        public ComponentSettings GetCurrent(Type settingsType)
        {
            lock (flushQueues)
            {
                if (flushQueues.Count == 0)
                {
                    var result = objectCache.Invoke(settingsType);
                    while (flushQueues.Count > 0)
                        flushQueues.Dequeue().Flush();
                    return result;
                }
            }

            return objectCache.Invoke(settingsType);
        }


        public void SetCurrent(Stream xmlFileStream)
        {
            xmlFileStream.Position = 0;
            using (var mem = new MemoryStream())
            {
                xmlFileStream.CopyTo(mem);
                mem.Position = 0;
                try
                {
                    var doc = XDocument.Load(mem);
                    if (doc.Root.Attribute("type") is null)
                    {
                        mem.Position = 0;
                        throw new InvalidDataException($"Stream does not contain valid ComponentSettings. Unable to determine ComponentSettings type from root attribute. Content: {Encoding.UTF8.GetString(mem.ToArray())}");
                    }
                    ITypeData typedata = TypeData.GetTypeData(doc.Root.Attribute(TapSerializer.typeName).Value);
                    xmlCache[typedata.AsTypeData().Type] = mem.ToArray();
                    Invalidate(typedata.AsTypeData().Type);
                }
                catch (XmlException ex)
                {
                    mem.Position = 0;
                    throw new InvalidDataException($"Stream does not contain valid ComponentSettings. Unable to parse XML. Content: {Encoding.UTF8.GetString(mem.ToArray())}", ex);
                }
            }
        }

        public ComponentSettings GetCurrentFromCache(Type settingsType) =>
            objectCache.GetCached(settingsType);

        /// <summary>
        /// Loads a new instance of the settings for a given component.
        /// </summary>
        /// <param name="settingsType">The type of the component settings to load (this type must be a descendant of <see cref="ComponentSettings"/>).</param>
        /// <returns>Returns the settings.</returns>
        public ComponentSettings Load(Type settingsType)
        {
            xmlCache.TryGetValue(settingsType, out byte[] cachedXml);

            string path = GetSaveFilePath(settingsType);
            Stopwatch timer = Stopwatch.StartNew();

            ComponentSettings settings = null;
            if (cachedXml != null || File.Exists(path))
            {
                try
                {
                    Stream reader;
                    if (cachedXml != null)
                        reader = new MemoryStream(cachedXml);
                    else reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                    using (var str = reader)
                    {
                        var serializer = new TapSerializer();
                        lock (flushQueues)
                            flushQueues.Enqueue(serializer);
                        settings = (ComponentSettings)serializer.Deserialize(str, false,
                            TypeData.FromType(settingsType), path: path);
                    }
                }
                catch (Exception ex) when (ex.InnerException is System.ComponentModel.LicenseException lex)
                {
                    log.Warning("Unable to load '{0}'. {1}", settingsType.GetDisplayAttribute().GetFullName(),
                        lex.Message);
                }
                catch (Exception ex)
                {
                    if (ex.InnerException != null)
                    {
                        if (ex.InnerException.Message.StartsWith("The specified type was not recognized"))
                            log.Warning("Error loading settings file for {0}. {1}.", settingsType.Name,
                                ex.InnerException.Message);
                        else
                            log.Warning(
                                "Error loading settings file for {0}. Is it an old version? A new file will be created with default values.",
                                settingsType.Name);
                    }

                    log.Debug(ex);
                }

                log.Debug(timer, "{0} loaded from {1}", settingsType.Name, path);
            }

            if (settings == null)
            {
                try
                {
                    settings = (ComponentSettings)Activator.CreateInstance(settingsType);
                }
                catch (TargetInvocationException ex)
                {
                    log.Error("Could not create '{0}': {1}", settingsType.GetDisplayAttribute().Name,
                        ex.InnerException.Message);
                    log.Debug(ex);
                    return null;
                }
                catch (Exception e)
                {
                    log.Error("Caught exception while creating instance of '{0}'", settingsType.FullName);
                    log.Debug(e);
                    return null;
                }

                settings.Initialize();

                log.Debug(timer,
                    "No settings file exists for {0}. A new instance with default values has been created.",
                    settingsType.Name);
            }

            return settings;
        }

        public void SetSettingsProfile(string groupName, string profileName)
        {
            if (groupName == null)
                throw new ArgumentNullException(nameof(groupName));
            if (profileName == null)
                throw new ArgumentNullException(nameof(profileName));

            if (GetSettingsDirectory(groupName) == profileName)
                return;

            if (ComponentSettings.PersistSettingGroups)
            {
                try
                {
                    EnsureSettingsDirectoryExists(groupName);
                }
                catch
                {
                }

                var currentSettingsFile = Path.Combine(SettingsDirectoryRoot, groupName, "CurrentProfile");
                if (File.Exists(currentSettingsFile))
                    File.SetAttributes(currentSettingsFile, FileAttributes.Normal);
                File.WriteAllText(currentSettingsFile,
                    FileSystemHelper.GetRelativePath(Path.GetFullPath(Path.Combine(SettingsDirectoryRoot, groupName)),
                        Path.GetFullPath(profileName)));
                File.SetAttributes(currentSettingsFile, FileAttributes.Hidden);
            }

            groupDir[groupName] = profileName;
            InvalidateAllSettings();
        }

        readonly Dictionary<Type, byte[]> xmlCache = new Dictionary<Type, byte[]>();

        public ComponentSettingsContext Clone()
        {
            var session = new ComponentSettingsContext
            {
                SettingsDirectoryRoot = settingsDirectoryRoot,
            };
            var loadedSettings = objectCache.GetResults().ToArray();
            var serializer = new TapSerializer();
            var mem = new MemoryStream();
            foreach (var setting in loadedSettings)
            {
                mem.Seek(0, SeekOrigin.Begin);
                mem.SetLength(0);

                serializer.Serialize(mem, setting);
                session.xmlCache[setting.GetType()] = mem.ToArray();
            }

            return session;
        }
    }
}