//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace OpenTap
{
    [AttributeUsage(AttributeTargets.Property,AllowMultiple = false)]
    internal class ExecutionStageReferenceAttribute : Attribute
    {
        public bool ExecuteIfReferenceFails { get; }
        public bool ExecuteIfReferenceSkipped { get; }

        public ExecutionStageReferenceAttribute(bool executeIfReferenceFails = false, bool executeIfReferenceSkipped = false)
        {
            ExecuteIfReferenceFails = executeIfReferenceFails;
            ExecuteIfReferenceSkipped = executeIfReferenceSkipped;
        }
    }

    /// <summary>
    /// Implements the logic in a stage of the test plan execution flow.
    /// Should define public properties to refence other IExecutionStages that it depends on.
    /// </summary>
    public interface IExecutionStage : ITapPlugin
    {
        /// <summary>
        /// Runs the stage.
        /// </summary>
        bool Execute(ExecutionStageContext context);
    }

    /// <summary>
    /// Context object passed to all IExecutionStages in an execution 
    /// </summary>
    public abstract class ExecutionStageContext
    {
        internal readonly Dictionary<IExecutionStage, Exception> Exceptions = new Dictionary<IExecutionStage, Exception>();

        /// <summary>
        /// Returns any Exception that might have been thrown from a previous IExecutionStage or null if the given stage completed sucessfully.
        /// </summary>
        public Exception GetExceptionFromFailedStage(IExecutionStage stage)
        {
            Exceptions.TryGetValue(stage, out Exception ex);
            return ex;
        }
    }

    class ExecutionStageDag
    {
        enum State
        {
            Waiting,
            Pending,
            Executing,
            Completed
        }

        public enum Result
        {
            Pending,
            Sucess,
            Fail,
            /// <summary>
            /// The stage did not execute and is not going to because a previous dependent stage failed.
            /// </summary>
            Skipped
        }

        [DebuggerDisplay("{StageType,nq} {State,nq}")]
        class Node
        {
            public ITypeData StageType;
            public IExecutionStage Stage;
            public List<Transision> Transisions = new List<Transision>();
            public object StateTransitionLock = new object();
            public State State = State.Pending;
            public Result Result = Result.Pending;
        }

        class Transision
        {
            public Node FromNode;
            public Node ToNode;
            public List<Result> Triggers = new List<Result> { Result.Sucess };
        }

        readonly List<Node> AllNodes;

        public int StageCount => AllNodes.Count;

        public ExecutionStageDag(ITypeData stageBaseType)
        {
            IEnumerable<ITypeData> stages = TypeData.GetDerivedTypes(stageBaseType).Where(s => s.CanCreateInstance);
            AllNodes = new List<Node>();

            Dictionary<ITypeData, IExecutionStage> stageInstances = new Dictionary<ITypeData, IExecutionStage>();
            foreach (ITypeData stage in stages)
            {
                IExecutionStage instance = (IExecutionStage)stage.CreateInstance();
                stageInstances.Add(stage, instance);
            }

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
                if (stageReferences.Any())
                {
                    foreach (var stageRef in stageReferences)
                    {
                        var transision = new Transision()
                        {
                            ToNode = node,
                            FromNode = AllNodes.First(n => n.StageType == stageRef.TypeDescriptor),
                        };
                        var attr = stageRef.GetAttribute<ExecutionStageReferenceAttribute>();
                        if (attr?.ExecuteIfReferenceFails == true)
                            transision.Triggers.Add(Result.Fail);
                        if (attr?.ExecuteIfReferenceSkipped == true)
                            transision.Triggers.Add(Result.Skipped);
                        node.Transisions.Add(transision);
                        transision.FromNode.Transisions.Add(transision);
                        stageRef.SetValue(node.Stage, stageInstances[stageRef.TypeDescriptor]);
                    }
                    node.State = State.Waiting;
                }
            }
        }

        internal IExecutionStage TakeExecutableStage()
        {
            foreach (Node node in AllNodes)
            {
                lock (node.StateTransitionLock)
                {
                    if (node.State == State.Pending)
                    {
                        node.State = State.Executing;
                        return node.Stage;
                    }
                }
            }
            return null;
        }

        internal void MarkStageAsCompleted(IExecutionStage stage, Result res)
        {
            var node = AllNodes.FirstOrDefault(n => n.Stage == stage);
            if (node == null)
            {
                Log.Flush();
                throw new ArgumentOutOfRangeException("The node is not in this tree.");
            }
            node.State = State.Completed;
            node.Result = res;
            foreach (Transision t in node.Transisions)
            {
                if(t.FromNode == node)
                {
                    if ( (res == Result.Fail && !t.Triggers.Contains(Result.Fail)) ||
                         (res == Result.Skipped && !t.Triggers.Contains(Result.Skipped))
                         )
                    {
                        MarkStageAsCompleted(t.ToNode.Stage, Result.Skipped);
                        continue;
                    }

                    t.ToNode.State = State.Pending;
                    foreach (Transision tTo in t.ToNode.Transisions)
                    {
                        if (tTo.ToNode == t.ToNode)
                        {
                            if (!tTo.Triggers.Contains(tTo.FromNode.Result))
                            {
                                tTo.ToNode.State = State.Waiting;
                            }
                        }
                    }
                }
            }
            //foreach (var nextNode in AllNodes.Where(n => n.PreviousStages.Contains(node)))
            //{
            //    if (nextNode.PreviousStages.All(s => s.State == State.Completed))
            //    {
            //        Debug.Assert(nextNode.State == State.Waiting);
            //        nextNode.State = State.Pending;
            //    }
            //}
        }

        internal bool IsInTree(IExecutionStage stage)
        {
            return AllNodes.Any(n => n.Stage == stage);
        }
    }
    class StagedExecutor
    {
        private static TraceSource log = Log.CreateSource("StageExec");
        private ITypeData stageBaseType;
        private ExecutionStageEventLog EventLog = new ExecutionStageEventLog();

        public StagedExecutor(ITypeData stageBaseType)
        {
            this.stageBaseType = stageBaseType;
        }

        private void ExecuteStage(IExecutionStage stage, ExecutionStageContext context)
        {
            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                if (stage.Execute(context))
                {
                    EventLog.Add(new CompletedEvent(stage));
                    log.Debug(timer, "Stage {0} completed.", stage.GetType().Name);
                }
                else
                {
                    EventLog.Add(new FailedEvent(stage, null));
                    log.Warning(timer, "Stage {0} failed.", stage.GetType().Name);
                }
            }
            catch (Exception ex)
            {
                EventLog.Add(new FailedEvent(stage, ex));
                log.Warning(timer, "Stage {0} failed with exception: {1}", stage.GetType().Name, ex.Message);
            }
        }

        public TResult Execute<TResult>(ExecutionStageContext context) where TResult : class
        {
            var tree = new ExecutionStageDag(stageBaseType);
            log.Debug("Starting execution of tree with {0} total stages.",tree.StageCount);

            var eventReader = new ExecutionStageEventLogReader(EventLog);

            // Kick off initial stages
            int stagesStarted = 0;
            while (true)
            {
                var stage = tree.TakeExecutableStage();
                if (stage == null)
                    break;
                TapThread.Start(() => ExecuteStage(stage, context), stage.GetType().Name);
                stagesStarted++;
            }

            TResult result = null;

            // Prosses events from stage executions:
            int stagesCompleted = 0;
            while (stagesCompleted < tree.StageCount)
            {
                Exception ex = null;
                if (stagesStarted == stagesCompleted)
                {
                    log.Debug("Execution ended without executing all stages.");
                    return result;
                }
                var e = eventReader.Read();
                if (!tree.IsInTree(e.Stage)) // ToDo: figure out better way to not depend on specific stage instances in the tree
                    continue;
                if(e is CompletedEvent completed)
                {
                    tree.MarkStageAsCompleted(completed.Stage,ExecutionStageDag.Result.Sucess);
                    var res = GetResultFromStage<TResult>(completed.Stage);
                    if (res != null) result = res;
                }
                else if(e is FailedEvent failed)
                {
                    tree.MarkStageAsCompleted(failed.Stage, ExecutionStageDag.Result.Fail);
                    ex = failed.Exception;
                    context.Exceptions[failed.Stage] = ex;
                }

                // Kick off nodes that are now executable (their dependences have completed)
                while (true)
                {
                    var stage = tree.TakeExecutableStage();
                    if (stage == null)
                        break;
                    TapThread.Start(() => ExecuteStage(stage, context), stage.GetType().Name);
                    stagesStarted++;
                }
                stagesCompleted++;
                if (stagesStarted == stagesCompleted && ex != null)
                    throw ex;
            }
            return result;
        }

        private TResult GetResultFromStage<TResult>(IExecutionStage stage)
        {
            ITypeData resultType = TypeData.FromType(typeof(TResult));
            var resultProperties = TypeData.GetTypeData(stage).GetMembers().Where(m => m.TypeDescriptor == resultType);
            if(resultProperties.Count() > 1)
                throw new Exception($"Stage has multiple properties of type {resultType.Name}");
            return (TResult)resultProperties.FirstOrDefault()?.GetValue(stage);

        }
    }
}
