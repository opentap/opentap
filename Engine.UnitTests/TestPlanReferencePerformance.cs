using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;
using System.Xml.Serialization;

namespace OpenTap.UnitTests
{
    public class TestPlanReferencePerformance
    {
        [Test, Ignore("For performance testing only")]
        public void SaveLoadPerformance()
        {
            
            for (int j = 1; j < 20; j++)
            {
                int innerSteps = j * 5;
                int outerSteps = 10;
                var innerPlanName = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".TapPlan");
                var outerPlanName = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".TapPlan");
                {
                    var innerPlan = new TestPlan();
                    for (int i = 0; i < innerSteps; i++)
                    {
                        var step = new DialogStep();
                        innerPlan.Steps.Add(new DialogStep());
                        var td = TypeData.GetTypeData(step);
                        foreach (var mem in td.GetMembers())
                        {
                            if (mem.Writable == false) continue;
                            if (mem.IsBrowsable() == false) continue;
                            if (mem.HasAttribute<XmlIgnoreAttribute>()) continue;
                            innerPlan.ExternalParameters.Add(step, mem, mem.Name);
                        }
                    }


                    innerPlan.Save(innerPlanName);

                }

                {
                    var outerPlan = new TestPlan();
                    for (int i = 0; i < outerSteps; i++)
                    {
                        var tpr = new TestPlanReference();
                        tpr.Filepath.Text = innerPlanName;
                        outerPlan.Steps.Add(tpr);
                    }

                    outerPlan.Save(outerPlanName);
                }
                {
                    var sw = Stopwatch.StartNew();
                    var loadPlan = TestPlan.Load(outerPlanName);
                    var loadtime = sw.Elapsed;
                    sw.Restart();
                    for (int i = 0; i < 10000; i++)
                    {
                        var annotate = AnnotationCollection.Annotate(loadPlan.Steps, new ReadOnlyMemberAnnotation());
                        var namemember = annotate.GetMember("Name");
                        annotate.Write();

                    }

                    var savetime = sw.Elapsed;
                    Debug.WriteLine("{2}, {0}, {1}", loadtime.TotalMilliseconds,
                        savetime.TotalMilliseconds, outerSteps);
                }
            }

        }
        
        [Test]
        public void AnnotatePerformance()
        {
            
            for (int j = 1; j < 20; j++)
            {
                int innerSteps = j * 5;
                int outerSteps = 10;
                var innerPlanName = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".TapPlan");
                var outerPlanName = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".TapPlan");
                {
                    var innerPlan = new TestPlan();
                    for (int i = 0; i < innerSteps; i++)
                    {
                        var step = new DialogStep();
                        innerPlan.Steps.Add(new DialogStep());
                        var td = TypeData.GetTypeData(step);
                        foreach (var mem in td.GetMembers())
                        {
                            if (mem.Writable == false) continue;
                            if (mem.IsBrowsable() == false) continue;
                            if (mem.HasAttribute<XmlIgnoreAttribute>()) continue;
                            innerPlan.ExternalParameters.Add(step, mem, mem.Name);
                        }
                    }


                    innerPlan.Save(innerPlanName);

                }

                {
                    var outerPlan = new TestPlan();
                    for (int i = 0; i < outerSteps; i++)
                    {
                        var tpr = new TestPlanReference();
                        tpr.Filepath.Text = innerPlanName;
                        outerPlan.Steps.Add(tpr);
                    }

                    outerPlan.Save(outerPlanName);
                }
                {
                    var sw = Stopwatch.StartNew();
                    var loadPlan = TestPlan.Load(outerPlanName);
                    var loadtime = sw.Elapsed;
                    sw.Restart();
                    for (int i = 0; i < 10000; i++)
                    {
                        var annotate = AnnotationCollection.Annotate(loadPlan.Steps, new ReadOnlyMemberAnnotation());
                        var namemember = annotate.GetMember("Name");
                        annotate.Write();

                    }

                    var savetime = sw.Elapsed;
                    Debug.WriteLine("{2}, {0}, {1}", loadtime.TotalMilliseconds,
                        savetime.TotalMilliseconds, outerSteps);
                }
            }

        }
    }
}