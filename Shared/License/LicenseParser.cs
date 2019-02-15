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
    internal class LicenseParser
    {
        internal static LicenseBase ParseString(string licenseString)
        {
            Scanner sc = new Scanner(licenseString);

            if (sc.Tokens.Any(t => t.Token == LicenseToken.Error))
                throw new Exception(string.Format("Error while parsing license string at position {0}.", sc.Tokens.First(t => t.Token == LicenseToken.Error).Position));

            var license = ParseLicensesAny(sc.Tokens);

            if (sc.Tokens.Count > 0)
                throw new Exception(string.Format("Invalid license string. Unepxected token: {0}", sc.Tokens[0].Token));

            return license;

            /*
                Rule = LicensesAny .
                LicensesAny = LicensesAll { '| LicensesAll } .
                LicensesAll = License { '&' License } .
                License = Identifier | Process | "<Any Identifier>" | '(' LicensesAny ')' .
             */
        }

        private static LicenseBase ParseLicensesAny(List<TokenEntry> tokens)
        {
            LicenseBase la = ParseLicensesAll(tokens);

            if ((tokens.Count > 0) && (tokens[0].Token == LicenseToken.Or))
            {
                var lic = new List<LicenseBase>() { la };

                while ((tokens.Count > 0) && (tokens[0].Token == LicenseToken.Or))
                {
                    tokens.RemoveAt(0);

                    lic.Add(ParseLicensesAll(tokens));
                }

                return new LicenseAny(lic.ToArray());
            }
            else
                return la;
        }

        private static LicenseBase ParseLicensesAll(List<TokenEntry> tokens)
        {
            LicenseBase la = ParseLicense(tokens);

            if ((tokens.Count > 0) && (tokens[0].Token == LicenseToken.And))
            {
                var lic = new List<LicenseBase>() { la };

                while ((tokens.Count > 0) && (tokens[0].Token == LicenseToken.And))
                {
                    tokens.RemoveAt(0);

                    lic.Add(ParseLicense(tokens));
                }

                return new LicenseAll(lic.ToArray());
            }
            else
                return la;
        }

        private static LicenseBase ParseLicense(List<TokenEntry> tokens)
        {
            if (tokens.Count <= 0) throw new Exception("Failed to get license.");

            if (tokens[0].Token == LicenseToken.Identifier)
            {
                var t = tokens[0];
                tokens.RemoveAt(0);
                return new LicenseRequired(t.Text);
            }
            else if (tokens[0].Token == LicenseToken.Process)
            {
                var t = tokens[0];
                tokens.RemoveAt(0);
                return new LicenseProcess(t.Text);
            }
            else if (tokens[0].Token == LicenseToken.LParan)
            {
                tokens.RemoveAt(0);
                var t = ParseLicensesAny(tokens);
                tokens.RemoveAt(0);

                return t;
            }
            else
                throw new Exception("Failed to parse license string.");
        }
    }
}
