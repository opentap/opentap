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
        static void printProgress(string header, long pos, long len)
        {
            const int MB = 1000000;
            Console.Write($"{header} [{new string('=', (int)(30 * pos / len))}{new string(' ', (int)(30 - 30 * pos / len))}] {100.0 * pos / len:0.00}% ({pos / MB} of {len/MB}MB) \r");
        }
        
        public static void PrintProgressTillEnd(Task task, string header, Func<long> pos, Func<long> len)
        {
            const int update_delay_ms = 1000;

            if (task.Wait(update_delay_ms)) return;
            try
            {
                do
                {
                    printProgress(header, pos(), len());
                }
                while (!task.Wait(update_delay_ms));
                printProgress(header, len(), len());
            }
            finally
            {
                Console.WriteLine();
            }
        }

    }
}