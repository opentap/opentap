using NUnit.Framework;
using OpenTap.Cli;
using System;
using System.Linq;


namespace OpenTap.UnitTests
{
    [TestFixture]
    public class ArgumentsParserTest
    {
        [Test]
        [TestCase("arg1",      "arg1")]
        [TestCase("a1 a2 a3",  "a1 a2 a3")]
        [TestCase("--color",   "-color")]
        [TestCase("-color",    "-color")]
        [TestCase("--c",       "-color")]
        [TestCase("-c",        "-color")]
        [TestCase("--list 10", "-list=10")]
        [TestCase("-list val", "-list=val")]
        [TestCase("--l 10",    "-list=10")]
        [TestCase("-l 10",     "-list=10")]
        [TestCase("--list=10", "-list=10")]
        [TestCase("-list=10",  "-list=10")]
        [TestCase("--l=10",    "-list=10")]
        [TestCase("-l=val",    "-list=val")]
        [TestCase(   "--c -verbose -h --list=10 -s val",                "-color -verbose -help -list=10 -save=val")]
        [TestCase("a1 --c -verbose a2 -h --list=10 -s val a3",          "a1 a2 a3 -color -verbose -help -list=10 -save=val")]
        [TestCase("a1 --c -verbose a2 -h --list=10 -s val a3 -missing", "a1 a2 a3 -color -verbose -help -list=10 -save=val")]
        public void CliArgumentParsing(string arguments, string check)
        {
            string [] checkArray = check.Split(' ');
            ArgumentsParser ap = new ArgumentsParser();

            ap.AllOptions.Add("help", 'h', false, "Write help information.");
            ap.AllOptions.Add("verbose", 'v', false, "Show verbose/debug level log messages.");
            ap.AllOptions.Add("color", 'c', false, "Color messages according to their level.");
            ap.AllOptions.Add("list", 'l', true, "List <arg> lines.");
            ap.AllOptions.Add("save", 's', true, "Save <arg> lines.");

            ArgumentCollection args = ap.Parse(arguments.Split(' '));

            Assert.AreEqual(checkArray.Length, args.Count() + args.UnnamedArguments.Count(),
                "Found only {0} option(s) and {1} unnamed argument(s) instead of {2} option(s) + argument(s)",
                    args.Count(), args.UnnamedArguments.Count(), checkArray.Length);

            foreach (string c in checkArray)
            {
                if(!c.StartsWith("-"))
                {
                    Assert.IsTrue(args.UnnamedArguments.Contains(c),
                        "Expected unnamed argument '{0}' not found", c);
                }
                else
                {
                    int i = c.IndexOf('=');
                    if (i > 0)
                    {
                        string expectedOpt = c.Substring(1, i - 1);
                        string expectedArg = c.Substring(i + 1);

                        Assert.IsTrue(args.Contains(expectedOpt),
                            "Expected option '{0}' not found", expectedOpt);
                        Argument arg = args[expectedOpt];

                        Assert.AreEqual(1, arg.Values.Count(),
                            "Expected argument for '{0}' not found", expectedOpt);
                        Assert.AreEqual(expectedArg, arg.Value,
                            "Argument for '{0}' is '{1}' instead of '{2}'",
                                expectedOpt, arg.Value, expectedArg);
                    }
                    else
                        Assert.IsTrue(args.Contains(c.Substring(1)), "Expected option '{0}' not found", c.Substring(1));
                }
            }
        }

        [Test]
        [TestCase("-l")]
        [TestCase("--l")]
        [TestCase("-list")]
        [TestCase("--list")]
        [TestCase("a1 a2 a3 --missing -h --list")]
        public void MissingCliOptionArgumentsAreReported(string arguments)
        {
            ArgumentsParser ap = new ArgumentsParser();

            ap.AllOptions.Add("help", 'h', false, "Write help information.");
            ap.AllOptions.Add("list", 'l', true, "List <arg> lines.");
            ap.AllOptions.Add("save", 's', true, "Save <arg> lines.");

            ArgumentCollection args = ap.Parse(arguments.Split(' '));

            Assert.AreEqual(1, args.MissingArguments.Count(),
                "No option argument is missing");
            Assert.AreEqual("list", args.MissingArguments.First().LongName,
                "'list' is not missing any argument");
        }

        [Test]
        [TestCase("-k",       "-k")]
        [TestCase("--k",      "--k")]
        [TestCase("-missing", "-missing")]
        [TestCase("--miss",   "--miss")]
        [TestCase("-miss --bliss -k --j",            "-miss --bliss -k --j")]
        [TestCase("a1 a2 a3 -h --missing -list val", "--missing")]
        [TestCase("a1 a2 a3 -h --missing --list",    "--missing")]
        public void UnknownOptionsAreReported(string arguments, string check)
        {
            string [] checkArray = check.Split(' ');
            ArgumentsParser ap = new ArgumentsParser();

            ap.AllOptions.Add("help", 'h', false, "Write help information.");
            ap.AllOptions.Add("list", 'l', true, "List <arg> lines.");

            ArgumentCollection args = ap.Parse(arguments.Split());

            foreach (string c in checkArray)
            {
                Assert.IsTrue(args.UnknownsOptions.Contains(c),
                    "Option '{0}' is not unknown", c);
            }
        }
   }
}
