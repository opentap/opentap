//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.IO;
using OpenTap.Diagnostic;
using System.Threading;
using System.Collections.Generic;
using System.Text;

namespace OpenTap
{
    /// <summary>
    /// TraceListener to be used in the App.Config file of the executable to write trace/log data to
    /// a file.
    /// </summary>
    class FileTraceListener : TextWriterTraceListener
    {
        string _fileName;
        
        /// <summary>
        /// If the log should be written with absolute or relative time.
        /// </summary>
        public bool IsRelative { get; set; }
        /// <summary>
        /// Initializes a new instance of the FileTraceListener class.
        /// </summary>
        /// <param name="fileName">Name of the file to write to.</param>
        public FileTraceListener(string fileName)
            : base(fileName)
        {
            _fileName = Path.GetFullPath(fileName);
        }

        public event EventHandler FileSizeLimitReached;

        /// <summary> Installs a file limit. When the limit is reached FileSIzeLimitReached is invoked. </summary>
        public ulong FileSizeLimit = ulong.MaxValue;

        /// <summary>
        ///  Initializes a new instance of the <see cref="OpenTap.FileTraceListener"/>
        ///  class, using the stream as the recipient of the debugging and tracing output.
        /// </summary>
        /// <param name="stream">A System.IO.Stream that represents the stream the System.Diagnostics.TextWriterTraceListener writes to.</param>
        public FileTraceListener(Stream stream) : base(stream)
        {
        }
        
        internal void ChangeFileName(string fileName, bool noExclusiveWriteLock)
        {
            string dir = Path.GetDirectoryName(fileName);
            if (string.IsNullOrWhiteSpace(dir) == false)
                Directory.CreateDirectory(Path.GetDirectoryName(fileName));

            StreamWriter newwriter = null;
            if (noExclusiveWriteLock)
            {
                // Initialize a stream where the underlying file can be deleted. If the file is deleted, writes just go into the void.
                var stream = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.Read | FileShare.Delete);
                newwriter = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            }
            else
            {
                newwriter = new StreamWriter(fileName, true, System.Text.Encoding.UTF8) { AutoFlush = true };
            }
            
            var OldWriter = base.Writer;
            base.Writer = newwriter;
            
            OldWriter.Close();
            if (_fileName != null)
            {
                Writer.Write(File.ReadAllText(_fileName));
                File.Delete(_fileName);
            }

            _fileName = Path.GetFullPath(fileName);
        }
        
        long first_timestamp = -1;
        static readonly ThreadLocal<StringBuilder> _sb = new ThreadLocal<StringBuilder>(() => new StringBuilder());
        public override void TraceEvents(IEnumerable<Event> events)
        {
            // Note, this function is heavily optimized.
            // profile carefully after doing any changes!!

            if (events == null)
                throw new ArgumentNullException(nameof(events));

            base.TraceEvents(events);

            var sb = _sb.Value;
            
            sb.Clear();
            long lastTick = 0;
            string tickmsg = string.Empty;
            foreach (var evt in events)
            {
                if (first_timestamp == -1)
                    first_timestamp = evt.Timestamp;

                if (lastTick != evt.Timestamp)
                {   // lastTick is to ms resolution
                    // If its the same, dont waste time generating a new string.
                    if (IsRelative)
                    {
                        var elapsed = TimeSpan.FromTicks(evt.Timestamp - first_timestamp);
                        tickmsg = elapsed.ToString("hh\\:mm\\:ss\\.ffffff");
                    }
                    else
                    {
                        var time = new DateTime(evt.Timestamp);
                        tickmsg = time.ToString("yyyy-MM-dd HH:mm:ss.ffffff");
                    }
                    lastTick = evt.Timestamp;
                }

                sb.Append(tickmsg);
                sb.Append(" ; ");
                sb.Append(evt.Source);
                int paddingcount = 14 - evt.Source.Length;
                if (paddingcount > 0)
                    sb.Append(' ', paddingcount);
                
                sb.Append(" ; ");
                switch ((LogEventType)evt.EventType)
                {   // All these should have same padding, but dont calculate runtime.
                    case LogEventType.Debug:
                        sb.Append("Debug      ");
                        break;
                    case LogEventType.Information:
                        sb.Append("Information");
                        break;
                    case LogEventType.Warning:
                        sb.Append("Warning    ");
                        break;
                    case LogEventType.Error:
                        sb.Append("Error      ");
                        break;
                }
                
                sb.Append(" ; ");
                if (evt.Message != null)
                {
                    sb.Append(evt.Message.Replace("\n", string.Empty).Replace("\r", string.Empty));
                }
                sb.AppendLine();
            }
            Write(sb.ToString());

            if (FileSizeLimitReached != null && Writer is StreamWriter sw && (ulong)sw.BaseStream.Length > FileSizeLimit)
            {
                FileSizeLimitReached(this, EventArgs.Empty);
            }
        }
    }
}
