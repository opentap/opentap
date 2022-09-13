namespace OpenTap.Package
{
    class FileRepositoryCache
    {
        public int CachePackageCount { get; set; }
        public string Hash { get; set; }

        public string CacheFileName => $"{FilePackageRepository.TapPluginCache}.{Hash}.{CachePackageCount}.xml";
    }
}