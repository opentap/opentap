//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Threading.Tasks;

namespace OpenTap
{
    internal static class ConsoleUtils
    {
        internal static void printProgress(string header, long pos, long len)
        {
            // 61 Downloading 'MyPlugin2|1.3.3+Build-something' (23.26% | 1.02 kB of 4.40 kB)
            var downloadProgress = 100.0 * pos / len;
            var progressString = $"({downloadProgress:0.00}% | {Utils.BytesToReadable(pos)} of {Utils.BytesToReadable(len)})";
            Console.Write(
                $"{header} [{new string('=', (int) (30 * pos / len))}{new string(' ', (int) (30 - 30 * pos / len))}] {progressString}   \r");
        }

        public static void PrintProgressTillEnd(Task task, string header, Func<long> pos, Func<long> len)
        {
            ReportProgressTillEnd(task, header, pos, len, printProgress);
        }

        public static void ReportProgressTillEnd(Task task, string header, Func<long> pos, Func<long> len,
            Action<string, long, long> updateProgress, int updateDelayMs = 1000)
        {
            updateProgress = updateProgress ?? ((h, p, l) => { });
            

            if (task.Wait(updateDelayMs)) return;
            try
            {
                do
                {
                    updateProgress(header, pos(), len());
                } 
                while (!task.Wait(updateDelayMs));

                updateProgress(header, len(), len());
            }
            finally
            {
                Console.WriteLine();
            }
        }
        public static async Task ReportProgressTillEndAsync(Task task, string header, Func<long> pos, Func<long> len,
            Action<string, long, long> updateProgress, int updateDelayMs = 1000)
        {
            updateProgress = updateProgress ?? ((h, p, l) => { });

            if (task == await Task.WhenAny(task, Task.Delay(updateDelayMs)))
                return;
            
            try
            {
                do
                {
                    updateProgress(header, pos(), len());
                    
                    //Task.WhenAny returns the completed task.
                    // if task is completed we can stop iterating.
                } while (task != await Task.WhenAny(task, Task.Delay(updateDelayMs)));
                
                if(task.IsCanceled == false && task.IsFaulted == false)
                    updateProgress(header, len(), len()); // print 100%
                await task; // throw exceptions if necessary.
            }
            finally
            {
                Console.WriteLine();
            }
        }
    }
}
