//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml.Serialization;
using System.Diagnostics;
using System.Reflection;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Collections;

namespace OpenTap
{
    /// <summary>
    /// <see cref="ComponentSettings"/> with this attribute belong to a group with a specified name. 
    /// They can be marked as profile groups, enabling selectable profiles for settings in that group.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class SettingsGroupAttribute : Attribute
    {
        /// <summary>
        /// Name of the settings group.
        /// </summary>
        public string GroupName { get; private set; }
        /// <summary>
        /// Specifies whether this settings group uses profiles.  
        /// </summary>
        public bool Profile { get; private set; }

        /// <summary>
        /// Constructor for <see cref="SettingsGroupAttribute"/> 
        /// </summary>
        /// <param name="GroupName">The name of the settings group.</param>
        /// <param name="Profile">If this settings group should use profiles.</param>
        public SettingsGroupAttribute(string GroupName, bool Profile = true)
        {
            this.GroupName = GroupName;
            this.Profile = Profile;
        }
    }

    /// <summary>
    /// Attribute that determines if the settings list should be fixed.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class FixedSettingsListAttribute : Attribute
    {
    }

    ///<summary>
    /// Contains some extra functionality for the ComponentSettingsList.
    /// Created so that it is possible to know which (generic) ComponentSettingsList
    /// contains a given type.
    /// </summary>
    public static class ComponentSettingsList
    {
        static private Dictionary<Type, Type> typehandlers_cache = null;

        static private Dictionary<Type, Type> typehandlers
        {
            get
            {
                if (typehandlers_cache == null)
                {
                    typehandlers_cache = new Dictionary<Type, Type>();
                    foreach (Type settingsType in PluginManager.GetPlugins(typeof(ComponentSettings)))
                    {
                        Type baseType = settingsType;
                        while (baseType != typeof(object))
                        {
                            baseType = baseType.BaseType;
                            if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(ComponentSettingsList<,>))
                            {
                                Type[] types = settingsType.BaseType.GetGenericArguments();
                                if (types.Length == 2)
                                {
                                    typehandlers_cache[types[1]] = types[0];
                                    break;
                                }
                            }
                        }
                    }
                }
                return typehandlers_cache;
            }
        }

        /// <summary>
        /// Gets the GetCurrent method for the container for type T.
        /// Null if no such container.
        /// </summary>
        /// <param name="T"></param>
        /// <returns></returns>
        static PropertyInfo getGetCurrentMethodForContainer(Type T)
        {
            if (T == null)
                throw new ArgumentNullException("T");
            foreach (Type key in typehandlers.Keys)
            {
                if (T.DescendsTo(key))
                {

                    Type compSetType = typehandlers[key];
                    PropertyInfo prop = compSetType.GetProperty("Current", BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
                    if (prop != null)
                        return prop;
                }
            }
            return null;
        }
        
        /// <summary>
        /// Finds a ComponentSettingsList containing instances of T.
        /// </summary>
        /// <param name="T"></param>
        /// <returns>A List of type T. Null if no ComponentSettingsList exists containing T.</returns>
        public static IList GetContainer(Type T)
        {
            var m = getGetCurrentMethodForContainer(T);
            if (m == null) return null;
            return (IList)m.GetValue(null, null);
        }

        /// <summary>
        /// For checking if there is a ComponentSettings for T without evaluating GetCurrent.
        /// </summary>
        /// <param name="T"></param>
        /// <returns></returns>
        internal static bool HasContainer(Type T)
        {
            return getGetCurrentMethodForContainer(T) != null;
        }

        /// <summary>
        /// Gets the ComponentSettings list for T and filters the instances that are not T.
        /// </summary>
        /// <returns></returns>
        public static IList<T> GetItems<T>()
        {
            IList items = GetContainer(typeof(T));
            if (items == null) return new List<T>();
            return items.OfType<T>().ToList();
        }
        /// <summary>
        /// (non-generic) Gets the ComponentSettings list for T and filters the instances that are not T.
        /// </summary>
        /// <param name="T"></param>
        /// <returns></returns>
        public static IList GetItems(Type T)
        {
            var container = GetContainer(T);
            if (container == null) return new List<object>();
            return container.Cast<object>().Where(v => v.GetType().DescendsTo(T)).ToList();
        }

    }

    /// <summary>
    /// ComponentSettingsList is a collection of objects. This is the case for DutSettings, for instance.
    /// Uses IObservableCollection so that changes can be monitored.
    /// </summary>
    /// <typeparam name="DerivedType"></typeparam>
    /// <typeparam name="ContainedType"></typeparam>
    public abstract class ComponentSettingsList<DerivedType, ContainedType> : ComponentSettings<DerivedType>, INotifyCollectionChanged, IList, IList<ContainedType>
        where DerivedType : ComponentSettingsList<DerivedType, ContainedType>
    {
        ObservableCollection<ContainedType> list { get; set; }
        IList ilist;
        /// <summary>
        /// Gets the first or default instance in the component settings list.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetDefault<T>() where T : ContainedType
        {
            return GetDefaultOf<T>();
        }
        /// <summary>
        /// Static Get first or default instance in the component settings list. (uses GetCurrent)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T GetDefaultOf<T>() where T : ContainedType
        {
            return (T)((ComponentSettingsList<DerivedType, ContainedType>)GetCurrent<DerivedType>())
                .FirstOrDefault(obj => obj is T);
        }
        

        /// <summary>
        /// Initializes the list.
        /// </summary>
        public ComponentSettingsList()
        {
            list = new ObservableCollection<ContainedType>();
            list.CollectionChanged += list_CollectionChanged;
            ilist = list;
        }

        void list_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                IResource newItem = e.NewItems.Cast<ContainedType>().First() as IResource;
                if (newItem != null)
                {
                    var sameName = this.FirstOrDefault(itm => (itm as IResource).Name == newItem.Name && (itm as IResource) != newItem);
                    int number = 0;
                    while (sameName != null)
                    {
                        number++;
                        sameName = this.FirstOrDefault(itm => (itm as IResource).Name == newItem.Name + number && (itm as IResource) != newItem);
                    }
                    if (number > 0)
                    { 
                        newItem.Name += number;
                    }
                }
            }
            if (CollectionChanged != null)
            {
                CollectionChanged.Invoke(sender, e);
            }
            OnPropertyChanged("Count");
        }
        /// <summary>
        /// Adds an element to the collection.
        /// </summary>
        /// <param name="item"></param>
        public void Add(ContainedType item)
        {
            list.Add(item);
        }
        /// <summary>
        /// Removes all elements from the collection.
        /// </summary>
        public void Clear()
        {
            list.Clear();
        }
        /// <summary>
        /// Determines if the collection contains the specified element.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Contains(ContainedType item)
        {
            return list.Contains(item);
        }
        /// <summary>
        /// Copies the collection to a compatible array.
        /// </summary>
        /// <param name="array"></param>
        /// <param name="arrayIndex"></param>
        public void CopyTo(ContainedType[] array, int arrayIndex)
        {
            list.CopyTo(array, arrayIndex);
        }
        /// <summary>
        /// Gets the number of elements in the collection.
        /// </summary>
        public int Count
        {
            get { return list.Count; }
        }
        /// <summary>
        /// Determines if the collection is read only.
        /// </summary>
        public bool IsReadOnly
        {
            get { return false; }
        }
        /// <summary>
        /// Removes the first occurrence of a specified element from the collection.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Remove(ContainedType item)
        {
            return list.Remove(item);
        }
        /// <summary>
        /// Returns an <see cref="IEnumerator"/> that iterates through the collection.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<ContainedType> GetEnumerator()
        {
            return list.GetEnumerator();
        }
        /// <summary>
        /// Returns an <see cref="IEnumerator"/> that iterates through the collection.
        /// </summary>
        /// <returns></returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        /// <summary>
        /// Returns the index of the first occurrences of a specified element in the collection.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public int IndexOf(ContainedType item)
        {
            return list.IndexOf(item);
        }
        /// <summary>
        /// Insert an element into the collection at the specified index.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="item"></param>
        public void Insert(int index, ContainedType item)
        {
            list.Insert(index, item);
        }
        /// <summary>
        /// Removes an element at a specified index.
        /// </summary>
        /// <param name="index"></param>
        public void RemoveAt(int index)
        {
            list.RemoveAt(index);
        }
        /// <summary>
        /// List interface
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        [XmlIgnore]
        public ContainedType this[int index]
        {
            get
            {
                return list[index];
            }
            set
            {
                list[index] = value;
            }
        }

        /// <summary>
        /// Invoked when collection is changed.
        /// </summary>
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        /// <summary>
        /// Adds an element to the collection.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public int Add(object value)
        {
            return ilist.Add(value);
        }
        /// <summary>
        /// Determines if the collection contains the specified element.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool Contains(object value)
        {
            return ilist.Contains(value);
        }
        /// <summary>
        /// Returns the index of the first occurrences of a specified element in the collection.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public int IndexOf(object value)
        {
            return ilist.IndexOf(value);
        }
        /// <summary>
        /// Insert an element into the collection at the specified index.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        public void Insert(int index, object value)
        {
            ilist.Insert(index, value);
        }
        /// <summary>
        /// Determines if the collection is fixed size.
        /// </summary>
        public bool IsFixedSize
        {
            get { return ilist.IsFixedSize; }
        }

        /// <summary>
        /// Removes the first occurrence of a specified element from the collection.
        /// </summary>
        /// <param name="value"></param>
        public void Remove(object value)
        {
            ilist.Remove(value);
        }
        /// <summary>
        /// List interface
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        object IList.this[int index]
        {
            get
            {
                return ilist[index];
            }
            set
            {
                ilist[index] = value;
            }
        }
        /// <summary>
        /// Copies the collection to a compatible array.
        /// </summary>
        /// <param name="array"></param>
        /// <param name="index"></param>
        public void CopyTo(Array array, int index)
        {
            ilist.CopyTo(array, index);
        }
        /// <summary>
        /// Determines if the collection is synchronized.
        /// </summary>
        public bool IsSynchronized
        {
            get { return ilist.IsSynchronized; }
        }
        /// <summary>
        /// Gets an object that can be used to synchronize access the collection.
        /// </summary>
        public object SyncRoot
        {
            get { return ilist.SyncRoot; }
        }
    }

    /// <summary>
    /// It is recommended to inherit from this class when implementing component settings. 
    /// This class uses a recurrent template pattern to exhibit a kind of "static reflection".
    /// </summary>
    /// <remarks>
    /// It is also possible implement a component setting by inherriting from the non-generic <see cref="ComponentSettings"/> 
    /// or <see cref="IComponentSettings"/> it is just not recommended.
    /// </remarks>
    /// <typeparam name="T">The inheriting class.</typeparam>
    public abstract class ComponentSettings<T> : ComponentSettings
        where T : ComponentSettings
    {

        ///<summary>
        /// Gets the current setting of a specific type.
        /// </summary>
        /// <returns></returns>
        internal static T GetCurrent()
        {
            return GetCurrent<T>();
        }

        /// <summary>
        /// Get the currently loaded ComponentSettings instance for this class.
        /// </summary>
        public static T Current
        {
            get { return GetCurrent(); }
        }
    }

    /// <summary>
    /// Specifies the ComponentSettings class to be a OpenTAP plugin.
    /// </summary>
    /// <remarks>
    /// It is recommended to iherit from <see cref="ComponentSettings{T}"/> when possible.
    /// </remarks>
    [Display("Component Settings")]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public interface IComponentSettings : ITapPlugin
    {

    }

    /// <summary>
    /// An abstract class to implement general settings for a component. 
    /// This class provides methods to load and save the settings to/from an XML file using TapSerializer.
    /// </summary>
    /// <remarks>
    /// It is recommended to iherit from <see cref="ComponentSettings{T}"/> when possible.
    /// </remarks>
    public abstract class ComponentSettings : ValidatingObject, IComponentSettings
    {
        /// <summary>
        /// Settings group of this settings class.
        /// </summary>
        public string GroupName
        {
            get
            {
                var settingsGroup = GetType().GetAttribute<SettingsGroupAttribute>();
                if (settingsGroup == null) return "";
                return settingsGroup.GroupName;
            }
        }

        bool profile
        {
            get
            {
                var settingsGroup = GetType().GetAttribute<SettingsGroupAttribute>();
                if (settingsGroup == null) return false;
                return settingsGroup.Profile;
            }
        }

        /// <summary>
        /// Invokes when the cache for this settings item is invalidated for this item. The way to handle it is usually to fetch the new instance using ComponentSettings.GetCurrent(sender.GetType()).
        /// </summary>
        public event EventHandler CacheInvalidated;
        
        /// <summary>
        /// Directory root for platform settings.
        /// </summary>
        static string settingsDirectoryRoot = Path.Combine(ExecutorClient.ExeDir, "Settings");

        /// <summary>
        /// Where settings files are located. 
        /// Usually this is at "[Executable location]\Settings", but it can be set to different locations. 
        /// Setting this will invalidate loaded settings.
        /// </summary>
        public static string SettingsDirectoryRoot
        {
            get { return settingsDirectoryRoot; }
            set
            {
                settingsDirectoryRoot = value;
                InvalidateAllSettings();
            }
        }


        private static readonly TraceSource log = Log.CreateSource("Settings");

        static Dictionary<string, string> groupDir = new Dictionary<string, string>();

        /// <summary>
        /// The directory where the settings are loaded from / saved to.
        /// </summary>
        /// <param name="groupName">Name of the settings group.</param>
        /// <param name="isProfile">If the settings group uses profiles, we load the default profile.</param>
        /// <returns></returns>
        public static string GetSettingsDirectory(string groupName, bool isProfile = true)
        {
            if (groupName == null)
                throw new ArgumentNullException("groupName");
            if (isProfile == false)
            {
                return Path.Combine(SettingsDirectoryRoot, groupName);
            }
            if (!groupDir.ContainsKey(groupName))
            {
                var file = Path.Combine(SettingsDirectoryRoot, groupName, "CurrentProfile");

                if (File.Exists(file))
                    groupDir[groupName] =  File.ReadAllText(file);
                else
                    groupDir[groupName] = "Default";
            }

            
            return Path.Combine(SettingsDirectoryRoot, groupName, groupDir[groupName]);
        }

        /// <summary>
        /// Ensures that the Settings directory exists and that the specified groupName sub directory exists. This might throw an exception if the settings directory was configured to something invalid. Like 'AUX', 'NUL', ....
        /// </summary>
        /// <param name="groupName">Name of the settings group.</param>
        /// <param name="isProfile">Determines if the settings group uses profiles.</param>
        public static void EnsureSettingsDirectoryExists(string groupName, bool isProfile = true)
        {
            if (!Directory.Exists(SettingsDirectoryRoot))
                Directory.CreateDirectory(SettingsDirectoryRoot);
            if (!Directory.Exists(GetSettingsDirectory(groupName, isProfile)))
                Directory.CreateDirectory(GetSettingsDirectory(groupName, isProfile));
        }

        /// <summary> Gets or sets if settings groups should be persisted between OpenTAP processes.</summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static bool PersistSettingGroups = true;

        /// <summary>
        /// Sets the directory in which settings groups are loaded from / saved to.
        /// </summary>
        /// <param name="groupName">Name of the settings group.</param>
        /// <param name="profileName">Name of the selected settings profile.</param>
        public static void SetSettingsProfile(string groupName, string profileName)
        {
            if (groupName == null)
                throw new ArgumentNullException("groupName");
            if (profileName == null)
                throw new ArgumentNullException("profileName");

            if (GetSettingsDirectory(groupName) == profileName)
                return;
            
            if (PersistSettingGroups)
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
                File.WriteAllText(currentSettingsFile, FileSystemHelper.GetRelativePath(Path.GetFullPath(Path.Combine(SettingsDirectoryRoot, groupName)), Path.GetFullPath(profileName)));
                File.SetAttributes(currentSettingsFile, FileAttributes.Hidden);

            }
            groupDir[groupName] = profileName;
            InvalidateAllSettings();
        }

        /// <summary>
        /// Invalidates all loaded settings. Next time a ComponentSettings is accessed, it will be read from an XML file.
        /// </summary>
        internal static void InvalidateAllSettings()
        {
            var cachedComponentSettings = loadedComponentSettingsMemorizer.GetResults().Where(x => x != null);

            // Settings can be co-dependent. Example: Connections and Instruments.
            // So we need to invalidate all the settings and then invoke the event.
            foreach (var componentSetting in cachedComponentSettings)
                loadedComponentSettingsMemorizer.Invalidate(componentSetting.GetType());
            foreach (var componentSetting in cachedComponentSettings)
                if (componentSetting.CacheInvalidated != null)
                    componentSetting.CacheInvalidated(componentSetting, new EventArgs());
        }

        readonly static Memorizer<Type, ComponentSettings> loadedComponentSettingsMemorizer = new Memorizer<Type, ComponentSettings>(type => Load(type));


        /// <summary>
        /// Saves the settings held by this class to an XML file in the <see cref="ComponentSettings.SetSettingsProfile(string, string)"/>.
        /// </summary>
        public void Save()
        {
            EnsureSettingsDirectoryExists(GroupName, profile);

            string path = GetSaveFilePath(GetType());
            string dir = Path.GetDirectoryName(path);
            if (dir != "" && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var sw = Stopwatch.StartNew();

            using (var str = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            {
                using (var xmlWriter = System.Xml.XmlWriter.Create(str, new System.Xml.XmlWriterSettings { Indent = true }))
                    new TapSerializer().Serialize(xmlWriter, this);
            }
            log.Debug(sw, "Saved {0} to {1}", this.GetType().Name, path);
        }

        /// <summary>
        /// Saves all ComponentSettings objects that have been loaded using <see cref="ComponentSettings.Load(Type)"/> or <see cref="ComponentSettings.GetCurrent(Type)"/>.
        /// </summary>
        public static void SaveAllCurrentSettings()
        {
            foreach (var comp in loadedComponentSettingsMemorizer.GetResults())
                if(comp != null) comp.Save();
        }

        /// <summary>
        /// Invalidates the cache of this type of component setting.
        /// </summary>
        public void Invalidate()
        {
            loadedComponentSettingsMemorizer.Invalidate(GetType());
        }

        /// <summary>
        /// Forces the reload of this type of component setting from the XML file the next time the setting is used.
        /// </summary>
        public void Reload()
        {
            if (CacheInvalidated != null)
                CacheInvalidated(this, new EventArgs());
        }

        /// <summary>
        /// Called if a new ComponentSettings is instantiated and there are no corresponding settings XML.
        /// </summary>
        public virtual void Initialize()
        {

        }

        /// <summary>
        /// Gets the file where a ComponentSettingsType is saved.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        static public string GetSaveFilePath(Type type)
        {
            if (type == null)
                throw new ArgumentNullException("type");
            var settingsGroup = type.GetAttribute<SettingsGroupAttribute>();

            bool isProfile = settingsGroup == null ? false : settingsGroup.Profile;
            string groupName = settingsGroup == null ? "" : settingsGroup.GroupName;

            return Path.Combine(GetSettingsDirectory(groupName, isProfile), type.GetDisplayAttribute().GetFullName() + ".xml");
        }
        static Queue<TapSerializer> flushers = new Queue<TapSerializer>();
        /// <summary>
        /// Loads a new instance of the settings for a given component.
        /// </summary>
        /// <param name="settingsType">The type of the component settings to load (this type must be a descendant of <see cref="ComponentSettings"/>).</param>
        /// <returns>Returns the settings.</returns>
        static ComponentSettings Load(Type settingsType)
        {

            string path = GetSaveFilePath(settingsType);
            Stopwatch timer = Stopwatch.StartNew();

            ComponentSettings settings = null;
            if (File.Exists(path))
            {
                try
                {
                    using (var str = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        var xdocserializer = new TapSerializer();
                        lock(flushers)
                            flushers.Enqueue(xdocserializer);
                        settings = (ComponentSettings)xdocserializer.Deserialize(str, false, TypeData.FromType(settingsType));
                        
                    }
                }
                catch (Exception ex)
                {
                    if (ex.InnerException != null)
                    {
                        if (ex.InnerException.Message.StartsWith("The specified type was not recognized"))
                            log.Warning("Error loading settings file for {0}. {1}.", settingsType.Name, ex.InnerException.Message);
                        else
                            log.Warning("Error loading settings file for {0}. Is it an old version? A new file will be created with default values.", settingsType.Name);
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
                    log.Error("Could not create '{0}': {1}", settingsType.GetDisplayAttribute().Name, ex.InnerException.Message);
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

                log.Debug(timer, "No settings file exists for {0}. A new instance with default values has been created.", settingsType.Name);
            }

            return settings;
        }

        /// <summary>
        /// Gets current settings for a specified component. This is either an instance of the settings class previously loaded, or a new instance loaded from the associated file. 
        /// </summary>
        /// <typeparam name="T">The type of the component settings requested (this type must be a descendant of <see cref="ComponentSettings"/>).</typeparam>
        /// <returns>Returns the loaded components settings. Null if it was not able to load the settings type.</returns>
        internal static T GetCurrent<T>() where T : ComponentSettings
        {
            return (T)GetCurrent(typeof(T));
        }

        /// <summary>
        /// Gets current settings for a specified component. This is either an instance of the settings class previously loaded, or a new instance loaded from the associated file.
        /// </summary>
        /// <param name="settingsType">The type of the component settings requested (this type must be a descendant of <see cref="ComponentSettings"/>).</param>
        /// <returns>Returns the loaded components settings. Null if it was not able to load the settings type.</returns>
        public static ComponentSettings GetCurrent(Type settingsType)
        {
            lock (flushers)
            {
                if (flushers.Count == 0)
                {
                    var result = loadedComponentSettingsMemorizer.Invoke(settingsType);
                    while (flushers.Count > 0)
                        flushers.Dequeue().Flush();
                    return result;
                }
            }
            return loadedComponentSettingsMemorizer.Invoke(settingsType);
        }

        /// <summary>
        /// Gets current settings for a specified component from cache.
        /// </summary>
        /// <param name="settingsType">The type of the component settings requested (this type must be a descendant of <see cref="ComponentSettings"/>).</param>
        /// <returns>Returns the loaded components settings. Null if it was not able to load the settings type or if it was not cached.</returns>
        public static ComponentSettings GetCurrentFromCache(Type settingsType)
        {
            return loadedComponentSettingsMemorizer.GetCached(settingsType);
        }
    }
}
