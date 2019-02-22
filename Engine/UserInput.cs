//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Threading;

namespace OpenTap
{
    /// <summary>
    /// Class for getting user input without using GUI.
    /// </summary>
    public class UserInput
    {
        /// <summary> Request user input from the GUI.</summary>
        /// <param name="dataObject">The object the user should fill out with data.</param>
        /// <param name="Timeout">How long the user should have.</param>
        /// <param name="modal">set to True if a modal request is wanted. This means the user will have to answer before doing anything else.</param>
        public static void Request(object dataObject, TimeSpan Timeout, bool modal = false)
        {
            inputInterface?.RequestUserInput(dataObject, Timeout, modal);
        }

        /// <summary> Request user input from the GUI.</summary>
        /// <param name="dataObject">The object the user should fill out with data.</param>
        /// <param name="modal">set to True if a modal request is wanted. This means the user will have to answer before doing anything else.</param>
        public static void Request(object dataObject, bool modal = false)
        {
            inputInterface?.RequestUserInput(dataObject, TimeSpan.MaxValue, modal);
        }

        static IUserInputInterface inputInterface;
        /// <summary> Sets the current user input interface.</summary>
        /// <param name="inputInterface"></param>
        public static void SetInterface(IUserInputInterface inputInterface)
        {
            UserInput.inputInterface = inputInterface;
        }
    }

    /// <summary> Defines a way for TAP to request input from the user. </summary>
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
        /// <summary>
        /// How much height should the input fill take. 
        /// </summary>
        public int RowHeight { get; }

        /// <summary> </summary>
        /// <param name="mode"></param>
        /// <param name="rowHeight"></param>
        public LayoutAttribute(LayoutMode mode, int rowHeight = 1)
        {
            Mode = mode;
            RowHeight = rowHeight;
        }
    }

    /// <summary> Specifies that a property finalizes input.</summary>
    public class SubmitAttribute : Attribute { }

    /// <summary> Standard implementation of UserInputInterface for Command Line interfaces</summary>
    public class CliUserInputInterface : IUserInputInterface
    {
        static Mutex platforDialogMutex = new Mutex();
        void IUserInputInterface.RequestUserInput(object dataObject, TimeSpan Timeout, bool modal)
        {
            if(Timeout == TimeSpan.Zero)
            {
                if (!platforDialogMutex.WaitOne())
                {
                    return;
                }
            }
            else
            {
                if (!platforDialogMutex.WaitOne(Timeout))
                {
                    return;
                }
            }
            try
            {
                DateTime TimeoutTime = DateTime.Now + (Timeout == TimeSpan.Zero ? TimeSpan.FromDays(1000) : Timeout);
                Log.Flush();
                var a = AnnotationCollection.Annotate(dataObject);
                var mems = a.Get<IMembersAnnotation>()?.Members;
                if (mems == null) return;

                var title = TypeInfo.GetTypeInfo(dataObject)?.GetMember("Name")?.GetValue(dataObject) as string;
                if (string.IsNullOrWhiteSpace(title) == false)
                {
                    Console.WriteLine(title);
                }

                foreach (var _message in mems)
                {
                    var str = _message.Get<IStringValueAnnotation>();
                    var name = _message.Get<DisplayAttribute>()?.Name;

                    start:
                    Console.Write(name);
                    Console.WriteLine(" " + str.Value);
                    var proxy = _message.Get<IAvailableValuesAnnotationProxy>();
                    if (proxy != null)
                    {
                        Console.WriteLine("Please write one of the following:");
                        foreach (var value in proxy.AvailableValues)
                        {
                            var v = value.Get<IStringValueAnnotation>();
                            if (v != null)
                            {
                                Console.WriteLine("* {0}", v.Value);
                            }
                        }
                        Console.WriteLine();
                    }

                    var read = (AwaitReadline(TimeoutTime) ?? "").Trim();

                    try
                    {
                        str.Value = read;
                        Console.WriteLine("{0}", str.Value);
                        a.Write();
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Unable to parse '{0}'", read);

                        if (proxy != null)
                        {
                            Console.WriteLine("Please write one of the following:");
                            foreach (var value in proxy.AvailableValues)
                            {
                                var v = value.Get<IStringValueAnnotation>();
                                if (v != null)
                                {
                                    Console.WriteLine("* {0}", v.Value);
                                }
                            }
                            Console.WriteLine();
                        }
                        goto start;
                    }
                }
            }
            finally
            {
                platforDialogMutex.ReleaseMutex();
            }
        }

        private static string AwaitReadline(DateTime TimeOut)
        {
            string Result = "";

            while (DateTime.Now <= TimeOut)
            {
                if (Console.KeyAvailable)
                {
                    ConsoleKeyInfo Key = Console.ReadKey();

                    if (Key.Key == ConsoleKey.Enter)
                    {
                        return Result;
                    }
                    else
                    {
                        Result += Key.KeyChar;
                    }
                }
                else
                {
                    TapThread.Sleep(20);
                }
            }

            Console.WriteLine();
            log.Info("Timed out while waiting for user input. Returning default answer.");
            throw new TimeoutException();
        }
        static TraceSource log = Log.CreateSource("UserInput");
    }


}
