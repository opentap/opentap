namespace OpenTap
{
    /// <summary>
    /// The status of the loading operation for TypeData and AssemblyData.
    /// </summary>
    internal enum LoadStatus
    {
        /// <summary> Loading has not been done yet. </summary>
        NotLoaded = 1,
        /// <summary> This has been loaded. </summary>
        Loaded = 2,
        /// <summary> It failed to load. </summary>
        FailedToLoad = 3
    }
}