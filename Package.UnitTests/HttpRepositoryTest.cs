using System.Net;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenTap.Authentication;

namespace OpenTap.Package.UnitTests;

[TestFixture]
public class HttpRepositoryTest
{

    [Test]
    public void TestTokenWithPort()
    {
        new TokenInfo("asd", "asd", "localhost:18080");
        new TokenInfo("asd", "asd", "localhost");
        new TokenInfo("asd", "asd", "packages.opentap.io");
        new TokenInfo("asd", "asd", "packages.opentap.io:1111");
    }
    
}