//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace OpenTap
{
    /// <summary>
    /// Class for getting user input without using GUI.
    /// </summary>
    public class UserInput
    {
        /// <summary> Request user input from the GUI. Waits an amount of time specified by Timeout. If the timeout occurs a TimeoutException will be thrown.</summary>
        /// <param name="dataObject">The object the user should fill out with data.</param>
        /// <param name="Timeout">How long to wait before timing out. </param>
        /// <param name="modal">set to True if a modal request is wanted. This means the user will have to answer before doing anything else.</param>
        public static void Request(object dataObject, TimeSpan Timeout, bool modal = false)
        {
            inputInterface?.RequestUserInput(dataObject, Timeout, modal);
        }

        /// <summary> Request user input from the GUI. Waits indefinitely.</summary>
        /// <param name="dataObject">The object the user should fill out with data.</param>
        /// <param name="modal">set to True if a modal request is wanted. This means the user will have to answer before doing anything else.</param>
        public static void Request(object dataObject, bool modal = false)
        {
            inputInterface?.RequestUserInput(dataObject, TimeSpan.MaxValue, modal);
        }

        static IUserInputInterface inputInterface;
        static IUserInterface userInterface;
        /// <summary> Sets the current user input interface. This should almost never be called from user code. </summary>
        /// <param name="inputInterface"></param>
        public static void SetInterface(IUserInputInterface inputInterface)
        {
            UserInput.inputInterface = inputInterface;
            UserInput.userInterface = inputInterface as IUserInterface;
        }

        /// <summary> Call to notify the user interface that an object property has changed. </summary>
        /// <param name="obj"></param>
        /// <param name="property"></param>
        public static void NotifyChanged(object obj, string property)
        {
            userInterface?.NotifyChanged(obj, property);
        }

        /// <summary> Gets the current user input interface. </summary>
        /// <returns></returns>
        public static IUserInputInterface GetInterface()
        {
            return UserInput.inputInterface;
        }
    }

    /// <summary> Defines a way for plugins to notify the user that a property has changed. </summary>
    public interface IUserInterface
    {
        /// <summary>
        /// This method is called to notify that a property has changed on an object.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="property"></param>
        void NotifyChanged(object obj, string property);
    }

    /// <summary> Defines a way for plugins to request input from the user. </summary>
    public interface IUserInputInterface
    {
        /// <summary> The method called when the interface requests user input.</summary>
        /// <param name="dataObject">The object the user should fill out with data.</param>
        /// <param name="Timeout">How long the user should have.</param>
        /// <param name="modal"> True if a modal request is wanted</param>
        void RequestUserInput(object dataObject, TimeSpan Timeout, bool modal);   
    }

    /// <summary> The supported layout modes. </summary>
    [Flags]
    public enum LayoutMode
    {
        /// <summary> The default mode.</summary>
        Normal = 1,
        /// <summary> The user input fills the whole row. </summary>
        FullRow = 2,
        /// <summary> The user input floats to the bottom.</summary>
        FloatBottom = 4
    }

    /// <summary> LayoutAttribute can be used to specify the wanted layout for user interfaces.</summary>
    public class LayoutAttribute : Attribute
    {
        /// <summary> Specifies the mode of layout.</summary>
        public LayoutMode Mode { get; }
        /// <summary> How much height should the input take.  </summary>
        public int RowHeight { get; }

        /// <summary> Maximum row height for the input. </summary>
        public int MaxRowHeight { get; } = 1000;

        /// <summary> </summary>
        /// <param name="mode"></param>
        /// <param name="rowHeight"></param>
        /// <param name="maxRowHeight"></param>
        public LayoutAttribute(LayoutMode mode, int rowHeight = 1, int maxRowHeight = 1000)
        {
            Mode = mode;
            RowHeight = rowHeight;
            MaxRowHeight = maxRowHeight;
        }
    }

    /// <summary> Specifies that a property finalizes input.</summary>
    public class SubmitAttribute : Attribute { }

    /// <summary> Standard implementation of UserInputInterface for Command Line interfaces</summary>
    public class CliUserInputInterface : IUserInputInterface
    {
        Mutex platforDialogMutex = new Mutex();
        object readerLock = new object();
        void IUserInputInterface.RequestUserInput(object dataObject, TimeSpan Timeout, bool modal)
        {
            if(readerThread == null)
            {
                lock (readerLock)
                {
                    if (readerThread == null)
                    {
                        readerThread = TapThread.Start(() =>
                        {
                            while (true)
                                lines.Add(Console.ReadLine());
                        }, "Console Reader");
                    }
                }
            }
            DateTime TimeoutTime;
            if (Timeout == TimeSpan.MaxValue)
                TimeoutTime = DateTime.MaxValue;
            else
                TimeoutTime = DateTime.Now + Timeout;

            if (Timeout >= new TimeSpan(0, 0, 0, 0, int.MaxValue))
                Timeout = new TimeSpan(0, 0, 0, 0, -1);
            do
            {
                if (platforDialogMutex.WaitOne(Timeout))
                    break;
                if (DateTime.Now < TimeoutTime)
                    throw new TimeoutException("Request User Input timed out");
            } while (true);

            try
            {
                Log.Flush();
                var a = AnnotationCollection.Annotate(dataObject);
                var mems = a.Get<IMembersAnnotation>()?.Members;

                if (mems == null) return;
                mems = mems.Concat(a.Get<IForwardedAnnotations>()?.Forwarded ?? Array.Empty<AnnotationCollection>());
                var title = TypeData.GetTypeData(dataObject)?.GetMember("Name")?.GetValue(dataObject) as string;
                if (string.IsNullOrWhiteSpace(title) == false)
                {
                    Console.WriteLine(title);
                }
                bool isBrowsable(IMemberData m)
                {
                    var browsable = m.GetAttribute<System.ComponentModel.BrowsableAttribute>();

                    // Browsable overrides everything
                    if (browsable != null) return browsable.Browsable;

                    if (m is IMemberData mem)
                    {
                        if (m.HasAttribute<OutputAttribute>())
                            return true;
                        if (!mem.Writable || !mem.Readable)
                            return false;
                        return true;
                    }
                    return false;
                }
                foreach (var _message in mems)
                {
                    var mem = _message.Get<IMemberAnnotation>()?.Member;
                    if (mem != null)
                    {
                        if (!isBrowsable(mem)) continue;
                    }
                    log.Flush();
                    var str = _message.Get<IStringValueAnnotation>();
                    if (str == null) continue;
                    var name = _message.Get<DisplayAttribute>()?.Name;

                    start:
                    var isVisible = _message.Get<IAccessAnnotation>()?.IsVisible ?? true;
                    if (!isVisible) continue;


                    var isReadOnly = _message.Get<IAccessAnnotation>()?.IsReadOnly ?? false;
                    if (isReadOnly)
                    {
                        Console.WriteLine($"{str.Value}");
                        continue;
                    }

                    var proxy = _message.Get<IAvailableValuesAnnotationProxy>();
                    List<string> options = null;
                    bool pleaseEnter = true;
                    if (proxy != null)
                    {
                        pleaseEnter = false;
                        options = new List<string>();
                        Console.WriteLine();

                        int index = 0;
                        var current_value = proxy.SelectedValue;
                        foreach (var value in proxy.AvailableValues)
                        {
                            var v = value.Get<IStringValueAnnotation>();
                            if (v != null)
                            {

                                Console.Write("{1}: '{0}'", v.Value, index);
                                if (value == current_value)
                                {
                                    Console.WriteLine(" (default)");
                                }
                                else
                                {
                                    Console.WriteLine();
                                }
                            }
                            options.Add(v?.Value);
                            index++;
                        }
                        Console.Write("Please enter a number or name ");
                    }

                    var layout = _message.Get<IMemberAnnotation>()?.Member.GetAttribute<LayoutAttribute>();
                    bool showName = layout?.Mode.HasFlag(LayoutMode.FullRow) == true ? false : true;
                    if (pleaseEnter)
                    {
                        Console.Write("Please enter ");
                    }
                    if (showName)
                        Console.Write($"{name} ({str.Value}): ");
                    else
                        Console.Write($"({str.Value}): ");


                    var read = (awaitReadLine(TimeoutTime) ?? "").Trim();
                    if (read == "")
                    {
                        // accept the default value.
                        continue;
                    }
                    try
                    {

                        if (options != null && int.TryParse(read, out int result))
                        {
                            if (result < options.Count)
                                read = options[result];
                            else goto start;
                        }
                        str.Value = read;
                        IEnumerable<string> errors = null;
                        var err = a.Get<IErrorAnnotation>();
                        errors = err?.Errors;

                        _message.Write();
                        if (errors.Any())
                        {
                            Console.WriteLine("Unable to parse value {0}", read);
                            goto start;
                        }

                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Unable to parse '{0}'", read);
                        goto start;
                    }
                }
                a.Write();
            }
            finally
            {
                platforDialogMutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// AwaitReadline reads the line asynchronously.  
        /// This has to be done in a thread, otherwise we cannot abort the test plan in the meantime. </summary>
        /// <param name="TimeOut"></param>
        /// <returns></returns>
        string awaitReadLine(DateTime TimeOut)
        {
            while (DateTime.Now <= TimeOut)
            {
                if (lines.TryTake(out string line, 20, TapThread.Current.AbortToken))
                    return line;
            }

            Console.WriteLine();
            log.Info("Timed out while waiting for user input. Returning a partial answer.");
            throw new TimeoutException("Request user input timed out");
        }

        TapThread readerThread = null;
        BlockingCollection<string> lines = new BlockingCollection<string>();
        
        static bool isLoaded = false;
        
        /// <summary> Loads the CLI user input interface. Note, once it is loaded it cannot be unloaded. </summary>
        public static void Load()
        {
            if (!isLoaded)
            {
                UserInput.SetInterface(new CliUserInputInterface());
                isLoaded = true;
            }
        }

        static TraceSource log = Log.CreateSource("UserInput");
    }
}
