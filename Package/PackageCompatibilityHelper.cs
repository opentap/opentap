namespace OpenTap.Package
{
    static class PackageCompatibilityHelper
    {
        /// <summary>
        /// Returns true if this specifier can be satisfied by the given version. Really the same behavior as VersionSpecifier.IsCompatible, just with a better name.
        /// </summary>
        public static bool IsSatisfiedBy(this VersionSpecifier spec, VersionSpecifier other)
        {
            if (spec == VersionSpecifier.Any) return true;
            if (other == VersionSpecifier.Any) return false;
            SemanticVersion semanticVersion = new SemanticVersion(other.Major ?? 0, other.Minor ?? 0, other.Patch ?? 0, other.PreRelease, other.BuildMetadata);
            if (other.Patch == null || other.Minor == null)
            {
                var specMajor = other.Major.HasValue ? spec.Major : null;
                var specMinor = other.Minor.HasValue ? spec.Minor : null;
                var specPatch = other.Patch.HasValue ? spec.Patch : null;
                
                // This is probably not needed, but a required invariant for version specifiers.
                if (specMajor == null) specMinor = null;
                if (specMinor == null) specPatch = null;
                // Add AnyPrerelease to 'Compatible' match.
                // otherwise e.g ^9.18.0 is not satisfied by ^9.18.1-rc.
                var versionMatchBehavior = spec.MatchBehavior == VersionMatchBehavior.Compatible
                    ? (spec.MatchBehavior | VersionMatchBehavior.AnyPrerelease)
                    : spec.MatchBehavior;

                var spec2 = new VersionSpecifier(specMajor, specMinor, specPatch, spec.PreRelease, spec.BuildMetadata,
                    versionMatchBehavior);
                return spec2.IsCompatible(semanticVersion);
            }
            var ok = spec.IsCompatible(semanticVersion);

            return ok;
        }

        public static bool IsSuperSetOf(this VersionSpecifier spec, VersionSpecifier other)
        {
            if (!spec.IsSatisfiedBy(other)) return false;
            if (spec == other) return true;
            if (spec == VersionSpecifier.Any) return true;
            if (other == VersionSpecifier.Any) return false;
            if (spec.Major.HasValue == false && other.Major.HasValue) return true;
            if (spec.Minor.HasValue == false && other.Minor.HasValue) return true;
            if (spec.Patch.HasValue == false && other.Patch.HasValue) return true;
            return false;
        }

        public static VersionSpecifier AsCompatibleSpecifier(this SemanticVersion semver)
        {
            return new VersionSpecifier(semver, VersionMatchBehavior.Compatible);
        }
        public static VersionSpecifier AsExactSpecifier(this SemanticVersion semver)
        {
            return new VersionSpecifier(semver, VersionMatchBehavior.Exact);
        }
    }
}