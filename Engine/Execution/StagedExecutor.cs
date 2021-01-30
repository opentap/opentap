//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTap
{
    /// <summary>
    /// Implements the logic in a stage of the test plan execution flow.
    /// Should define public properties to refence other IExecutionStages that it depends on.
    /// </summary>
    public interface IExecutionStage : ITapPlugin
    {
        /// <summary>
        /// Runs the stage.
        /// </summary>
        void Execute();
    }

    class ExecutionStageDag
    {
        class Node
        {
            public ITypeData StageType;
            public IExecutionStage Stage;
            public IEnumerable<Node> PreviousStages;
        }

        readonly List<Node> AllNodes;
        readonly HashSet<Node> CompletedNodes = new HashSet<Node>();

        public int StageCount => AllNodes.Count;

        public ExecutionStageDag(ITypeData stageBaseType)
        {
            IEnumerable<ITypeData> stages = TypeData.GetDerivedTypes(stageBaseType).Where(s => s.CanCreateInstance);
            AllNodes = new List<Node>();

            Dictionary<ITypeData, IExecutionStage> stageInstances = new Dictionary<ITypeData, IExecutionStage>();
            stages.ForEach(st => stageInstances.Add(st, (IExecutionStage)st.CreateInstance()));

            // Add nodes
            foreach (ITypeData stageType in stages)
            {
                var stageReferences = stageType.GetMembers().Where(m => m.TypeDescriptor == stageBaseType);
                var node = new Node
                {
                    Stage = stageInstances[stageType],
                    StageType = stageType
                };
                AllNodes.Add(node);
            }

            // Fill in PreviousStages
            foreach (Node node in AllNodes)
            {
                var stageReferences = node.StageType.GetMembers().Where(m => m.TypeDescriptor.DescendsTo(stageBaseType));
                node.PreviousStages = stageReferences.Select(t => AllNodes.First(n => n.StageType == t.TypeDescriptor)).ToArray();
                stageReferences.ForEach(m => m.SetValue(node.Stage, stageInstances[m.TypeDescriptor]));
            }
        }

        internal IEnumerable<IExecutionStage> GetExecutableStages()
        {
            foreach (Node node in AllNodes)
            {
                if (CompletedNodes.Contains(node))
                    continue;
                if (!node.PreviousStages.Any(n => !CompletedNodes.Contains(n)))
                    yield return node.Stage;
            }
        }

        internal void MarkStageAsCompleted(IExecutionStage stage)
        {
            var node = AllNodes.FirstOrDefault(n => n.Stage == stage);
            if (node == null)
                throw new ArgumentOutOfRangeException("The node is not in this tree.");
            CompletedNodes.Add(node);
        }
    }
    class StagedExecutor
    {
        private ITypeData stageBaseType;
        private ExecutionStageEventLog EventLog = new ExecutionStageEventLog();

        public StagedExecutor(ITypeData stageBaseType)
        {
            this.stageBaseType = stageBaseType;
        }

        private void ExecuteStage(IExecutionStage stage)
        {
            try
            {
                stage.Execute();
                EventLog.Add(new CompletedEvent(stage));
            }
            catch (Exception ex)
            {
                EventLog.Add(new FailedEvent(stage, ex));
            }
        }

        public void Execute()
        {
            var tree = new ExecutionStageDag(stageBaseType);
            var initialNodes = tree.GetExecutableStages();
            
            // Kick off initial nodes
            foreach (IExecutionStage stage in initialNodes)
            {
                TapThread.Start(() => ExecuteStage(stage), stage.GetType().Name);
            }

            // Prosses events from stage executions:
            int nodesToProcess = tree.StageCount;
            var eventReader = new ExecutionStageEventLogReader(EventLog);
            while (nodesToProcess > 0)
            {
                var e = eventReader.Read();
                if(e is CompletedEvent completed)
                {
                    tree.MarkStageAsCompleted(completed.Stage);
                    var newExecutableStages = tree.GetExecutableStages();
                    // Kick off nodes that are now executable (their dependences have completed)
                    foreach (IExecutionStage stage in newExecutableStages)
                    {
                        TapThread.Start(() => ExecuteStage(stage), stage.GetType().Name);
                    }
                    nodesToProcess--;
                    if (!newExecutableStages.Any() && nodesToProcess > 0)
                        throw new NotSupportedException("This shouldn't happen.");

                }
                else if(e is FailedEvent failed)
                {
                    throw failed.Exception;
                }
            }
        }
    }
}
