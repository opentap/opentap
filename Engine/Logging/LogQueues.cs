//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Threading;

namespace OpenTap.Diagnostic
{
    /// <summary>
    /// Generic log message queue.
    /// </summary>
    internal class LogQueue
    {
        /// <summary>
        /// Fixed length log buffer allowing atomic lock-free insertion.
        /// </summary>
        internal class LogBuffer
        {
            /// <summary> How many log messages to make room for in the buffer.  </summary>
            public const int Capacity = 1024 * 8; 

            public LogBuffer Next = null;

            private Event[] LogEvents;
            private bool[] Written;

            private int _first;

            private int First
            {
                get
                {
                    return _first;
                }
            }

            private int LastRead
            {
                get
                {
                    for (int i = 0; i < Capacity; i++)
                    {
                        if (Written[i] == false)
                            return i;
                    }
                    return Capacity;
                }
            }

            private int _last;

            private int Last
            {
                get
                {
                    return _last;
                }
            }

            public bool Empty
            {
                get { return (Last <= First) || (First >= Capacity); }
            }

            public bool Done
            {
                get { return Last >= (Capacity - 1); }
            }

            public LogBuffer()
            {
                LogEvents = new Event[Capacity];
                Written = new bool[Capacity];
            }

            public bool PushMessage(string source, string message, long time, long duration, int eventType)
            {
                var index = Interlocked.Increment(ref _last) - 1;

                if (index > (Capacity - 1))
                    return false;

                LogEvents[index] = new Event
                {
                    Source = source, Message = message, Timestamp = time, DurationNS = duration, EventType = eventType
                };
                
                Written[index] = true;

                return true;
            }

            public ArraySegment<Event> PopCurrent()
            {
                int oldFirst = _first;
                _first = LastRead;
                int newFirst = First;

                if (newFirst > Capacity)
                    newFirst = Capacity;

                if (newFirst > oldFirst)
                    return new ArraySegment<Event>(LogEvents, oldFirst, newFirst - oldFirst);

                return new ArraySegment<Event>(LogEvents, 0, 0);
            }
        }

        private LogBuffer _first;
        private LogBuffer _last;
        private long _postedMessages;

        public long PostedMessages
        {
            get
            {
                return _postedMessages;
            }

            set
            {
                _postedMessages = value;
            }
        }

        object lck = new object();
        public void Enqueue(string source, string message, long time, long duration, int eventType)
        {
            Interlocked.Increment(ref _postedMessages);

            while (true)
            {
                LogBuffer buf = _last;

                if (!buf.PushMessage(source, message, time, duration, eventType))
                {
                    lock (lck)
                    {
                        if (!buf.Done)
                            continue;
                        LogBuffer nb = new LogBuffer();
                        if (Interlocked.CompareExchange(ref _last, nb, buf) == buf)
                            buf.Next = nb;
                    }
                }
                else
                {
                    break;
                }
            }
        }

        public Event[] DequeueBunch()
        {
            LogBuffer buf = _first;
            if (buf.Empty)
            {
                if (buf.Done)
                {
                    var oldFirst = _first;
                    var next = oldFirst.Next;

                    if (next != null)
                        Interlocked.CompareExchange(ref _first, next, oldFirst);
                }

                return null;
            }

            var res = buf.PopCurrent();

            if (buf.Done && buf.Empty)
            {
                var oldFirst1 = _first;
                var next1 = oldFirst1.Next;

                if (next1 != null)
                    Interlocked.CompareExchange(ref _first, next1, oldFirst1);
            }

            if ((res.Offset == 0) && (res.Count == res.Array.Length))
            {
                return res.Array;
            }

            Event[] res2 = new Event[res.Count];
            Array.Copy(res.Array, res.Offset, res2, 0, res.Count);
            return res2;
        }

        public int DequeueBunch(ref Event[] into)
        {
            LogBuffer buf = _first;
            if (buf.Empty)
            {
                if (buf.Done)
                {
                    var oldFirst = _first;
                    var next = oldFirst.Next;

                    if (next != null)
                        Interlocked.CompareExchange(ref _first, next, oldFirst);
                }
                return 0;
            }

            var res = buf.PopCurrent();

            if (buf.Done && buf.Empty)
            {
                var oldFirst1 = _first;
                var next1 = oldFirst1.Next;

                if (next1 != null)
                    Interlocked.CompareExchange(ref _first, next1, oldFirst1);
            }

            if (res.Count != into.Length)
            {
                Array.Resize(ref into, res.Count);
            }

            Array.Copy(res.Array, res.Offset, into, 0, res.Count);
            return res.Count;
        }

        public LogQueue()
        {
            _first = new LogBuffer();
            _last = _first;
        }

        public bool IsEmpty
        {
            get
            {
                LogBuffer buf = _first;

                return buf.Empty && (buf.Next == null);
            }
        }
    }
}
