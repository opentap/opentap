//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using System.Reflection;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Collections;
using System.IO;

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
        public string GroupName { get; }
        /// <summary>
        /// Specifies whether this settings group uses profiles.  
        /// </summary>
        public bool Profile { get; }

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

    internal interface IComponentSettingsList : IList
    {
        /// <summary> Return the objects which have been removed but still alive (non-GC'd) resources.
        /// This requires using a list of WeakReferences.</summary>
        IResource[] GetRemovedAliveResources();
    }

    ///<summary>
    /// Contains some extra functionality for the ComponentSettingsList.
    /// Created so that it is possible to know which (generic) ComponentSettingsList
    /// contains a given type.
    /// </summary>
    public static class ComponentSettingsList
    {
        static Dictionary<Type, Type> typeHandlersCache;

        static Dictionary<Type, Type> typeHandlers
        {
            get
            {
                if (typeHandlersCache == null)
                {
                    typeHandlersCache = new Dictionary<Type, Type>();
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
                                    typeHandlersCache[types[1]] = types[0];
                                    break;
                                }
                            }
                        }
                    }
                }
                return typeHandlersCache;
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
            foreach (Type key in typeHandlers.Keys)
            {
                if (T.DescendsTo(key))
                {

                    Type compSetType = typeHandlers[key];
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
    public abstract class ComponentSettingsList<DerivedType, ContainedType> : ComponentSettings<DerivedType>, 
        INotifyCollectionChanged, IList, IList<ContainedType>, IComponentSettingsList
        where DerivedType : ComponentSettingsList<DerivedType, ContainedType>
    {
        readonly ObservableCollection<ContainedType> list;
        readonly IList ilist;

        // Keep track of all the still living, but previously touched objects.
        // this is only used if ContainedType is a resource type.
        private readonly WeakHashSet<IResource> touchedResources = new WeakHashSet<IResource>(); 

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
        public static T GetDefaultOf<T>() where T : ContainedType => GetCurrent<DerivedType>().OfType<T>().FirstOrDefault();
        
        /// <summary>
        /// Initializes the list.
        /// </summary>
        public ComponentSettingsList()
        {
            list = new ObservableCollection<ContainedType>();
            list.CollectionChanged += list_CollectionChanged;
            ilist = list;
        }

        IResource[] IComponentSettingsList.GetRemovedAliveResources()
        {
            return touchedResources.GetElements().Where(x => Contains(x) == false).ToArray();
        }

        void list_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                var _newItem = e.NewItems.Cast<ContainedType>().First();
                if (_newItem is IResource newItem)
                {
                    this.touchedResources.Add(newItem);
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
            CollectionChanged?.Invoke(sender, e);
            OnPropertyChanged(nameof(Count));
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
        internal static T GetCurrent() => GetCurrent<T>();

        /// <summary>
        /// Get the currently loaded ComponentSettings instance for this class.
        /// </summary>
        public static T Current =>  GetCurrent();
    }

    /// <summary>
    /// Specifies the ComponentSettings class to be a OpenTAP plugin.
    /// </summary>
    /// <remarks>
    /// It is recommended to inherit from <see cref="ComponentSettings{T}"/> when possible.
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
    /// It is recommended to inherit from <see cref="ComponentSettings{T}"/> when possible.
    /// </remarks>
    public abstract class ComponentSettings : ValidatingObject, IComponentSettings
    {
        internal XmlError[] loadErrors;
        string groupName;
        /// <summary> Settings group of this settings class. </summary>
        public string GroupName
        {
            get
            {
                if (groupName != null) return groupName;
                var settingsGroup = GetType().GetAttribute<SettingsGroupAttribute>();
                groupName = settingsGroup == null ? "" : settingsGroup.GroupName;

                return groupName;
            }
        }

        internal bool profile
        {
            get
            {
                var settingsGroup = GetType().GetAttribute<SettingsGroupAttribute>();
                if (settingsGroup == null) return false;
                return settingsGroup.Profile;
            }
        }

        /// <summary>
        /// Invokes when the cache for this settings item is invalidated for this item.
        /// The way to handle it is usually to fetch the new instance using ComponentSettings.GetCurrent(sender.GetType()).
        /// </summary>
        public event EventHandler CacheInvalidated;
        /// <summary>
        /// Where settings files are located. 
        /// Usually this is at "[Executable location]\Settings", but it can be set to different locations. 
        /// Setting this will invalidate loaded settings.
        /// </summary>
        public static string SettingsDirectoryRoot
        {
            get => context.SettingsDirectoryRoot;
            set => context.SettingsDirectoryRoot = value;
        }

        /// <summary> The directory where the settings are loaded from / saved to. </summary>
        /// <param name="groupName">Name of the settings group.</param>
        /// <param name="isProfile">If the settings group uses profiles, we load the default profile.</param>
        public static string GetSettingsDirectory(string groupName, bool isProfile = true) =>
            context.GetSettingsDirectory(groupName, isProfile);

        /// <summary>
        /// Ensures that the Settings directory exists and that the specified groupName sub directory exists.
        /// This might throw an exception if the settings directory was configured to something invalid. Like 'AUX', 'NUL', ....
        /// </summary>
        /// <param name="groupName">Name of the settings group.</param>
        /// <param name="isProfile">Determines if the settings group uses profiles.</param>
        public static void EnsureSettingsDirectoryExists(string groupName, bool isProfile = true) =>
            context.EnsureSettingsDirectoryExists(groupName, isProfile);
        /// <summary> Gets or sets if settings groups should be persisted between processes.</summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static bool PersistSettingGroups = true;

        /// <summary> Sets the directory in which settings groups are loaded from / saved to. </summary>
        /// <param name="groupName">Name of the settings group.</param>
        /// <param name="profileName">Name of the selected settings profile.</param>
        public static void SetSettingsProfile(string groupName, string profileName) =>
            context.SetSettingsProfile(groupName, profileName);

        /// <summary> Invalidates all loaded settings. Next time a ComponentSettings is accessed, it will be read from an XML file. </summary>
        internal static void InvalidateAllSettings() => context.InvalidateAllSettings();
        
        
        static ComponentSettingsContext context => sessionContext.Value;
        static readonly SessionLocal<ComponentSettingsContext> sessionContext = new SessionLocal<ComponentSettingsContext>(new ComponentSettingsContext());

        internal static void BeginSession()
        {
            var currentContext = context;
            var nextContext = currentContext.Clone();
            nextContext.readOnlyContext = true;
            sessionContext.Value = nextContext;
        }
        
        /// <summary>
        /// Saves the settings held by this class to an XML file in the <see cref="ComponentSettings.SetSettingsProfile(string, string)"/>.
        /// </summary>
        public void Save() => context.Save(this);

        /// <summary> Saves all ComponentSettings objects that have been loaded. </summary>
        public static void SaveAllCurrentSettings() => context.SaveAllCurrentSettings();

        /// <summary>
        /// Invalidates the cache of this type of component setting.
        /// </summary>
        public void Invalidate() => context.Invalidate(GetType());

        /// <summary>
        /// Forces the reload of this type of component setting from the XML file the next time the setting is used.
        /// </summary>
        public void Reload() => context.Reload();

        /// <summary>
        /// Called if a new ComponentSettings is instantiated and there are no corresponding settings XML.
        /// </summary>
        public virtual void Initialize()
        {

        }

        /// <summary> Gets the current file location where a ComponentSettings type is saved. </summary>
        /// <param name="type"> Must be a ComponentSettings sub-class. </param>
        /// <returns></returns>
        public static string GetSaveFilePath(Type type) =>  context.GetSaveFilePath(type);

        /// <summary>
        /// Gets current settings for a specified component. This is either an instance of the settings class previously loaded, or a new instance loaded from the associated file. 
        /// </summary>
        /// <typeparam name="T">The type of the component settings requested (this type must be a descendant of <see cref="ComponentSettings"/>).</typeparam>
        /// <returns>Returns the loaded components settings. Null if it was not able to load the settings type.</returns>
        internal static T GetCurrent<T>() where T : ComponentSettings => (T)GetCurrent(typeof(T));

        /// <summary>
        /// Gets current settings for a specified component. This is either an instance of the settings class previously loaded, or a new instance loaded from the associated file.
        /// </summary>
        /// <param name="settingsType">The type of the component settings requested (this type must be a descendant of <see cref="ComponentSettings"/>).</param>
        /// <returns>Returns the loaded components settings. Null if it was not able to load the settings type.</returns>
        public static ComponentSettings GetCurrent(Type settingsType) =>
            context.GetCurrent(settingsType);

        /// <summary>
        /// Sets current settings for a component setting based on a stream of the file contents of a ComponentSettings XML file.
        /// </summary>
        /// <exception cref="InvalidDataException">If the input stream is not valid XML</exception>
        /// <param name="xmlFileStream">The component settings stream to be set</param>
        /// <returns></returns>
        public static void SetCurrent(Stream xmlFileStream) => context.SetCurrent(xmlFileStream);

        /// <summary>
        /// Sets current settings for a component setting based on a stream of the file contents of a ComponentSettings XML file.
        /// </summary>
        /// <param name="xmlFileStream">The component settings stream to be set</param>
        /// <param name="errors">Any XML errors that occurred during deserialization</param>
        /// <returns></returns>
        public static void SetCurrent(Stream xmlFileStream, out IEnumerable<XmlError> errors) => context.SetCurrent(xmlFileStream, out errors);

        /// <summary>
        /// Gets current settings for a specified component. This is either an instance of the settings class previously loaded, or a new instance loaded from the associated file.
        /// </summary>
        /// <param name="settingsType">The type of the component settings requested (this type must be a descendant of <see cref="ComponentSettings"/>).</param>
        /// <returns>Returns the loaded components settings. Null if it was not able to load the settings type.</returns>
        public static ComponentSettings GetCurrent(ITypeData settingsType)
        {
            var td = settingsType.AsTypeData()?.Type;
            // in some rare cases, the type can be null
            // for example if the assembly could not be loaded(32/64bit incompatible), but it could be reflected.
            // in this case just return null.
            if (td == null)
                return null;
            return context.GetCurrent(td);
        }
        
        /// <summary>
        /// Gets current settings for a specified component from cache.
        /// </summary>
        /// <param name="settingsType">The type of the component settings requested (this type must be a descendant of <see cref="ComponentSettings"/>).</param>
        /// <returns>Returns the loaded components settings. Null if it was not able to load the settings type or if it was not cached.</returns>
        public static ComponentSettings GetCurrentFromCache(Type settingsType) =>
            context.GetCurrentFromCache(settingsType);

        internal void InvokeInvalidate()
        {
            CacheInvalidated?.Invoke(this, EventArgs.Empty);
        }
    }
}
