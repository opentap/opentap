//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OpenTap
{
    /// <summary> 
    /// Represents a stack of ITypeDataProvider/IStackedTypeDataProvider that is used to get TypeData for a given type. 
    /// The providers on this stack are called in order until a provider returuns a
    /// </summary>
    public class TypeDataProviderStack
    {
        object[] providers;
        int offset = 0;

        internal TypeDataProviderStack()
        {
            offset = 0;
            providers = GetProviders();
        }

        private TypeDataProviderStack(object[] providers, int providerOffset)
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
            while (offset < providers.Length)
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

            if (!failedGetWarnHit)
            {
                // Todo: Change this method to never return null and throw in this case. Just added this warning for now, to see if this ever happens
                log.Warning("Could not get TypeData for {0}", obj.GetType().FullName);
                failedGetWarnHit = true;
            }
            return null;
        }

        static bool failedGetWarnHit = false;
        static TraceSource log = Log.CreateSource("TypeDataProvider");

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
            while (offset < providers.Length)
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

        static object providersCacheLockObj = new object();
        static object[] providersCache = new object[0];
        static readonly HashSet<ITypeData> badProviders = new HashSet<ITypeData>();
        static int lastCount = 0;
        static object[] GetProviders()
        {
            var providers1 = TypeData.FromType(typeof(IStackedTypeDataProvider)).DerivedTypes;
            var providers2 = TypeData.FromType(typeof(ITypeDataProvider)).DerivedTypes;

            int l1 = providers1.Count();
            int l2 = providers2.Count();

            if (lastCount == l1 + l2) return providersCache;

            lock (providersCacheLockObj)
            {
                if (lastCount == l1 + l2) return providersCache;
                Dictionary<object, double> priorities = new Dictionary<object, double>();
                foreach (var providerType in providers1.Concat(providers2).Distinct())
                {
                    if (providerType.CanCreateInstance == false) continue;

                    try
                    {
                        var provider = providerType.CreateInstance();
                        if (provider == null)
                        {
                            throw new Exception(
                                $"Failed to instantiate TypeDataProvider of type '{providerType.Name}'.");
                        }

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
                    catch (Exception e)
                    {
                        while (e is TargetInvocationException te)
                            e = te.InnerException;
                        
                        bool isNewError;
                        lock (badProviders)
                            isNewError = badProviders.Add(providerType);
                        if (isNewError)
                        {
                            log.Error("Unable to use TypeDataProvider of type '{0}' due to errors.", providerType.Name);
                            log.Debug("The error was '{0}'", e.Message);
                            log.Debug(e);
                        }
                    }
                }

                providersCache = priorities.Keys.OrderByDescending(x => priorities[x]).ToArray();
                lastCount = l1 + l2;
            }
            return providersCache;
        }
    }
}
