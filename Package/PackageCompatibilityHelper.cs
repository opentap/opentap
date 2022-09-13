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
                var spec2 = new VersionSpecifier(other.Major.HasValue ? spec.Major : 0,
                    other.Minor.HasValue ? spec.Minor : 0
                    
                    , other.Patch.HasValue ? spec.Patch : (spec.Patch.HasValue ? (int?)0 : null), spec.PreRelease, spec.BuildMetadata,
                    // Add AnyPrerelease to 'Compatible' match.
                    // otherwise e.g ^9.18.0 is not satisfied by ^9.18.1-rc.
                    spec.MatchBehavior == VersionMatchBehavior.Compatible ? (spec.MatchBehavior | VersionMatchBehavior.AnyPrerelease) : spec.MatchBehavior);
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