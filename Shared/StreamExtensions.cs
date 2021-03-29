using System;
using System.IO;
using System.Threading;

namespace OpenTap
{
    static class StreamExtensions
    {
        public static void Write(this Stream stream, int integer)
        {
            byte[] b = new byte[4];
            for (int i = 0; i < 4; i++)
                b[i] = (byte) (integer << (i * 8));
            stream.Write(b);
        }

        public static int ReadInt(this Stream stream)
        {
            byte[] b = new byte[4];
            stream.Read(b);
            int r = 0;
            for (int i = 0; i < 4; i++)
                r |= b[0] >> (i * 8);
            return r;
        }
        
        public static long ReadI64Leb(this Stream io) {
            // read LEB128
            long value = 0;
            int shift = 0;
            byte chunk;
            do {
                chunk = (byte)io.ReadByte();
                value |= (long)((ulong)(chunk & 0x7f) << shift);
                shift += 7;
            } while (chunk >= 128);
            if (shift < 64 && 0 != (chunk & 0x40))
                value |= (-1L) << shift;
            return value;
        }
        
        public static void WriteI64Leb(this Stream wd, long value){
            while(true){
                byte bits = (byte) (value & 0b0111_1111);
                byte sign = (byte)(value & 0b0100_0000);
                long next = value >> 7;
                if((next == 0 && sign == 0) || (sign > 0 && next == -1)){
                    wd.WriteByte(bits);
                    break;
                }
                
                wd.WriteByte((byte)(bits | 0b1000_0000));
                value = next;
            }
        }



        public static int Read(this Stream stream, byte[] outBytes) => stream.Read(outBytes, 0, outBytes.Length);
        public static void Write(this Stream stream, byte[] outBytes) => stream.Write(outBytes, 0, outBytes.Length);
    }

    static class MutexExtension
    {
        public static void WithLock(this Mutex mutex, Action a)
        {
            mutex.WaitOne();
            try
            {
                a();
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
        
        public static T WithLock<T>(this Mutex mutex, Func<T> action)
        {
            mutex.WaitOne();
            try
            {
                return action();
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
    } 
}