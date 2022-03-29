using System;
using System.ComponentModel;
using System.Net.Http;
using System.Threading;
using OpenTap.Cli;

namespace OpenTap.Authentication
{
    [Display("get", Group: "auth")]
    [Browsable(false)]
    class WebRequestAction : ICliAction
    {
        private static TraceSource log = Log.CreateSource("web");

        [UnnamedCommandLineArgument("request", Required = true)]
        public string Request { get; set; }

        public int Execute(CancellationToken cancellationToken)
        {
            this.MustBeDefined(nameof(Request));
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