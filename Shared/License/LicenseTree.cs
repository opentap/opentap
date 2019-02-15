//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenTap
{
    internal abstract class LicenseBase
    {
        internal abstract bool Matches(HashSet<string> licenses);
        internal abstract IEnumerable<LicenseBase> FindMatch(HashSet<string> licenses);

        internal static string FormatFriendly(string licenseString)
        {
            return PrintLicense(Prune(LicenseParser.ParseString(licenseString)));
        }

        private static LicenseBase Prune(LicenseBase real)
        {
            if (real is LicenseAll)
            {
                var all = (real as LicenseAll).Licenses.Select(Prune).ToArray();

                if (all.Any(l => l == null))
                    return null;

                all = all.Where(l => !(l is LicenseProcess)).ToArray();

                if (all.Length == 1)
                    return all.First();
                else if (all.Length == 0)
                    return null;
                else
                    return new LicenseAll(all);
            }
            else if (real is LicenseAny)
            {
                var all = (real as LicenseAny).Licenses.Select(Prune).Where(x => x != null).ToArray();

                if (all.OfType<LicenseProcess>().Any())
                    return all.OfType<LicenseProcess>().First();

                if (all.Length == 1)
                    return all.First();
                else if (all.Length == 0)
                    return null;
                else
                    return new LicenseAny(all);
            }
            else if (real is LicenseProcess)
            {
                if ((real as LicenseProcess).ProcessName.ToLowerInvariant().Contains("keysight.opentap"))
                    return real;
                else
                    return null;
            }

            return real;
        }

        private static string PrintLicense(LicenseBase license)
        {
            if (license is LicenseAll)
            {
                return string.Join(" and ", (license as LicenseAll).Licenses.Select(l =>
                {
                    string output = PrintLicense(l);
                    if (l is LicenseAny)
                        output = "(" + output + ")";
                    return output;
                }
                ).Where(x => x != ""));
            }
            else if (license is LicenseAny)
            {
                return string.Join(" or ", (license as LicenseAny).Licenses.Select(l =>
                {
                    string output = PrintLicense(l);
                    if (l is LicenseAll)
                        output = "(" + output + ")";
                    return output;
                }
                ).Where(x => x != ""));
            }
            else if (license is LicenseRequired)
            {
                string featureSeed = (license as LicenseRequired).FeatureSeed;
                return featureSeed.Contains("-INT") ? "" : featureSeed;
            }

            return "";
        }
    }

    internal class LicenseRequired : LicenseBase
    {
        internal readonly string FeatureSeed;

        internal override bool Matches(HashSet<string> licenses)
        {
            return licenses.Contains(FeatureSeed);
        }

        internal override IEnumerable<LicenseBase> FindMatch(HashSet<string> licenses)
        {
            if (Matches(licenses))
                yield return this;
        }

        public LicenseRequired(string text)
        {
            FeatureSeed = text;
        }
    }

    internal class LicenseProcess : LicenseBase
    {
        internal readonly string ProcessName;

        internal override bool Matches(HashSet<string> licenses)
        {
            return ProcessName.ToLowerInvariant().Contains("keysight.opentap");
        }

        internal override IEnumerable<LicenseBase> FindMatch(HashSet<string> licenses)
        {
            if (Matches(licenses))
                yield return this;
        }

        public LicenseProcess(string processName)
        {
            ProcessName = processName;
        }
    }

    internal class LicenseAny : LicenseBase
    {
        internal readonly LicenseBase[] Licenses;

        internal override bool Matches(HashSet<string> licenses)
        {
            return Licenses.Any(l => l.Matches(licenses));
        }

        internal override IEnumerable<LicenseBase> FindMatch(HashSet<string> licenses)
        {
            var l = Licenses.FirstOrDefault(lic => lic.Matches(licenses));

            if (l != null)
                return l.FindMatch(licenses);
            else
                return Enumerable.Empty<LicenseBase>();
        }

        public LicenseAny(LicenseBase[] licenseBase)
        {
            Licenses = licenseBase;
        }
    }

    internal class LicenseAll : LicenseBase
    {
        internal readonly LicenseBase[] Licenses;

        internal override bool Matches(HashSet<string> licenses)
        {
            return Licenses.All(l => l.Matches(licenses));
        }

        internal override IEnumerable<LicenseBase> FindMatch(HashSet<string> licenses)
        {
            if (Matches(licenses))
                return Licenses.SelectMany(l => l.FindMatch(licenses));
            else
                return Enumerable.Empty<LicenseBase>();
        }

        public LicenseAll(LicenseBase[] licenseBase)
        {
            Licenses = licenseBase;
        }
    }
}
