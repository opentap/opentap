//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
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
    public class ResultColumn : IResultColumn
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
        public string ObjectType { get { return "Result Column"; } }

        /// <summary>
        /// Helper to access a strongly typed value in the <see cref="Data"/> array.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="Index">Index in the array.</param>
        /// <returns></returns>
        public T GetValue<T>(int Index) where T : IConvertible
        {
            if ((Index < 0) || (Index >= Data.Length))
                return default(T);

            var value = Data.GetValue(Index);
            if (value == null)
                return default(T);
            else if (typeof(T) == typeof(object))
                return (T)value;
            else
                return (T)Convert.ChangeType(value, Type.GetTypeCode(typeof(T)));
        }

        /// <summary>
        /// Creates a new populated result column.
        /// </summary>
        /// <param name="name">The name of the column.</param>
        /// <param name="data">The data of the column.</param>
        public ResultColumn(string name, Array data)
        {
            if (name == null) throw new ArgumentNullException("name");
            if (data == null) throw new ArgumentNullException("data");

            Name = name;
            Data = data;
            TypeCode = Type.GetTypeCode(data.GetType().GetElementType());
        }
    }

    /// <summary>
    /// A vector containing a number of results with matching names, column name, and types. 
    /// </summary>
    [Serializable]
    public class ResultTable : IResultTable
    {
        /// <summary>
        /// The name of the results.
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// An array containing the result columns.
        /// </summary>
        public ResultColumn[] Columns { get; private set; }
        /// <summary>
        /// Indicates how many rows of results this vector contains.
        /// </summary>
        public int Rows { get; private set; }

        IResultColumn[] IResultTable.Columns { get { return Columns; } }

        /// <summary>
        /// The parent of this object.
        /// </summary>
        public IData Parent { get; protected set; }

        IParameters IData.Parameters { get { return new _Parameters(); } }

        string IAttributedObject.ObjectType { get { return "Result Vector"; } }
        long IData.GetID()
        {
            return 0;
        }

        /// <summary>
        /// Creates an empty vector.
        /// </summary>
        public ResultTable()
        {
            Name = "";
            Columns = new ResultColumn[0];
            Rows = 0;
        }

        /// <summary>
        /// Creates a new vector.
        /// </summary>
        /// <param name="name">The name of the result vector.</param>
        /// <param name="resultColumns">The columns of the vector.</param>
        public ResultTable(string name, ResultColumn[] resultColumns)
        {
            if (name == null) throw new ArgumentNullException("name");
            if (resultColumns == null) throw new ArgumentNullException("resultColumns");

            Name = name;
            Columns = resultColumns;
            if (resultColumns.Length <= 0)
                Rows = 0;
            else
            {
                Rows = resultColumns[0].Data.Length;
                for (int i = 1; i < resultColumns.Length; i++)
                {
                    if (resultColumns[i].Data.Length != Rows)
                        throw new ArgumentException("Columns needs to be of same length.", "resultColumns");
                }
            }
        }

        private class _Parameters : List<IParameter>, IParameters
        {
            public IConvertible this[string Name]
            {
                get
                {
                    return null;
                }
            }
        }
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
        private List<Exception> deferExceptions = new List<Exception>();

        /// <summary>
        /// Logging source for this class.
        /// </summary>
        static readonly TraceSource log = Log.CreateSource("ResultProxy");

        internal static Array GetArray(TypeCode tc, object Value)
        {
            var array = Array.CreateInstance(Utils.TypeOf(tc), 1);
            array.SetValue(Value, 0);
            return array;
        }

        internal static Array GetArray(Type type, object Value)
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

        private void DoStore(ResultTable obj)
        {
            planRun.ScheduleInResultProcessingThread<IResultListener>(l => Propagate(l, obj));
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
        }
        /// <summary>
        /// Waits for all result listeners in the test plan.
        /// </summary>
        /// <param name="execStage"></param>
        void WaitForResultListeners(TestPlanRun execStage)
        {
            WaitHandle.WaitAny(new[] { execStage.PromptWaitHandle, TapThread.Current.AbortToken.WaitHandle });
            
            foreach (IResultListener r in execStage.ResultListeners)
            {
                try //Usercode..
                {
                    execStage.ResourceManager.WaitUntilResourcesOpened(TapThread.Current.AbortToken, r);
                }
                catch (Exception e)
                {
                    log.Warning("Caught exception in result handling task.");
                    log.Debug(e);
                }
            }
        }

        /// <summary>
        /// Waits for the propagation of results from all Proxies to the Listeners. Normally this is not necessary. 
        /// However, if a step needs to change a property after it has written results, this method makes sure the ResultListeners record the previous/correct value before changing it.  	 
        /// </summary>
        public void Wait()
        {
            planRun.WaitForResults();
        }

        readonly WorkQueue DeferWorker = new WorkQueue(WorkQueue.Options.None, "Defer Worker");

        int deferCount = 0;
        /// <summary>
        /// Defer an action from the current teststep run. This means the action will be executed some time after
        /// the current run. 
        /// </summary>
        /// <param name="action"></param>
        public void Defer(Action action)
        {
            Interlocked.Increment(ref deferCount);
            // only one defer task may run at a time.
            DeferWorker.EnqueueWork(() =>
            {
                try
                {
                    action();
                }
                catch(Exception e)
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
                DeferWorker.Dispose();
            }
            else
            {
                DeferWorker.EnqueueWork(() =>
                {
                    try
                    {
                        if (deferExceptions.Count == 1)
                            action(Task.FromException(deferExceptions[0]));
                        else if (deferExceptions.Count > 1)
                            action(Task.FromException(new AggregateException(deferExceptions.ToArray())));
                        else
                            action(Finished);
                        DeferWorker.Dispose();
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

        private Dictionary<Type, Func<object, ResultTable>> ResultFunc = new Dictionary<Type, Func<object, ResultTable>>();
        private Dictionary<Type, Func<string, object, ResultTable>> AnonResultFunc = new Dictionary<Type, Func<string, object, ResultTable>>();

        /// <summary>
        /// Stores an object as a result.  These results will be propagated to the ResultStore after the TestStep completes.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="result">The result whose properties should be stored.</param>
        public void Publish<T>(T result)
        {
            if (result == null)
                throw new ArgumentNullException("result");
            Type runtimeType = result.GetType();
            if (!ResultFunc.ContainsKey(runtimeType))
            {
                var Typename = runtimeType.GetDisplayAttribute().GetFullName();
                var Props = runtimeType.GetPropertiesTap().Where(p => p.GetMethod != null).Where(p => p.PropertyType.DescendsTo(typeof(IConvertible))).ToList();
                var PropNames = Props.Select(p => p.GetDisplayAttribute().GetFullName()).ToList();

                ResultFunc[runtimeType] = (v) =>
                {
                    var cols = new ResultColumn[Props.Count];

                    for (int i = 0; i < Props.Count; i++)
                        cols[i] = new ResultColumn(PropNames[i], GetArray(Props[i].PropertyType, Props[i].GetValue(v)));

                    return new ResultTable(Typename, cols);
                };
            }

            var res = ResultFunc[runtimeType](result);

            DoStore(res);
        }

        /// <summary>
        /// Stores an object as a result.  These results will be propagated to the ResultStore after the TestStep completes.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name">The name of the result.</param>
        /// <param name="result">The result whose properties should be stored.</param>
        public void Publish<T>(string name, T result)
        {
            Type runtimeType = result.GetType();
            if (!AnonResultFunc.ContainsKey(runtimeType))
            {
                var Props = runtimeType.GetPropertiesTap().Where(p => p.GetMethod != null).Where(p => p.PropertyType.DescendsTo(typeof(IConvertible))).ToList();
                var PropNames = Props.Select(p => p.GetDisplayAttribute().GetFullName()).ToList();

                AnonResultFunc[runtimeType] = (n, v) =>
                {
                    var cols = new ResultColumn[Props.Count];

                    for (int i = 0; i < Props.Count; i++)
                        cols[i] = new ResultColumn(PropNames[i], GetArray(Props[i].PropertyType, Props[i].GetValue(v)));

                    return new ResultTable(n, cols);
                };
            }

            var res = AnonResultFunc[runtimeType](name, result);

            DoStore(res);
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
                throw new ArgumentNullException("columnNames");
            if (results == null)
                throw new ArgumentNullException("results");

            var columns = results.Zip(columnNames, (val, title) => new ResultColumn(title, GetArray(val == null ? typeof(object) : val.GetType(), val))).ToArray();

            DoStore(new ResultTable(name, columns));
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
                throw new ArgumentNullException("columnNames");
            if (results == null)
                throw new ArgumentNullException("results");

            var columns = results.Zip(columnNames, (val, title) => new ResultColumn(title, val)).ToArray();

            DoStore(new ResultTable(name, columns));
        }
    }
    
}
