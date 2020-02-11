using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using OpenTap.Plugins.BasicSteps;

namespace OpenTap.UnitTests
{
    [TestFixture]
    public class ScpiInstrumentTest
    {
        class DummyScpiIo : IScpiIO2
        {
            public ScpiIOResult DeviceClear()
            {
                errors.Clear();   
                return ScpiIOResult.Success;
            }

            public ScpiIOResult ReadSTB(ref byte stb)
            {
                throw new NotSupportedException();
            }

            public ScpiIOResult Read(ArraySegment<byte> buffer, int count, ref bool eoi, ref int read)
            {
                int offset = 0;
                while (responses.Count > 0 && offset < count)
                {
                    ((IList<byte>)buffer)[offset] = responses.Dequeue();
                    offset += 1;
                }

                read = offset;

                return ScpiIOResult.Success;
            }

            public ScpiIOResult Write(ArraySegment<byte> buffer, int count, ref int written)
            {
                var cmd = UTF8Encoding.UTF8.GetString(buffer.ToArray());
                written = count;
                HandleCommand(cmd);
                return ScpiIOResult.Success;
            }

            void response(string response)
            {
                foreach(var b in UTF8Encoding.UTF8.GetBytes(response))
                    responses.Enqueue(b);
            }

            TraceSource log = Log.CreateSource("ScpiIo");
            Queue<string> errors = new Queue<string>();

            public void PushError(int code, string err)
            {
                errors.Enqueue(string.Format("{0},\"{1}\"", code, err));
            }
            void HandleCommand(string cmd)
            {
                if (cmd == "*IDN?")
                {
                    response("DummyInstrument");
                }else if (cmd == "*RST")
                {
                    log.Debug("Reset");
                }else if (cmd == "SYST:ERR?")
                {
                    if (errors.Count == 0)
                        PushError(0, "No Error");
                    response(errors.Dequeue());
                    
                } else if (cmd == "*CLS")
                {
                    errors.Clear();
                    responses.Clear();
                }
                else
                {
                    PushError(100, "Unknown command.");
                }
            }
            
            Queue<byte> responses = new Queue<byte>();

            public ScpiIOResult Lock(ScpiLockType lockType, string sharedKey = null)
            {
                return ScpiIOResult.Success;
            }

            public ScpiIOResult Unlock()
            {
                return ScpiIOResult.Success;
            }

            public bool SendEnd { get; set; }
            public int IOTimeoutMS { get; set; }
            public int LockTimeoutMS { get; set; }
            public byte TerminationCharacter { get; set; }
            public bool UseTerminationCharacter { get; set; }
            public string ResourceClass { get; } = "instr";
            public ScpiIOResult Open(string visaAddress, bool @lock)
            {
                return ScpiIOResult.Success;
            }

            public ScpiIOResult Close()
            {
                return ScpiIOResult.Success;
            }

            public int ID { get; }
#pragma warning disable 67
            public event ScpiIOSrqDelegate SRQ;
#pragma warning restore 67
            public void OpenSRQ()
            {
                
            }

            public void CloseSRQ()
            {
                
            }
        }
        
        class DummyScpiInstrument : ScpiInstrument
        {
            public DummyScpiInstrument() : base(new DummyScpiIo())
            {
                IO.SRQ += sender => Log.Debug("SRQ"); // unused.
            }
            
            public DummyScpiIo IO => ((IScpiInstrument)this).IO as DummyScpiIo;
        }

        [Test]
        public void ScpiInstrumentOpenCloseNoStbError()
        {
            var scpi = new DummyScpiInstrument();
            scpi.Open();
            var eer = scpi.QueryErrors();
            Assert.AreEqual(0, eer.Count);
            scpi.IO.PushError(42, "Test Error");
            eer = scpi.QueryErrors();
            Assert.AreEqual(1, eer.Count);
            Assert.AreEqual(42, eer[0].Code);
            
            eer = scpi.QueryErrors();
            Assert.AreEqual(0, eer.Count);
            scpi.Close();
        }        
    }
}