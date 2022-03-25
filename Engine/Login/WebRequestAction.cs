using System;
using System.ComponentModel;
using System.Net.Http;
using System.Threading;
using OpenTap.Cli;

namespace OpenTap.Login
{
    [Display("get", Group: "web")]
    [Browsable(false)]
    public class WebRequestAction : ICliAction
    {
        private static TraceSource log = Log.CreateSource("web");

        [UnnamedCommandLineArgument("url", Required = true)]
        public string Request { get; set; }

        public int Execute(CancellationToken cancellationToken)
        {
            var cli = new HttpClient(LoginInfo.GetClientHandler());
            var response = cli.GetAsync(Request, cancellationToken).Result;
            var content = response.Content.ReadAsStringAsync().Result;
            if (response.IsSuccessStatusCode == false)
                throw new Exception("Unable to connect: " + response.StatusCode + content);
            log.Info("Response: {0}", content);
            return 0;
        }
    }
}