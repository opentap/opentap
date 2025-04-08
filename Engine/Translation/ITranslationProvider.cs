//            Copyright Keysight Technologies 2012-2025
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using System.IO;
using System.Collections.Immutable;
using System.Linq;
using System;
namespace OpenTap.Package.Translation;

internal interface ITranslationProvider
{
    public IEnumerable<CultureInfo> SupportedLanguages();
    public DisplayAttribute GetDisplayAttribute(IReflectionData mem, CultureInfo culture);
}
