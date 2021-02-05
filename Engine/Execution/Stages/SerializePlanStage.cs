//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace OpenTap
{
    class SerializePlanStage : TestPlanExecutionStageBase
    {
        internal static readonly TraceSource log = OpenTap.Log.CreateSource("Executor(SP)");

        public CreateRunStage CreateRun { get; set; }

        protected override void Execute(TestPlanExecutionContext context)
        {
            TestPlan plan = context.Plan;
            TestPlanRun run = CreateRun.execStage;

            if (context.currentExecutionState != null)
            {
                using (var memstr = new MemoryStream(128))
                {
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        plan.Save(memstr);
                        var testPlanBytes = memstr.ToArray();
                        run.TestPlanXml = Encoding.UTF8.GetString(testPlanBytes);
                        run.Parameters.Add(new ResultParameter("Test Plan", nameof(run.Hash), GetHash(testPlanBytes), new MetaDataAttribute(), 0));
                        log.Debug(sw, "Saved Test Plan XML");
                    }
                    catch (Exception e)
                    {
                        log.Warning("Unable to XML serialize test plan.");
                        log.Debug(e);
                    }
                }
            }
            else
            {
                if (run.TestPlanXml != null)
                {
                    run.Parameters.Add("Test Plan", nameof(run.Hash), GetHash(Encoding.UTF8.GetBytes(run.TestPlanXml)), new MetaDataAttribute());
                    return;
                }

                if (plan.GetCachedXml() is byte[] xml)
                {

                    if (!testPlanHashMemory.TryGetValue(plan, out var pair))
                    {
                        if (pair == null)
                        {
                            pair = new StringHashPair();
                            testPlanHashMemory.Add(plan, pair);
                        }
                    }


                    if (Equals(pair.Bytes, xml) == false)
                    {
                        pair.Xml = Encoding.UTF8.GetString(xml);
                        pair.Hash = GetHash(xml);
                        pair.Bytes = xml;
                        run.TestPlanXml = pair.Xml;
                    }
                    else
                        run.TestPlanXml = pair.Xml;

                    run.Parameters.Add("Test Plan", nameof(run.Hash), pair.Hash, new MetaDataAttribute());
                    return;
                }

                using (var memstr = new MemoryStream(128))
                {
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        plan.Save(memstr);
                        var testPlanBytes = memstr.ToArray();
                        run.TestPlanXml = Encoding.UTF8.GetString(testPlanBytes);

                        run.Parameters.Add(new ResultParameter("Test Plan", nameof(run.Hash), GetHash(testPlanBytes),
                            new MetaDataAttribute(), 0));
                        log.Debug(sw, "Saved Test Plan XML");
                    }
                    catch (Exception e)
                    {
                        log.Warning("Unable to XML serialize test plan.");
                        log.Debug(e);
                    }
                }
            }
        }


        class StringHashPair
        {
            public string Xml { get; set; }
            public string Hash { get; set; }
            public byte[] Bytes { get; set; }
        }

        /// <summary> Memorizer for storing pairs of Xml and hash. </summary>
        static ConditionalWeakTable<TestPlan, StringHashPair> testPlanHashMemory = new ConditionalWeakTable<TestPlan, StringHashPair>();

        private string GetHash(byte[] testPlanXml)
        {
            using (var algo = System.Security.Cryptography.SHA1.Create())
                return BitConverter.ToString(algo.ComputeHash(testPlanXml), 0, 8).Replace("-", string.Empty);
        }
    }
}
