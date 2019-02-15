//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenTap.Diagnostic
{
    /// <summary>
    /// A simple log listener that writes events to a binary file.
    /// </summary>
    public class LogFile : ILogListener, IDisposable
    {
        private readonly StreamWriter _sw;

        /// <summary>
        /// Initializes a new instance of the <see cref="LogFile"/> class.
        /// Logs events to a given stream.
        /// </summary>
        /// <param name="st">The stream to use.</param>
        public LogFile(Stream st)
        {
            if (st == null)
                throw new ArgumentNullException("st");
            _sw = new StreamWriter(st, Encoding.UTF8, 1024 * 1024);
        }

        /// <summary>
        /// Implements the ILogListener method.
        /// </summary>
        /// <param name="logEvents">Events array.</param>
        public void EventsLogged(IEnumerable<Event> logEvents)
        {
            if (logEvents == null)
                throw new ArgumentNullException("logEvent");
            foreach (var evt in logEvents)
                WriteLog(evt);
        }

        /// <summary>
        /// This method must be called to close the stream.
        /// </summary>
        public void Close()
        {
            _sw.Flush();
            _sw.Close();
        }

        internal void WriteLog(Event evt)
        {
            _sw.WriteLine("{3} ; {0,-13} ; {1,-11} ; {2}", evt.Source, evt.EventType, evt.Message.Replace("\n", " ").Replace("\r", string.Empty), evt.Timestamp);
        }

        /// <summary>
        /// Flush the current stream.
        /// </summary>
        public void Flush()
        {
            _sw.Flush();
        }

        /// <summary>
        /// Dispose method since BufferedStream implements IDisposable.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose method since BufferedStream implements IDisposable.
        /// </summary>
        /// <param name="cleanupScope">If false, cleanup native resources, if true, clean up native and managed.</param>
        protected virtual void Dispose(bool cleanupScope)
        {
            if (_sw != null)
            {
                _sw.Dispose();
            }
        }
    }

    /// <summary>
    /// A simple log listener that writes events to a  binary file.
    /// </summary>
    public class BinaryLog : ILogListener, IDisposable
    {
        private Stream str;

        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryLog"/> class.
        /// Logs events to a given stream.
        /// </summary>
        /// <param name="st">The stream to use.</param>
        public BinaryLog(Stream st)
        {
            if (st == null)
                throw new ArgumentNullException("st");
            str = new BufferedStream(st, 1024 * 1024);
        }

        /// <summary>
        /// Method that must be called to properly close the stream.  
        /// </summary>
        public void Close()
        {
            str.Flush();
            str.Close();
        }

        /// <summary>
        /// Flushes the messages from the listener.  
        /// </summary>
        public void Flush()
        {
            str.Flush();
        }

        /// <summary>
        /// Logs the events.  
        /// </summary>
        /// <param name="logEvents">List of events to log.</param>
        public void EventsLogged(IEnumerable<Event> logEvents)
        {
            if (logEvents == null)
                throw new ArgumentNullException("logEvents");
            foreach (var evt in logEvents)
            {
                byte[] source = Encoding.UTF8.GetBytes(evt.Source);
                byte[] message = Encoding.UTF8.GetBytes(evt.Message);

                Int32 len =
                    4 +
                    8 +
                    8 +
                    4 + message.Length +
                    4 + source.Length;

                str.Write(BitConverter.GetBytes(len), 0, 4);

                str.Write(BitConverter.GetBytes((Int32)evt.EventType), 0, 4);
                str.Write(BitConverter.GetBytes(evt.Timestamp), 0, 8);
                str.Write(BitConverter.GetBytes(evt.DurationNS), 0, 8);

                str.Write(BitConverter.GetBytes((Int32)message.Length), 0, 4);
                str.Write(message, 0, message.Length);

                str.Write(BitConverter.GetBytes((Int32)source.Length), 0, 4);
                str.Write(source, 0, source.Length);
            }
        }

        /// <summary>
        /// Resets and releases resources for this unmanaged class
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose method since BufferedStream implements IDisposable.
        /// </summary>
        /// <param name="cleanupScope">If false, cleanup native resources, if true, clean up native and managed.</param>
        protected virtual void Dispose(bool cleanupScope)
        {
            if (str != null)
            {
                str.Dispose();
            }
        }
    }
}
