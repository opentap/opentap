//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTap
{
    /// <summary>
    /// A class to store a column of data for a <see cref="ResultTable"/>.
    /// </summary>
    [Serializable]
    public class ResultColumn : IResultColumn, IData
    {
        /// <summary>
        /// The name of the column.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// The data in the column.
        /// </summary>
        public Array Data { get; private set; }

        /// <summary>
        /// The TypeCode of data in the column.
        /// </summary>
        public TypeCode TypeCode { get; private set; }

        /// <summary>
        /// String describing the column.
        /// </summary>
        public string ObjectType { get; }

        /// <summary>
        /// Helper to access a strongly typed value in the <see cref="Data"/> array.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="Index">Index in the array.</param>
        /// <returns></returns>
        public T GetValue<T>(int Index) where T : IConvertible
        {
            if ((Index < 0) || (Index >= Data.Length))
                return default;

            var value = Data.GetValue(Index);
            if (value == null)
                return default;
            if (typeof(T) == typeof(object))
                return (T)value;
            return (T)Convert.ChangeType(value, Type.GetTypeCode(typeof(T)));
        }

        /// <summary>
        /// Creates a new populated result column.
        /// </summary>
        /// <param name="name">The name of the column.</param>
        /// <param name="data">The data of the column.</param>
        public ResultColumn(string name, Array data)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (data == null) throw new ArgumentNullException(nameof(data));

            Name = name;
            Data = data;
            TypeCode = Type.GetTypeCode(data.GetType().GetElementType());
            Parameters = ParameterCollection.Empty;
            ObjectType = ResultObjectTypes.ResultColumn;
        }

        /// <summary> Creates a new result column with parameters attached.  </summary>
        public ResultColumn(string name, Array data, params IParameter[] parameters) : this(name, data)
        {
            Parameters = new ParameterCollection(parameters);
        }

        internal ResultColumn(string name, Array data, IData table, IParameters parameters,
            string ObjectType = ResultObjectTypes.ResultColumn) : this(name, data)
        {
            Parameters = parameters;
            Parent = table;
            this.ObjectType = ObjectType;
        }

        internal ResultColumn WithResultTable(ResultTable table)
        {
            return new ResultColumn(Name, Data, table, Parameters, (this as IAttributedObject).ObjectType);
        }

        /// <summary>
        /// The parent object of this column. Usually a Result Table. This value will get assigned during ResultProxy.Publish.
        /// </summary>
        public IData Parent { get; }

        /// <summary> Unused. </summary>
        long IData.GetID() => 0;

        /// <summary> The parameters attached to this column. </summary>
        public IParameters Parameters { get; }

        /// <summary>  Create a result column clone with additional parameters. </summary>
        public ResultColumn AddParameters(params IParameter[] additionalParameters)
        {
            return new ResultColumn(Name, Data, Parent,
                new ParameterCollection(Parameters.Concat(additionalParameters).ToArray()));
        }
    }

    /// <summary>
    /// A result table containing rows of results with matching names, column name, and types. 
    /// </summary>
    [Serializable]
    public class ResultTable : IResultTable
    {
        /// <summary>
        /// The name of the results.
        /// </summary>
        public string Name { get; private set; }

        ResultColumn[] columns;

        /// <summary> An array containing the result columns. </summary>
        public ResultColumn[] Columns
        {
            get => columns;
            private set => columns = value;
        }

        /// <summary>
        /// Indicates how many rows of results this table contains.
        /// </summary>
        public int Rows { get; private set; }

        IResultColumn[] IResultTable.Columns
        {
            get { return Columns; }
        }

        /// <summary>
        /// The parent of this object.
        /// </summary>
        public IData Parent { get; protected set; }

        /// <summary>
        /// Parameters attached to this Result Table.
        /// Note, test step parameter are often attached in the result listener and does not need to be added here.
        /// </summary>
        public IParameters Parameters { get; } = ParameterCollection.Empty;


        string IAttributedObject.ObjectType => ResultObjectTypes.ResultVector;

        long IData.GetID()
        {
            return 0;
        }

        /// <summary>
        /// Creates an empty results table.
        /// </summary>
        public ResultTable()
        {
            Name = "";
            Columns = new ResultColumn[0];
            Rows = 0;
        }

        /// <summary>
        /// Creates a new result table.
        /// </summary>
        /// <param name="name">The name of the result table.</param>
        /// <param name="resultColumns">The columns of the table.</param>
        public ResultTable(string name, ResultColumn[] resultColumns)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (resultColumns == null) throw new ArgumentNullException(nameof(resultColumns));

            Name = name;
            columns = resultColumns.ToArray();
            for (int i = 0; i < columns.Length; i++)
            {
                columns[i] = columns[i].WithResultTable(this);
            }

            if (columns.Length <= 0)
                Rows = 0;
            else
            {
                Rows = columns[0].Data.Length;
                for (int i = 1; i < columns.Length; i++)
                {
                    if (columns[i].Data.Length != Rows)
                        throw new ArgumentException("Columns needs to be of same length.", nameof(resultColumns));
                }
            }
        }

        /// <summary>
        /// Creates a new Result Table with a name, result columns and parameters.
        /// </summary>
        public ResultTable(string name, ResultColumn[] resultColumns, params IParameter[] parameters) : this(name,
            resultColumns)
        {
            Parameters = new ParameterCollection(parameters);
        }

        ResultTable(string name, ResultColumn[] resultColumns, IParameters parameters) : this(name, resultColumns)
        {
            Parameters = parameters;
        }

        internal ResultTable WithName(string newName) => new ResultTable(newName, Columns, Parameters);
    }

    /// <summary>
    /// Interface that the TestStep can access through the Results property.
    /// </summary>
    public interface IResultSource
    {
        /// <summary>
        /// Waits for the propagation of results from all Proxies to the Listeners. Normally this is not necessary. 
        /// However, if a step needs to change a property after it has written results, this method makes sure the ResultListeners record the previous/correct value before changing it.
        /// </summary>
        void Wait();

        /// <summary>
        /// Defer an action from the current teststep run.
        /// This action will be called as soon as possible, and block the execution for any parent steps.
        /// </summary>
        /// <param name="action"></param>
        void Defer(Action action);

        /// <summary>
        /// Run an action as the final step after the last deferred action.
        /// This should not be used while the associated TestStep is running.
        /// </summary>
        /// <param name="action"></param>
        void Finally(Action<Task> action);

        /// <summary>
        /// Stores a result. These results will be propagated to the ResultStore after the TestStep completes.
        /// </summary>
        /// <param name="name">Name of the result.</param>
        /// <param name="columnNames">Titles of the columns.</param>
        /// <param name="results">The values of the results to store.</param>
        void Publish(string name, List<string> columnNames, params IConvertible[] results);

        /// <summary>
        /// The fastest way to store a result. These results will be propagated to the ResultStore after the TestStep completes.
        /// </summary>
        /// <param name="name">Name of the result.</param>
        /// <param name="columnNames">Titles of the columns.</param>
        /// <param name="results">The columns of the results to store.</param>
        /// <remarks>
        /// This is the fastest way to store a large number of results.
        /// </remarks>
        void PublishTable(string name, List<string> columnNames, params Array[] results);
    }

    /// <summary>
    /// Temporarily holds results from a TestStep, before they are propagated to the ResultListener by the TestPlan. See <see cref="TestStep.Results"/>
    /// </summary>
    public class ResultSource : IResultSource
    {
        /// <summary>  Logging source for this class. </summary>
        static readonly TraceSource log = Log.CreateSource("ResultProxy");

        static Array GetArray(Type type, object Value)
        {
            var array = Array.CreateInstance(type, 1);
            array.SetValue(Value, 0);
            return array;
        }

        void Propagate(IResultListener rt, ResultTable result)
        {
            try
            {
                rt.OnResultPublished(stepRun.Id, result);
            }
            catch (Exception e)
            {
                log.Warning("Caught exception in result handling task.");
                log.Debug(e);
                planRun.RemoveFaultyResultListener(rt);
            }
        }


        /// <summary>
        /// The current plan run.
        /// </summary>
        readonly TestPlanRun planRun;

        /// <summary>
        /// The TestStepRun for this object.
        /// </summary>
        readonly TestStepRun stepRun;

        /// <summary>
        /// Adds an additional parameter to this TestStep run.
        /// </summary>
        /// <param name="param">Parameter to add.</param>
        public void AddParameter(ResultParameter param)
        {
            stepRun.Parameters.Add(param);
        }

        /// <summary>
        /// Creates a new ResultProxy. Done for each test step run.
        /// </summary>
        /// <param name="stepRun">TestStepRun that this result proxy is proxy for.</param>
        /// <param name="planRun">TestPlanRun that this result proxy is proxy for.</param>
        public ResultSource(TestStepRun stepRun, TestPlanRun planRun)
        {
            this.stepRun = stepRun;
            this.planRun = planRun;
            stepRun.SetResultSource(this);
        }

        /// <summary>
        /// Waits for the propagation of results from all Proxies to the Listeners. Normally this is not necessary. 
        /// However, if a step needs to change a property after it has written results, this method makes sure the ResultListeners record the previous/correct value before changing it.  	 
        /// </summary>
        public void Wait()
        {
            planRun.WaitForResults();
        }

        WorkQueue deferWorker;
        List<Exception> deferExceptions;

        /// <summary>
        /// Defer an action from the current test step run. This means the action will be executed some time after
        /// the current run. 
        /// </summary>
        /// <param name="action"></param>
        public void Defer(Action action)
        {
            if (TapThread.Current != stepRun.StepThread)
                throw new InvalidOperationException(
                    "Defer may only be executed from the same thread as the test step.");
            DeferNoCheck(action);
        }

        int deferCount = 0;

        internal void DeferNoCheck(Action action)
        {
            if (deferWorker == null)
            {
                deferExceptions = new List<Exception>();
                deferWorker = new WorkQueue(WorkQueue.Options.None, "Defer Worker");
            }

            Interlocked.Increment(ref deferCount);
            // only one defer task may run at a time.
            deferWorker.EnqueueWork(() =>
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    deferExceptions.Add(e);
                }
                finally
                {
                    Interlocked.Decrement(ref deferCount);
                }
            });
        }

        static readonly Task Finished = Task.FromResult(0);

        /// <summary>
        /// Run an action as the final step after the last deferred action
        /// </summary>
        /// <param name="action"></param>
        void IResultSource.Finally(Action<Task> action)
        {
            if (deferCount == 0)
            {
                action(Finished);
                deferWorker?.Dispose();
            }
            else
            {
                deferWorker.EnqueueWork(() =>
                {
                    try
                    {
                        if (deferExceptions.Count == 1)
                            action(Task.FromException(deferExceptions[0]));
                        else if (deferExceptions.Count > 1)
                            action(Task.FromException(new AggregateException(deferExceptions.ToArray())));
                        else
                            action(Finished);
                        deferWorker.Dispose();
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception e)
                    {
                        log.Error("Caught error while finalizing test step run. {0}", e.Message);
                        log.Debug(e);
                    }
                });
            }
        }

        Dictionary<ITypeData, Func<object, ResultTable>>
            resultFunc = null; //new Dictionary<Type, Func<object, ResultTable>>();

        readonly object resultFuncLock = new object();

        ResultTable ToResultTable<T>(T result)
        {
            ITypeData runtimeType = TypeData.GetTypeData(result);
            if (resultFunc == null)
            {
                lock (resultFuncLock)
                    resultFunc = new Dictionary<ITypeData, Func<object, ResultTable>>();
            }

            if (!resultFunc.ContainsKey(runtimeType))
            {
                bool enumerable = result is IEnumerable && !(result is string);
                var targetType = runtimeType;
                if (enumerable)
                {
                    targetType = targetType.AsTypeData().ElementType;
                }

                var Typename = targetType.GetDisplayAttribute().GetFullName();
                var classParameters = targetType.GetAttributes<IParameter>().ToArray();
                var Props = targetType.GetMembers()
                    .Where(x => x.Readable && x.TypeDescriptor.DescendsTo(typeof(IConvertible))).ToArray();
                var PropNames = Props.Select(p => p.GetDisplayAttribute().GetFullName()).ToArray();
                var propParameter = Props.Select(p => p.GetAttributes<IParameter>().ToArray()).ToArray();
                resultFunc[runtimeType] = (v) =>
                {
                    int count;
                    IEnumerable values;
                    if (enumerable)
                    {
                        values = (IEnumerable)v;
                        count = values.Count();
                    }
                    else
                    {
                        values = new[] { v };
                        count = 1;
                    }

                    var arrays = Props
                        .Select(p => Array.CreateInstance(p.TypeDescriptor.AsTypeData().Type, count)).ToArray();

                    int j = 0;
                    foreach (var obj in values)
                    {
                        for (int i = 0; i < Props.Length; i++)
                        {
                            arrays[i].SetValue(Props[i].GetValue(obj), j);
                        }

                        j++;
                    }

                    var cols = new ResultColumn[Props.Length];
                    for (int i = 0; i < Props.Length; i++)
                    {
                        cols[i] = new ResultColumn(PropNames[i], arrays[i], propParameter[i]);
                    }

                    return new ResultTable(Typename, cols, classParameters);
                };
            }

            return resultFunc[runtimeType](result);
        }

        /// <summary>
        /// Stores an object as a result.  These results will be propagated to the ResultStore after the TestStep completes.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="result">The result whose properties should be stored.</param>
        public void Publish<T>(T result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            PublishTable(ToResultTable(result));
        }

        /// <summary> Publishes a result table. </summary>
        public void Publish(ResultTable result) => PublishTable(result);

        /// <summary>
        /// Stores an object as a result.  These results will be propagated to the ResultStore after the TestStep completes.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name">The name of the result.</param>
        /// <param name="result">The result whose properties should be stored.</param>
        public void Publish<T>(string name, T result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            Publish(ToResultTable(result).WithName(name));
        }

        /// <summary>
        /// Stores a result. These results will be propagated to the ResultStore after the TestStep completes.
        /// </summary>
        /// <param name="name">Name of the result.</param>
        /// <param name="columnNames">Titles of the columns.</param>
        /// <param name="results">The values of the results to store.</param>
        public void Publish(string name, List<string> columnNames, params IConvertible[] results)
        {
            if (columnNames == null)
                throw new ArgumentNullException(nameof(columnNames));
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            var columns = results.Zip(columnNames, (val, title)
                => new ResultColumn(title, GetArray(val == null ? typeof(object) : val.GetType(), val))).ToArray();

            Publish(new ResultTable(name, columns));
        }

        /// <summary>
        /// The fastest way to store a result. These results will be propagated to the ResultStore after the TestStep completes.
        /// </summary>
        /// <param name="name">Name of the result.</param>
        /// <param name="columnNames">Titles of the columns.</param>
        /// <param name="results">The columns of the results to store.</param>
        /// <remarks>
        /// This is the fastest way to store a large number of results.
        /// </remarks>
        public void PublishTable(string name, List<string> columnNames, params Array[] results)
        {
            if (columnNames == null)
                throw new ArgumentNullException(nameof(columnNames));
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            var columns = results.Zip(columnNames, (val, title) => new ResultColumn(title, val)).ToArray();

            Publish(new ResultTable(name, columns));
        }

        /// <summary> Publishes a result table. </summary>
        public void PublishTable(ResultTable table)
        {
            planRun.ScheduleInResultProcessingThread(new PublishResultTableInvokable(table, this));
        }

        internal bool WasDeferred => deferWorker != null;

        class PublishResultTableInvokable : IInvokable<IResultListener, WorkQueue>
        {
            readonly ResultTable table;
            readonly ResultSource proxy;

            public PublishResultTableInvokable(ResultTable table, ResultSource proxy)
            {
                this.table = table;
                this.proxy = proxy;
            }

            /// <summary>
            /// If possible, introspect the current work queue and collapse result table propagations into one.
            /// This can give a huge performance boost for many use cases, but mostly when PublishTable is not used.
            ///
            /// Note that this has to be done in the result listener thread - since each may have different number of elements queued
            /// depending on the speed of the result listener. Slow ones like internet based ones will have more items queued.
            /// </summary>
            /// <returns>An optimized table or the original one if it is not possible to optimize.</returns>
            ResultTable CreateOptimizedTable(WorkQueue workQueue)
            {
                List<ResultTable> mergeTables = null;
                while (workQueue?.Peek() is PublishResultTableInvokable p)
                {
                    if (!ResultTableOptimizer.CanMerge(p.table, table))
                        break;
                    p = (PublishResultTableInvokable)workQueue.Dequeue();
                    if (p == null) break;
                    if (mergeTables == null)
                        mergeTables = new List<ResultTable>();
                    mergeTables.Add(p.table);
                    
                }

                if (mergeTables != null)
                {
                    mergeTables.Add(table);
                    return ResultTableOptimizer.MergeTables(mergeTables);
                }

                return table;
            }

            public void Invoke(IResultListener a, WorkQueue queue)
            {
                try
                {
                    a.OnResultPublished(proxy.stepRun.Id, CreateOptimizedTable(queue));
                }
                catch (Exception e)
                {
                    log.Warning("Caught exception in result handling task.");
                    log.Debug(e);
                    proxy.planRun.RemoveFaultyResultListener(a);
                }
            }


        }
    }
}