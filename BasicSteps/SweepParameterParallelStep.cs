using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace OpenTap.Plugins.BasicSteps
{
    [AllowAnyChild]
    [Display(
        "Sweep Parameter Parallel",
        "Runs child steps for each sweep value simultaneously in separate threads.",
        "Flow Control"
    )]
    public class SweepParameterParallelStep : SweepParameterStep
    {
        // SweepParameterStepBase.SelectedMembers is internal — reimplemented here.
        IEnumerable<ParameterMemberData> SelectedParamMembers =>
            AvailableParameters.Where(x => Selected.ContainsKey(x.Name) && Selected[x.Name]);

        public SweepParameterParallelStep()
        {
            Name = "Sweep {Parameters} Parallel";
        }

        // The inherited SweepValues property carries [ElementFactory("NewElement")] and [Factory("NewSweepRowCollection")].
        // BindingFlags.NonPublic does not find private methods from base classes, so these shadows are required.
        private SweepRow NewElement() => new SweepRow(this);

        private SweepRowCollection NewSweepRowCollection() => new SweepRowCollection(this);

        public override void Run()
        {
            // base.Run() would execute the full sequential sweep — call LoopTestStep.Run() directly via reflection to only reset the break-loop token.
            typeof(LoopTestStep)
                .GetProperty(
                    "breakLoopToken",
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly
                )
                ?.SetValue(this, new CancellationTokenSource());

            var sets = SelectedParamMembers.ToArray();
            var rowType = SweepValues.Select(TypeData.GetTypeData).FirstOrDefault();

            var enabledRows = SweepValues
                .Select((row, idx) => (row, idx))
                .Where(x => x.row.Enabled)
                .ToArray();

            // Each row needs its own child step instances: parallel threads cannot share mutable step properties
            var childStepArray = ChildTestSteps.ToArray();
            // IgnoreErrors = true suppresses "Unable to find referenced step" warnings that arise when steps are serialized in isolation and cannot resolve cross-subtree references.
            var serializer = new TapSerializer { IgnoreErrors = true };

            Log.Info("Starting {0} sweep iterations in parallel.", enabledRows.Length);

            TapThread.WithNewContext(() =>
            {
                var sem = new SemaphoreSlim(0);
                var trd = TapThread.Current;
                // The first enabled row's clones keep the original GUIDs so the Editor can highlight steps during execution.
                // Subsequent rows get fresh GUIDs to avoid collisions in the engine's stepRuns map and to avoid TestStepSerializer.FixupStep's "Duplicate ID" warning.
                bool firstRow = true;

                foreach (var (sweepRow, index) in enabledRows)
                {
                    var additionalParams = new ResultParameters();
                    additionalParams.Add("Sweep", "Iteration", index + 1, null);
                    var iterParts = new List<string>(sets.Length);
                    foreach (var set in sets)
                    {
                        var mem = rowType.GetMember(set.Name);
                        var value = mem.GetValue(sweepRow);
                        var valueString =
                            value == null ? ""
                            : StringConvertProvider.TryGetString(
                                value,
                                out var s,
                                CultureInfo.InvariantCulture
                            )
                                ? s
                            : value.ToString();
                        var disp = mem.GetDisplayAttribute();
                        additionalParams.Add(
                            disp.Group.FirstOrDefault() ?? "",
                            disp.Name,
                            valueString,
                            null
                        );
                        iterParts.Add(valueString);
                    }
                    var iterStr = string.Join(", ", iterParts);

                    var rowClones = CloneAndConfigureChildSteps(
                        serializer,
                        childStepArray,
                        sweepRow,
                        sets,
                        rowType,
                        preserveStepIds: firstRow
                    );
                    firstRow = false;

                    // Prefix each clone's name with the iteration values so parallel log entries stay distinguishable.
                    foreach (var clone in rowClones)
                        clone.Name = $"[{iterStr}] \\ {clone.Name}";

                    TapThread.Start(
                        () =>
                        {
                            try
                            {
                                foreach (var clone in rowClones)
                                {
                                    if (!clone.Enabled)
                                        continue;
                                    var run = RunChildStep(clone, additionalParams);
                                    run.WaitForCompletion();

                                    // A descendant can set SuggestedNextStep = loop-parent.Id to request "jump to next iteration"
                                    if (run.SuggestedNextStep == Id)
                                        break;
                                }
                            }
                            catch
                            {
                                TapThread.WithNewContext(trd.Abort, null);
                            }
                            finally
                            {
                                sem.Release();
                            }
                        },
                        iterStr
                    );
                }

                for (int i = 0; i < enabledRows.Length; i++)
                    sem.Wait();
            });
        }

        private ITestStep[] CloneAndConfigureChildSteps(
            TapSerializer serializer,
            ITestStep[] originals,
            SweepRow row,
            ParameterMemberData[] sets,
            ITypeData rowType,
            bool preserveStepIds
        )
        {
            var clones = new ITestStep[originals.Length];
            for (int i = 0; i < originals.Length; i++)
            {
                var xml = serializer.SerializeToString(originals[i]);
                if (!preserveStepIds)
                    xml = ReplaceStepIds(xml, originals[i]);
                clones[i] = (ITestStep)serializer.DeserializeFromString(xml);
                clones[i].Parent = this;
            }

            var originalToClone = new Dictionary<ITestStep, ITestStep>();
            for (int i = 0; i < originals.Length; i++)
                BuildStepMap(originals[i], clones[i], originalToClone);

            // Restore Input<T> connections: within-subtree references are redirected to clones;
            // external ones (e.g. instruments) keep the original reference.
            for (int i = 0; i < originals.Length; i++)
                FixInputReferences(originals[i], clones[i], originalToClone);

            // Restore InputOutputRelation connections broken by isolated deserialization.
            // These are used by steps with plain connected properties (not Input<T>).
            for (int i = 0; i < originals.Length; i++)
                FixInputOutputRelations(originals[i], originalToClone);

            foreach (var set in sets)
            {
                var mem = rowType.GetMember(set.Name);
                if (mem == null)
                    continue;
                var value = mem.GetValue(row);

                foreach (var (source, member) in set.ParameterizedMembers)
                {
                    if (
                        source is ITestStep srcStep
                        && originalToClone.TryGetValue(srcStep, out var clone)
                    )
                        TypeData.GetTypeData(clone).GetMember(member.Name)?.SetValue(clone, value);
                }
            }

            return clones;
        }

        // IDs are replaced in the XML before deserialization so the serializer never registers a
        // duplicate — assigning new IDs after the fact would still trigger the warning.
        private static string ReplaceStepIds(string xml, ITestStep step)
        {
            xml = xml.Replace(step.Id.ToString(), Guid.NewGuid().ToString());
            foreach (var child in step.ChildTestSteps)
                xml = ReplaceStepIds(xml, child);
            return xml;
        }

        private static void FixInputReferences(
            ITestStep original,
            ITestStep clone,
            Dictionary<ITestStep, ITestStep> originalToClone
        )
        {
            foreach (var member in TypeData.GetTypeData(original).GetMembers())
            {
                if (member.GetValue(original) is IInput originalInput && originalInput.Step != null)
                {
                    if (member.GetValue(clone) is IInput cloneInput)
                    {
                        cloneInput.Step = originalToClone.TryGetValue(
                            originalInput.Step,
                            out var mapped
                        )
                            ? mapped
                            : originalInput.Step;
                        cloneInput.Property = originalInput.Property;
                    }
                }
            }

            for (
                int i = 0;
                i < original.ChildTestSteps.Count && i < clone.ChildTestSteps.Count;
                i++
            )
                FixInputReferences(
                    original.ChildTestSteps[i],
                    clone.ChildTestSteps[i],
                    originalToClone
                );
        }

        private static void FixInputOutputRelations(
            ITestStep original,
            Dictionary<ITestStep, ITestStep> originalToClone
        )
        {
            if (!originalToClone.TryGetValue(original, out var clone))
                return;

            foreach (
                var relation in InputOutputRelation
                    .GetRelations(original)
                    .Where(r => r.InputObject == original)
            )
            {
                if (
                    relation.OutputObject is ITestStep outputStep
                    && originalToClone.TryGetValue(outputStep, out var outputClone)
                )
                {
                    var inputMember = TypeData
                        .GetTypeData(clone)
                        .GetMember(relation.InputMember.Name);
                    var outputMember = TypeData
                        .GetTypeData(outputClone)
                        .GetMember(relation.OutputMember.Name);
                    if (inputMember != null && outputMember != null)
                    {
                        // Assign throws on type-mismatch or duplicate relations; skip those rather than fail the whole sweep.
                        try
                        {
                            InputOutputRelation.Assign(
                                clone,
                                inputMember,
                                outputClone,
                                outputMember
                            );
                        }
                        catch { }
                    }
                }
            }

            foreach (var child in original.ChildTestSteps)
                FixInputOutputRelations(child, originalToClone);
        }

        private static void BuildStepMap(
            ITestStep original,
            ITestStep clone,
            Dictionary<ITestStep, ITestStep> map
        )
        {
            map[original] = clone;
            for (
                int i = 0;
                i < original.ChildTestSteps.Count && i < clone.ChildTestSteps.Count;
                i++
            )
                BuildStepMap(original.ChildTestSteps[i], clone.ChildTestSteps[i], map);
        }
    }
}
