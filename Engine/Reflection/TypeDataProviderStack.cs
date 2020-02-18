//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap
{
    /// <summary> 
    /// Represents a stack of ITypeDataProvider/IStackedTypeDataProvider that is used to get TypeData for a given type. 
    /// The providers on this stack are called in order until a provider returuns a
    /// </summary>
    public class TypeDataProviderStack
    {
        List<object> providers;
        int offset = 0;

        internal TypeDataProviderStack()
        {
            offset = 0;
            providers = GetProviders();
        }

        private TypeDataProviderStack(List<object> providers, int providerOffset)
        {
            this.providers = providers;
            this.offset = providerOffset;
        }

        /// <summary> Gets the type data from an object. </summary>
        /// <param name="obj">The object to get type information for.</param>
        /// <returns>A representation of the type of the specified object or null if no providers can handle the specified type of object.</returns>
        public ITypeData GetTypeData(object obj)
        {
            if (obj == null)
                return null;
            while (offset < providers.Count)
            {
                var provider = providers[offset];
                offset++;
                try
                {
                    if (provider is IStackedTypeDataProvider sp)
                    {
                        var newStack = new TypeDataProviderStack(providers, offset);
                        if (sp.GetTypeData(obj, newStack) is ITypeData found)
                            return found;
                    }
                    else if (provider is ITypeDataProvider p)
                    {
                        if (p.GetTypeData(obj) is ITypeData found)
                            return found;
                    }
                }
                catch (Exception error)
                {
                    logProviderError(provider, error);
                }
            }

            return null;
        }

        static void logProviderError(object provider, Exception error)
        {
            var log = Log.CreateSource(provider.GetType().Name);
            log.Error("Unhandled error occured in type resolution: {0}", error.Message);
            log.Debug(error);
        }

        /// <summary> Gets the type data from an identifier. </summary>
        /// <param name="identifier">The identifier to get type information for.</param>
        /// <returns>A representation of the type specified by identifier or null if no providers can handle the specified identifier.</returns>
        public ITypeData GetTypeData(string identifier)
        {
            if (identifier == null) return null;
            while (offset < providers.Count)
            {
                var provider = providers[offset];
                offset++;
                try
                {
                    if (provider is IStackedTypeDataProvider sp)
                    {
                        var newStack = new TypeDataProviderStack(providers, offset);
                        if (sp.GetTypeData(identifier, newStack) is ITypeData found)
                            return found;
                    }
                    else if (provider is ITypeDataProvider p)
                    {
                        if (p.GetTypeData(identifier) is ITypeData found)
                            return found;
                    }
                }
                catch (Exception error)
                {
                    logProviderError(provider, error);
                }
            }

            return null;
        }

        static List<object> providersCache = new List<object>();
        static readonly HashSet<ITypeData> badProviders = new HashSet<ITypeData>();

        static List<object> GetProviders()
        {
            var providerTypes = TypeData.FromType(typeof(IStackedTypeDataProvider)).DerivedTypes;
            providerTypes = providerTypes.Concat(TypeData.FromType(typeof(ITypeDataProvider)).DerivedTypes).Distinct();
            if (providersCache.Count + badProviders.Count == providerTypes.Count()) return providersCache;
            Dictionary<object, double> priorities = new Dictionary<object, double>();
            
            foreach (var providerType in providerTypes)
            {
                if (providerType.CanCreateInstance == false) continue;
                
                try
                {
                    var provider = providerType.CreateInstance();
                    double priority;

                    if (provider is IStackedTypeDataProvider p)
                        priority = p.Priority;
                    else if (provider is ITypeDataProvider p2)
                        priority = p2.Priority;
                    else
                    {
                        lock (badProviders)
                        {
                            if (badProviders.Contains(providerType))
                                continue; // error was printed first time, so just continue.
                        }

                        throw new InvalidOperationException("Unreachable code path executed.");
                    }
                    priorities.Add(provider, priority);
                }
                catch(Exception e)
                {
                    bool isNewError = false;
                    lock(badProviders)
                        isNewError = badProviders.Add(providerType);
                    if (isNewError)
                    {
                        var log = Log.CreateSource("TypeDataProvider");
                        log.Error("Unable to use TypeDataProvider of type '{0}' due to errors.", providerType.Name);
                        log.Debug("The error was '{0}'", e.Message);
                        log.Debug(e);
                    }
                }
            }

            providersCache = priorities.Keys.OrderByDescending(x => priorities[x]).ToList();
            return providersCache;
        }
    }
}
