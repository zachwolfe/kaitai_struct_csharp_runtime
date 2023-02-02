using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Globalization;
using System.Text;

namespace Kaitai
{
    /// <summary>
    /// The base Kaitai stream which exposes an API for the Kaitai Struct framework.
    /// It's based off a <code>BinaryReader</code>, which is a little-endian reader.
    /// </summary>
    public partial class KaitaiStream : BinaryReader
    {
        #region Constructors

        public KaitaiStream(Stream stream) : base(stream)
        {
            Writer = new BinaryWriter(stream, Encoding.UTF8, true);
        }

        ///<summary>
        /// Creates a KaitaiStream backed by a file (RO)
        ///</summary>
        public KaitaiStream(string file) : this(File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
        }

        ///<summary>
        ///Creates a KaitaiStream backed by a byte buffer
        ///</summary>
        public KaitaiStream(byte[] bytes) : this(new MemoryStream(bytes))
        {
        }

        ///<summary>
        ///Creates a KaitaiStream backed by a newly-allocated byte buffer of a given size
        ///</summary>
        public KaitaiStream(long size) : this(Enumerable.Repeat<byte>(0, Convert.ToInt32(size)).ToArray())
        {
        }

        private BinaryWriter Writer;
        private int BitsLeft = 0;
        private ulong Bits = 0;
        private bool BitsLe = false;
        private bool BitsWriteMode = false;

        private WriteBackHandler? WBHandler = null;

        private List<KaitaiStream> ChildStreams = new List<KaitaiStream>();

        static readonly bool IsLittleEndian = BitConverter.IsLittleEndian;

        #endregion

        #region Stream positioning

        /// <summary>
        /// Check if the stream position is at the end of the stream
        /// </summary>
        public bool IsEof
        {
            get { return BaseStream.Position >= BaseStream.Length && (BitsWriteMode || BitsLeft == 0); }
        }

        /// <summary>
        /// Seek to a specific position from the beginning of the stream
        /// </summary>
        /// <param name="position">The position to seek to</param>
        public void Seek(long position)
        {
            if (BitsWriteMode)
            {
                WriteAlignToByte();
            }
            else
            {
                AlignToByte();
            }
            BaseStream.Seek(position, SeekOrigin.Begin);
        }

        /// <summary>
        /// Get the current position in the stream
        /// </summary>
        public long Pos
        {
            get { return BaseStream.Position + ((BitsWriteMode && BitsLeft > 0) ? 1 : 0); }
        }

        /// <summary>
        /// Get the total length of the stream (ie. file size)
        /// </summary>
        public long Size
        {
            get { return BaseStream.Length; }
        }

        #endregion

        #region Reading

        #region Integer types

        #region Signed

        /// <summary>
        /// Read a signed byte from the stream
        /// </summary>
        /// <returns></returns>
        public sbyte ReadS1()
        {
            AlignToByte();
            return ReadSByte();
        }

        #region Big-endian

        /// <summary>
        /// Read a signed short from the stream (big endian)
        /// </summary>
        /// <returns></returns>
        public short ReadS2be()
        {
            return BitConverter.ToInt16(ReadBytesNormalisedBigEndian(2), 0);
        }

        /// <summary>
        /// Read a signed int from the stream (big endian)
        /// </summary>
        /// <returns></returns>
        public int ReadS4be()
        {
            return BitConverter.ToInt32(ReadBytesNormalisedBigEndian(4), 0);
        }

        /// <summary>
        /// Read a signed long from the stream (big endian)
        /// </summary>
        /// <returns></returns>
        public long ReadS8be()
        {
            return BitConverter.ToInt64(ReadBytesNormalisedBigEndian(8), 0);
        }

        #endregion

        #region Little-endian

        /// <summary>
        /// Read a signed short from the stream (little endian)
        /// </summary>
        /// <returns></returns>
        public short ReadS2le()
        {
            return BitConverter.ToInt16(ReadBytesNormalisedLittleEndian(2), 0);
        }

        /// <summary>
        /// Read a signed int from the stream (little endian)
        /// </summary>
        /// <returns></returns>
        public int ReadS4le()
        {
            return BitConverter.ToInt32(ReadBytesNormalisedLittleEndian(4), 0);
        }

        /// <summary>
        /// Read a signed long from the stream (little endian)
        /// </summary>
        /// <returns></returns>
        public long ReadS8le()
        {
            return BitConverter.ToInt64(ReadBytesNormalisedLittleEndian(8), 0);
        }

        #endregion

        #endregion

        #region Unsigned

        /// <summary>
        /// Read an unsigned byte from the stream
        /// </summary>
        /// <returns></returns>
        public byte ReadU1()
        {
            AlignToByte();
            return ReadByte();
        }

        #region Big-endian

        /// <summary>
        /// Read an unsigned short from the stream (big endian)
        /// </summary>
        /// <returns></returns>
        public ushort ReadU2be()
        {
            return BitConverter.ToUInt16(ReadBytesNormalisedBigEndian(2), 0);
        }

        /// <summary>
        /// Read an unsigned int from the stream (big endian)
        /// </summary>
        /// <returns></returns>
        public uint ReadU4be()
        {
            return BitConverter.ToUInt32(ReadBytesNormalisedBigEndian(4), 0);
        }

        /// <summary>
        /// Read an unsigned long from the stream (big endian)
        /// </summary>
        /// <returns></returns>
        public ulong ReadU8be()
        {
            return BitConverter.ToUInt64(ReadBytesNormalisedBigEndian(8), 0);
        }

        #endregion

        #region Little-endian

        /// <summary>
        /// Read an unsigned short from the stream (little endian)
        /// </summary>
        /// <returns></returns>
        public ushort ReadU2le()
        {
            return BitConverter.ToUInt16(ReadBytesNormalisedLittleEndian(2), 0);
        }

        /// <summary>
        /// Read an unsigned int from the stream (little endian)
        /// </summary>
        /// <returns></returns>
        public uint ReadU4le()
        {
            return BitConverter.ToUInt32(ReadBytesNormalisedLittleEndian(4), 0);
        }

        /// <summary>
        /// Read an unsigned long from the stream (little endian)
        /// </summary>
        /// <returns></returns>
        public ulong ReadU8le()
        {
            return BitConverter.ToUInt64(ReadBytesNormalisedLittleEndian(8), 0);
        }

        #endregion

        #endregion

        #endregion

        #region Floating point types

        #region Big-endian

        /// <summary>
        /// Read a single-precision floating point value from the stream (big endian)
        /// </summary>
        /// <returns></returns>
        public float ReadF4be()
        {
            return BitConverter.ToSingle(ReadBytesNormalisedBigEndian(4), 0);
        }

        /// <summary>
        /// Read a double-precision floating point value from the stream (big endian)
        /// </summary>
        /// <returns></returns>
        public double ReadF8be()
        {
            return BitConverter.ToDouble(ReadBytesNormalisedBigEndian(8), 0);
        }

        #endregion

        #region Little-endian

        /// <summary>
        /// Read a single-precision floating point value from the stream (little endian)
        /// </summary>
        /// <returns></returns>
        public float ReadF4le()
        {
            return BitConverter.ToSingle(ReadBytesNormalisedLittleEndian(4), 0);
        }

        /// <summary>
        /// Read a double-precision floating point value from the stream (little endian)
        /// </summary>
        /// <returns></returns>
        public double ReadF8le()
        {
            return BitConverter.ToDouble(ReadBytesNormalisedLittleEndian(8), 0);
        }

        #endregion

        #endregion

        #region Unaligned bit values

        public void AlignToByte()
        {
            BitsLeft = 0;
            Bits = 0;
        }

        /// <summary>
        /// Read a n-bit integer in a big-endian manner from the stream
        /// </summary>
        /// <returns></returns>
        public ulong ReadBitsIntBe(int n)
        {
            BitsWriteMode = false;

            ulong res = 0;

            int bitsNeeded = n - BitsLeft;
            BitsLeft = -bitsNeeded & 7; // `-bitsNeeded mod 8`

            if (bitsNeeded > 0)
            {
                // 1 bit  => 1 byte
                // 8 bits => 1 byte
                // 9 bits => 2 bytes
                int bytesNeeded = ((bitsNeeded - 1) / 8) + 1; // `ceil(bitsNeeded / 8)`
                byte[] buf = ReadBytesNotAligned(bytesNeeded);
                for (int i = 0; i < bytesNeeded; i++)
                {
                    res = res << 8 | buf[i];
                }

                ulong newBits = res;
                res = res >> BitsLeft | (bitsNeeded < 64 ? Bits << bitsNeeded : 0);
                Bits = newBits; // will be masked at the end of the function
            }
            else
            {
                res = Bits >> -bitsNeeded; // shift unneeded bits out
            }

            ulong mask = (1UL << BitsLeft) - 1; // `BitsLeft` is in range 0..7, so `(1UL << 64)` does not have to be considered
            Bits &= mask;

            return res;
        }

        [Obsolete("use ReadBitsIntBe instead")]
        public ulong ReadBitsInt(int n)
        {
            return ReadBitsIntBe(n);
        }

        /// <summary>
        /// Read a n-bit integer in a little-endian manner from the stream
        /// </summary>
        /// <returns></returns>
        public ulong ReadBitsIntLe(int n)
        {
            BitsWriteMode = false;

            ulong res = 0;
            int bitsNeeded = n - BitsLeft;

            if (bitsNeeded > 0)
            {
                // 1 bit  => 1 byte
                // 8 bits => 1 byte
                // 9 bits => 2 bytes
                int bytesNeeded = ((bitsNeeded - 1) / 8) + 1; // `ceil(bitsNeeded / 8)`
                byte[] buf = ReadBytesNotAligned(bytesNeeded);
                for (int i = 0; i < bytesNeeded; i++)
                {
                    res |= ((ulong)buf[i]) << (i * 8);
                }

                // NB: in C#, bit shift operators on left-hand operand of type `ulong` work
                // as if the right-hand operand were subjected to `& 63` (`& 0b11_1111`) (see
                // https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/operators/bitwise-and-shift-operators#shift-count-of-the-shift-operators),
                // so `res >> 64` is equivalent to `res >> 0` (but we don't want that)
                ulong newBits = bitsNeeded < 64 ? res >> bitsNeeded : 0;
                res = res << BitsLeft | Bits;
                Bits = newBits;
            }
            else
            {
                res = Bits;
                Bits >>= n;
            }

            BitsLeft = -bitsNeeded & 7; // `-bitsNeeded mod 8`

            if (n < 64)
            {
                ulong mask = (1UL << n) - 1;
                res &= mask;
            }
            // if `n == 64`, do nothing
            return res;
        }

        #endregion

        #region Byte arrays

        /// <summary>
        /// Read a fixed number of bytes from the stream
        /// </summary>
        /// <param name="count">The number of bytes to read</param>
        /// <returns></returns>
        public byte[] ReadBytes(long count)
        {
            if (count < 0 || count > Int32.MaxValue)
                throw new ArgumentOutOfRangeException("requested " + count + " bytes, while only non-negative int32 amount of bytes possible");
            AlignToByte();
            return ReadBytesNotAligned((int)count);
        }

        /// <summary>
        /// Read a fixed number of bytes from the stream
        /// </summary>
        /// <param name="count">The number of bytes to read</param>
        /// <returns></returns>
        public byte[] ReadBytes(ulong count)
        {
            if (count > Int32.MaxValue)
                throw new ArgumentOutOfRangeException("requested " + count + " bytes, while only non-negative int32 amount of bytes possible");
            AlignToByte();
            return ReadBytesNotAligned((int)count);
        }

        private byte[] ReadBytesNotAligned(int count)
        {
            byte[] bytes = base.ReadBytes(count);
            if (bytes.Length < (int)count)
                throw new EndOfStreamException("requested " + count + " bytes, but got only " + bytes.Length + " bytes");
            return bytes;
        }

        /// <summary>
        /// Read bytes from the stream in little endian format and convert them to the endianness of the current platform
        /// </summary>
        /// <param name="count">The number of bytes to read</param>
        /// <returns>An array of bytes that matches the endianness of the current platform</returns>
        protected byte[] ReadBytesNormalisedLittleEndian(int count)
        {
            byte[] bytes = ReadBytes(count);
            if (!IsLittleEndian) Array.Reverse(bytes);
            return bytes;
        }

        /// <summary>
        /// Read bytes from the stream in big endian format and convert them to the endianness of the current platform
        /// </summary>
        /// <param name="count">The number of bytes to read</param>
        /// <returns>An array of bytes that matches the endianness of the current platform</returns>
        protected byte[] ReadBytesNormalisedBigEndian(int count)
        {
            byte[] bytes = ReadBytes(count);
            if (IsLittleEndian) Array.Reverse(bytes);
            return bytes;
        }

        /// <summary>
        /// Read all the remaining bytes from the stream until the end is reached
        /// </summary>
        /// <returns></returns>
        public byte[] ReadBytesFull()
        {
            return ReadBytes(BaseStream.Length - BaseStream.Position);
        }

        /// <summary>
        /// Read a terminated string from the stream
        /// </summary>
        /// <param name="terminator">The string terminator value</param>
        /// <param name="includeTerminator">True to include the terminator in the returned string</param>
        /// <param name="consumeTerminator">True to consume the terminator byte before returning</param>
        /// <param name="eosError">True to throw an error when the EOS was reached before the terminator</param>
        /// <returns></returns>
        public byte[] ReadBytesTerm(byte terminator, bool includeTerminator, bool consumeTerminator, bool eosError)
        {
            AlignToByte();
            List<byte> bytes = new List<byte>();
            while (true)
            {
                if (IsEof)
                {
                    if (eosError) throw new EndOfStreamException(string.Format("End of stream reached, but no terminator `{0}` found", terminator));
                    break;
                }

                byte b = ReadByte();
                if (b == terminator)
                {
                    if (includeTerminator) bytes.Add(b);
                    if (!consumeTerminator) Seek(Pos - 1);
                    break;
                }
                bytes.Add(b);
            }
            return bytes.ToArray();
        }

        /// <summary>
        /// Read a specific set of bytes and assert that they are the same as an expected result
        /// </summary>
        /// <param name="expected">The expected result</param>
        /// <returns></returns>
        [Obsolete("use explicit \"if\" using ByteArrayCompare method instead")]
        public byte[] EnsureFixedContents(byte[] expected)
        {
            byte[] bytes = ReadBytes(expected.Length);

            if (bytes.Length != expected.Length)
            {
                throw new Exception(string.Format("Expected bytes: {0} ({1} bytes), Instead got: {2} ({3} bytes)", Convert.ToBase64String(expected), expected.Length, Convert.ToBase64String(bytes), bytes.Length));
            }
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] != expected[i])
                {
                    throw new Exception(string.Format("Expected bytes: {0} ({1} bytes), Instead got: {2} ({3} bytes)", Convert.ToBase64String(expected), expected.Length, Convert.ToBase64String(bytes), bytes.Length));
                }
            }

            return bytes;
        }

        public static byte[] BytesStripRight(byte[] src, byte padByte)
        {
            int newLen = src.Length;
            while (newLen > 0 && src[newLen - 1] == padByte)
                newLen--;

            byte[] dst = new byte[newLen];
            Array.Copy(src, dst, newLen);
            return dst;
        }

        public static byte[] BytesTerminate(byte[] src, byte terminator, bool includeTerminator)
        {
            int newLen = 0;
            int maxLen = src.Length;

            while (newLen < maxLen && src[newLen] != terminator)
                newLen++;

            if (includeTerminator && newLen < maxLen)
                newLen++;

            byte[] dst = new byte[newLen];
            Array.Copy(src, dst, newLen);
            return dst;
        }

        #endregion

        #endregion

        #region Writing

        #region Integer types

        #region Signed

        /// <summary>
        /// Write a signed byte to the stream
        /// </summary>
        public void WriteS1(sbyte v)
        {
            WriteAlignToByte();
            Writer.Write(v);
        }

        #region Big-endian

        /// <summary>
        /// Write a signed short to the stream (big endian)
        /// </summary>
        public void WriteS2be(short v)
        {
            WriteBytesNormalisedBigEndian(BitConverter.GetBytes(v));
        }

        /// <summary>
        /// Write a signed int to the stream (big endian)
        /// </summary>
        public void WriteS4be(int v)
        {
            WriteBytesNormalisedBigEndian(BitConverter.GetBytes(v));
        }

        /// <summary>
        /// Write a signed long to the stream (big endian)
        /// </summary>
        public void WriteS8be(long v)
        {
            WriteBytesNormalisedBigEndian(BitConverter.GetBytes(v));
        }

        #endregion

        #region Little-endian

        /// <summary>
        /// Write a signed short to the stream (little endian)
        /// </summary>
        public void WriteS2le(short v)
        {
            WriteBytesNormalisedLittleEndian(BitConverter.GetBytes(v));
        }

        /// <summary>
        /// Write a signed int to the stream (little endian)
        /// </summary>
        public void WriteS4le(int v)
        {
            WriteBytesNormalisedLittleEndian(BitConverter.GetBytes(v));
        }

        /// <summary>
        /// Write a signed long to the stream (little endian)
        /// </summary>
        public void WriteS8le(long v)
        {
            WriteBytesNormalisedLittleEndian(BitConverter.GetBytes(v));
        }

        #endregion

        #endregion

        #region Unsigned

        /// <summary>
        /// Write an unsigned byte to the stream
        /// </summary>
        public void WriteU1(byte v)
        {
            WriteAlignToByte();
            Writer.Write(v);
        }

        #region Big-endian

        /// <summary>
        /// Write an unsigned short to the stream (big endian)
        /// </summary>
        public void WriteU2be(ushort v)
        {
            WriteBytesNormalisedBigEndian(BitConverter.GetBytes(v));
        }

        /// <summary>
        /// Write an unsigned int to the stream (big endian)
        /// </summary>
        public void WriteU4be(uint v)
        {
            WriteBytesNormalisedBigEndian(BitConverter.GetBytes(v));
        }

        /// <summary>
        /// Write an unsigned long to the stream (big endian)
        /// </summary>
        public void WriteU8be(ulong v)
        {
            WriteBytesNormalisedBigEndian(BitConverter.GetBytes(v));
        }

        #endregion

        #region Little-endian

        /// <summary>
        /// Write an unsigned short to the stream (little endian)
        /// </summary>
        public void WriteU2le(ushort v)
        {
            WriteBytesNormalisedLittleEndian(BitConverter.GetBytes(v));
        }

        /// <summary>
        /// Write an unsigned int to the stream (little endian)
        /// </summary>
        public void WriteU4le(uint v)
        {
            WriteBytesNormalisedLittleEndian(BitConverter.GetBytes(v));
        }

        /// <summary>
        /// Write an unsigned long to the stream (little endian)
        /// </summary>
        public void WriteU8le(ulong v)
        {
            WriteBytesNormalisedLittleEndian(BitConverter.GetBytes(v));
        }

        #endregion

        #endregion

        #endregion

        #region Floating point types

        #region Big-endian

        /// <summary>
        /// Write a single-precision floating point value to the stream (big endian)
        /// </summary>
        public void WriteF4be(float v)
        {
            WriteBytesNormalisedBigEndian(BitConverter.GetBytes(v));
        }

        /// <summary>
        /// Write a double-precision floating point value to the stream (big endian)
        /// </summary>
        public void WriteF8be(double v)
        {
            WriteBytesNormalisedBigEndian(BitConverter.GetBytes(v));
        }

        #endregion

        #region Little-endian

        /// <summary>
        /// Write a single-precision floating point value to the stream (little endian)
        /// </summary>
        public void WriteF4le(float v)
        {
            WriteBytesNormalisedLittleEndian(BitConverter.GetBytes(v));
        }

        /// <summary>
        /// Write a double-precision floating point value to the stream (little endian)
        /// </summary>
        public void WriteF8le(double v)
        {
            WriteBytesNormalisedLittleEndian(BitConverter.GetBytes(v));
        }

        #endregion

        #endregion

        #region Unaligned bit values

        public void WriteAlignToByte()
        {
            if (BitsLeft > 0)
            {
                byte b = (byte)Bits;
                if (!BitsLe)
                {
                    b <<= 8 - BitsLeft;
                }
                Writer.Write(b);
                AlignToByte();
            }
        }

        /*
            Example 1 (bytesToWrite > 0):
        
            old bitsLeft = 5
                | |          new bitsLeft = 18 mod 8 = 2
               /   \             /\
              |01101xxx|xxxxxxxx|xx......|
               \    \             /
                \    \__ n = 13 _/
                 \              /
                  \____________/
                 bitsToWrite = 18  ->  bytesToWrite = 2

            ---

            Example 2 (bytesToWrite == 0):

               old bitsLeft = 1
                    |   |
                     \ /
            |01101100|1xxxxx..|........|
                     / \___/\
                    /  n = 5 \
                   /__________\
                 bitsToWrite = 6  ->  bytesToWrite = 0,
                                      new bitsLeft = 6 mod 8 = 6
         */
        public void WriteBitsIntBe(int n, ulong val) {
            BitsLe = false;
            BitsWriteMode = true;

            if (n < 64) {
                ulong mask = (1UL << n) - 1;
                val &= mask;
            }
            // if `n == 64`, do nothing

            int bitsToWrite = BitsLeft + n;
            int bytesToWrite = bitsToWrite / 8;

            BitsLeft = bitsToWrite & 7; // `bitsToWrite mod 8`

            if (bytesToWrite > 0) {
                byte[] buf = new byte[bytesToWrite];

                ulong mask = (1UL << BitsLeft) - 1; // `bitsLeft` is in range 0..7, so `(1L << 64)` does not have to be considered
                ulong newBits = val & mask;
                val = val >> BitsLeft | (n - BitsLeft < 64 ? Bits << (n - BitsLeft) : 0);
                Bits = newBits;

                for (int i = bytesToWrite - 1; i >= 0; i--) {
                    buf[i] = (byte) (val & 0xff);
                    val >>= 8;
                }
                WriteBytesNotAligned(buf);
            } else {
                Bits = Bits << n | val;
            }
        }

        /*
            Example 1 (bytesToWrite > 0):

            n = 13

               old bitsLeft = 5
                   | |             new bitsLeft = 18 mod 8 = 2
                  /   \                /\
              |xxx01101|xxxxxxxx|......xx|
               \               /      / /
                ---------------       --
                          \           /
                         bitsToWrite = 18  ->  bytesToWrite = 2

            ---

            Example 2 (bytesToWrite == 0):

                      old bitsLeft = 1
                           |   |
                            \ /
            |01101100|..xxxxx1|........|
                       /\___/ \
                      / n = 5  \
                     /__________\
                   bitsToWrite = 6  ->  bytesToWrite = 0,
                                        new bitsLeft = 6 mod 8 = 6
         */
        public void WriteBitsIntLe(int n, ulong val) {
            BitsLe = true;
            BitsWriteMode = true;

            int bitsToWrite = BitsLeft + n;
            int bytesToWrite = bitsToWrite / 8;

            int oldBitsLeft = BitsLeft;
            BitsLeft = bitsToWrite & 7; // `bitsToWrite mod 8`

            if (bytesToWrite > 0) {
                byte[] buf = new byte[bytesToWrite];

                ulong newBits = n - BitsLeft < 64 ? val >> (n - BitsLeft) : 0;
                val = val << oldBitsLeft | Bits;
                Bits = newBits;

                for (int i = 0; i < bytesToWrite; i++) {
                    buf[i] = (byte) (val & 0xff);
                    val >>= 8;
                }
                WriteBytesNotAligned(buf);
            } else {
                Bits |= val << oldBitsLeft;
            }

            ulong mask = (1UL << BitsLeft) - 1; // `bitsLeft` is in range 0..7, so `(1L << 64)` does not have to be considered
            Bits &= mask;
        }

        #endregion

        #region Byte arrays

        /// <summary>
        /// Write an array of bytes to the stream
        /// </summary>
        /// <param name="bytes">The bytes to be written</param>
        public void WriteBytes(byte[] bytes)
        {
            WriteAlignToByte();
            WriteBytesNotAligned(bytes);
        }

        private void WriteBytesNotAligned(byte[] bytes)
        {
            Writer.Write(bytes);
        }

        /// <summary>
        /// Convert bytes in the endianess of the current platform to little endian, then write them to the stream
        /// </summary>
        /// <param name="bytes">The bytes to be written</param>
        protected void WriteBytesNormalisedLittleEndian(byte[] bytes)
        {
            if (!IsLittleEndian) Array.Reverse(bytes);
            WriteBytes(bytes);
        }

        /// <summary>
        /// Convert bytes in the endianess of the current platform to big endian, then write them to the stream
        /// </summary>
        /// <param name="bytes">The bytes to be written</param>
        protected void WriteBytesNormalisedBigEndian(byte[] bytes)
        {
            if (IsLittleEndian) Array.Reverse(bytes);
            WriteBytes(bytes);
        }

        public void WriteBytesLimit(byte[] bytes, long size, byte term, byte padByte)
        {
            WriteAlignToByte();
            int len = bytes.Length;
            Writer.Write(bytes);
            if (bytes.Length < size)
            {
                Writer.Write(term);
                long padLen = size - len - 1;
                for (long i = 0; i < padLen; i++)
                    Writer.Write(padByte);
            }
            else if (len > size)
            {
                throw new ArgumentException("writing" + size + "bytes, but " + len + " bytes were given");
            }
        }

        public void WriteStream(KaitaiStream other)
        {
            WriteBytes(other.ToByteArray());
        }

        #endregion

        #endregion

        #region Byte array processing

        /// <summary>
        /// Performs XOR processing with given data, XORing every byte of the input with a single value.
        /// </summary>
        /// <param name="value">The data toe process</param>
        /// <param name="key">The key value to XOR with</param>
        /// <returns>Processed data</returns>
        public byte[] ProcessXor(byte[] value, int key)
        {
            byte[] result = new byte[value.Length];
            for (int i = 0; i < value.Length; i++)
            {
                result[i] = (byte)(value[i] ^ key);
            }
            return result;
        }

        /// <summary>
        /// Performs XOR processing with given data, XORing every byte of the input with a key
        /// array, repeating from the beginning of the key array if necessary
        /// </summary>
        /// <param name="value">The data toe process</param>
        /// <param name="key">The key array to XOR with</param>
        /// <returns>Processed data</returns>
        public byte[] ProcessXor(byte[] value, byte[] key)
        {
            int keyLen = key.Length;
            byte[] result = new byte[value.Length];
            for (int i = 0, j = 0; i < value.Length; i++, j = (j + 1) % keyLen)
            {
                result[i] = (byte)(value[i] ^ key[j]);
            }
            return result;
        }

        /// <summary>
        /// Performs a circular left rotation shift for a given buffer by a given amount of bits.
        /// Pass a negative amount to rotate right.
        /// </summary>
        /// <param name="data">The data to rotate</param>
        /// <param name="amount">The number of bytes to rotate by</param>
        /// <param name="groupSize"></param>
        /// <returns></returns>
        public byte[] ProcessRotateLeft(byte[] data, int amount, int groupSize)
        {
            if (amount > 7 || amount < -7) throw new ArgumentException("Rotation of more than 7 cannot be performed.", "amount");
            if (amount < 0) amount += 8; // Rotation of -2 is the same as rotation of +6

            byte[] r = new byte[data.Length];
            switch (groupSize)
            {
                case 1:
                    for (int i = 0; i < data.Length; i++)
                    {
                        byte bits = data[i];
                        // http://stackoverflow.com/a/812039
                        r[i] = (byte) ((bits << amount) | (bits >> (8 - amount)));
                    }
                    break;
                default:
                    throw new NotImplementedException(string.Format("Unable to rotate a group of {0} bytes yet", groupSize));
            }
            return r;
        }

        /// <summary>
        /// Inflates a deflated zlib byte stream
        /// </summary>
        /// <param name="data">The data to deflate</param>
        /// <returns>The deflated result</returns>
        public byte[] ProcessZlib(byte[] data)
        {
            // See RFC 1950 (https://tools.ietf.org/html/rfc1950)
            // zlib adds a header to DEFLATE streams - usually 2 bytes,
            // but can be 6 bytes if FDICT is set.
            // There's also 4 checksum bytes at the end of the stream.

            byte zlibCmf = data[0];
            if ((zlibCmf & 0x0F) != 0x08) throw new NotSupportedException("Only the DEFLATE algorithm is supported for zlib data.");

            const int zlibFooter = 4;
            int zlibHeader = 2;

            // If the FDICT bit (0x20) is 1, then the 4-byte dictionary is included in the header, we need to skip it
            byte zlibFlg = data[1];
            if ((zlibFlg & 0x20) == 0x20) zlibHeader += 4;

            using (MemoryStream ms = new MemoryStream(data, zlibHeader, data.Length - (zlibHeader + zlibFooter)))
            {
                using (DeflateStream ds = new DeflateStream(ms, CompressionMode.Decompress))
                {
                    using (MemoryStream target = new MemoryStream())
                    {
                        ds.CopyTo(target);
                        return target.ToArray();
                    }
                }
            }
        }

        public byte[] UnprocessZlib(byte[] data)
        {
            // See RFC 1950 (https://tools.ietf.org/html/rfc1950)
            byte zlibCmf =
                0x08 << 0 | // CM: 8 for DEFLATE
                0x07 << 4;  // CINFO: logarithm of window size, minus 8. The source for
                            // `DeflateStream` defines the default number of window bits at 15,
                            // which is 7 + 8.
                            // (see https://github.com/dotnet/runtime/blob/9dad1103b5c98a380280ee82182092153c1faa67/src/libraries/Common/src/System/IO/Compression/ZLibNative.cs#L122)

            byte zlibFlg =
                0x1A << 0 | // FCHECK: rounds CMF and FLG interpreted as a big-endian 16-bit
                            // unsigned int up to the nearest multiple of 31
                0x00 << 5 | // FDICT: no preset dictionary needed
                0x03 << 6;  // FLEVEL: optimal compression

            // Adler-32 checksum
            ushort s1 = 1;
            ushort s2 = 0;
            const ushort limit = 65521;
            foreach (byte b in data)
            {
                s1 = (ushort)((s1 + b) % limit);
                s2 = (ushort)((s2 + s1) % limit);
            }

            byte[] s1Bytes = BitConverter.GetBytes(s1);
            byte[] s2Bytes = BitConverter.GetBytes(s2);
            if (IsLittleEndian)
            {
                s1Bytes.Reverse();
                s2Bytes.Reverse();
            }

            using (MemoryStream ms = new MemoryStream(data))
            {
                using (DeflateStream ds = new DeflateStream(ms, CompressionLevel.Optimal))
                {
                    using (MemoryStream target = new MemoryStream())
                    {
                        // Header
                        target.WriteByte(zlibCmf);
                        target.WriteByte(zlibFlg);

                        // Compressed data
                        ds.CopyTo(target);

                        // Footer
                        target.Write(s2Bytes, 0, 2);
                        target.Write(s1Bytes, 0, 2);

                        return target.ToArray();
                    }
                }
            }
        }

        #endregion

        #region Misc utility methods

        private byte[] GetBackingBuffer()
        {
            if (BaseStream is MemoryStream ms)
            {
                // Neither TryGetBuffer nor GetBuffer are available on both
                // .NET Standard 1.3 and .NET Framework 4.5.
                // Fun.
#if NETSTANDARD
                // If the array segment does not refer to the entire array,
                // then we shouldn't return it.
                if (ms.TryGetBuffer(out ArraySegment<byte> segment) && segment.Offset == 0 && segment.Count == segment.Array.Length)
                {
                    return segment.Array;
                }
                else
                {
                    return null;
                }
#elif NETFRAMEWORK
                // The user might have passed in their own `MemoryStream`
                // without a publicly visible buffer to our `Stream`
                // constructor, so we must catch an exception in that case
                // See https://learn.microsoft.com/en-us/dotnet/api/system.io.memorystream.getbuffer?view=net-7.0#exceptions
                try
                {
                    return ms.GetBuffer();
                }
                catch (UnauthorizedAccessException)
                {
                    return null;
                }
#endif
            }
            else
            {
                return null;
            }
        }

        public byte[] ToByteArray()
        {
            // If this stream was created from a byte array, return it
            // directly without copying:
            byte[] backingBuffer = GetBackingBuffer();
            if (backingBuffer != null)
                return backingBuffer;

            long pos = Pos;
            Seek(0);
            byte[] r = ReadBytesFull();
            Seek(pos);
            return r;
        }

        /// <summary>
        /// Performs modulo operation between two integers.
        /// </summary>
        /// <remarks>
        /// This method is required because C# lacks a "true" modulo
        /// operator, the % operator rather being the "remainder"
        /// operator. We want mod operations to always be positive.
        /// </remarks>
        /// <param name="a">The value to be divided</param>
        /// <param name="b">The value to divide by. Must be greater than zero.</param>
        /// <returns>The result of the modulo opertion. Will always be positive.</returns>
        public static int Mod(int a, int b)
        {
            if (b <= 0) throw new ArgumentException("Divisor of mod operation must be greater than zero.", "b");
            int r = a % b;
            if (r < 0) r += b;
            return r;
        }

        /// <summary>
        /// Performs modulo operation between two integers.
        /// </summary>
        /// <remarks>
        /// This method is required because C# lacks a "true" modulo
        /// operator, the % operator rather being the "remainder"
        /// operator. We want mod operations to always be positive.
        /// </remarks>
        /// <param name="a">The value to be divided</param>
        /// <param name="b">The value to divide by. Must be greater than zero.</param>
        /// <returns>The result of the modulo opertion. Will always be positive.</returns>
        public static long Mod(long a, long b)
        {
            if (b <= 0) throw new ArgumentException("Divisor of mod operation must be greater than zero.", "b");
            long r = a % b;
            if (r < 0) r += b;
            return r;
        }

        /// <summary>
        /// Compares two byte arrays in lexicographical order.
        /// </summary>
        /// <returns>negative number if a is less than b, <c>0</c> if a is equal to b, positive number if a is greater than b.</returns>
        /// <param name="a">First byte array to compare</param>
        /// <param name="b">Second byte array to compare.</param>
        public static int ByteArrayCompare(byte[] a, byte[] b)
        {
            if (a == b)
                return 0;
            int al = a.Length;
            int bl = b.Length;
            int minLen = al < bl ? al : bl;
            for (int i = 0; i < minLen; i++) {
                int cmp = a[i] - b[i];
                if (cmp != 0)
                    return cmp;
            }

            // Reached the end of at least one of the arrays
            if (al == bl) {
                return 0;
            } else {
                return al - bl;
            }
        }

        /// <summary>
        /// Reverses the string, Unicode-aware.
        /// </summary>
        /// <a href="https://stackoverflow.com/a/15029493">taken from here</a>
        public static string StringReverse(string s)
        {
            TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(s);

            List<string> elements = new List<string>();
            while (enumerator.MoveNext())
                elements.Add(enumerator.GetTextElement());

            elements.Reverse();
            return string.Concat(elements);
        }

        public struct WriteBackHandler
        {
            private readonly long Pos;
            private readonly Action<KaitaiStream> Write;

            public WriteBackHandler(long pos, Action<KaitaiStream> write)
            {
                Pos = pos;
                Write = write;
            }

            public void WriteBack(KaitaiStream parent)
            {
                parent.Seek(Pos);
                Write(parent);
            }
        }

        public void SetWriteBackHandler(WriteBackHandler? wbHandler)
        {
            WBHandler = wbHandler;
        }

        public void AddChildStream(KaitaiStream child)
        {
            ChildStreams.Add(child);
        }

        public void WriteBackChildStreams()
        {
            WriteBackChildStreams(null);
        }

        private void WriteBackChildStreams(KaitaiStream parent)
        {
            long pos = Pos;
            foreach (KaitaiStream child in ChildStreams)
            {
                child.WriteBackChildStreams(this);
            }
            ChildStreams.Clear();
            Seek(pos);
            if (parent != null)
            {
                WriteBack(parent);
            }
        }

        private void WriteBack(KaitaiStream parent)
        {
            WBHandler.Value.WriteBack(parent);
        }

        #endregion

        #region Disposal

        override protected void Dispose(bool disposing)
        {
            try
            {
                if (BitsWriteMode) WriteAlignToByte();
            }
            finally
            {
                if (disposing) Writer.Dispose();

                base.Dispose(disposing);
            }
        }

        #endregion
    }
}
