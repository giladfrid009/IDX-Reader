using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace IDXReader
{
    public class IDXProperties
    {
        public Type DataType { get; } // todo: not a clone! clone in constructor somehow

        public int[] ElementDims { get; }

        public int DataLength { get; }

        public IDXProperties(int dataLength, int[] elementDims, Type dataType)
        {
            DataLength = dataLength;
            ElementDims = (int[])elementDims.Clone();
            DataType = dataType;
        }
    }

    public static class Reader
    {
        private static Type GetTypeFromVal(byte val)
        {
            switch (val)
            {
                case 0x08: return typeof(byte);
                case 0x09: return typeof(sbyte);
                case 0x0B: return typeof(short);
                case 0x0C: return typeof(int);
                case 0x0D: return typeof(float);
                case 0x0E: return typeof(double);

                default: throw new ArgumentException("Unsupported dataType");
            }
        }

        private static IDXProperties ReadProperties(BinaryReader reader, bool isLittleEndian)
        {
            bool reverse = isLittleEndian != BitConverter.IsLittleEndian;

            byte[] magicNum = reader.ReadBytes(4);

            if (reverse)
            {
                Array.Reverse(magicNum);
            }

            var elementDims = new int[magicNum[0] - 1];

            var dataType = GetTypeFromVal(magicNum[1]);

            var lengthBytes = reader.ReadBytes(4);

            if (reverse)
            {
                Array.Reverse(lengthBytes);
            }

            var dataLength = BitConverter.ToInt32(lengthBytes);

            for (int i = 0; i < elementDims.Length; i++)
            {
                var dimBytes = reader.ReadBytes(4);

                if (reverse)
                {
                    Array.Reverse(dimBytes);
                }

                elementDims[i] = BitConverter.ToInt32(dimBytes);
            }

            return new IDXProperties(dataLength, elementDims, dataType);
        }

        public static IDXProperties ReadProperties(string filePath, bool isLittleEndian)
        {
            using (BinaryReader reader = new BinaryReader(new FileStream(filePath, FileMode.Open)))
            {
                return ReadProperties(reader, isLittleEndian);
            }
        }

        public static IEnumerable<TElement> ReadFileND<TElement>(string filePath, bool isLittleEndian) where TElement : ICollection
        {
            using (BinaryReader reader = new BinaryReader(new FileStream(filePath, FileMode.Open)))
            {
                var fileProperties = ReadProperties(reader, isLittleEndian);

                if (typeof(TElement).GetElementType() != fileProperties.DataType)
                    throw new Exception("TElement does't match file's element type.");

                if (typeof(TElement).GetArrayRank() != fileProperties.ElementDims.Length)
                    throw new Exception("TElement rank doesn't match file's element rank.");

                if (fileProperties.ElementDims.Length == 0)
                    throw new Exception("File is 1D, not ND.");

                var dataSize = Marshal.SizeOf(fileProperties.DataType);
                var totalSize = fileProperties.ElementDims.Aggregate(1, (R, X) => R * X) * dataSize;

                if (BitConverter.IsLittleEndian != isLittleEndian)
                {
                    for (int i = 0; i < fileProperties.DataLength; i++)
                    {
                        var element = Array.CreateInstance(fileProperties.DataType, fileProperties.ElementDims);
                        var handle = GCHandle.Alloc(element, GCHandleType.Pinned); 
                        var ptr0 = Marshal.UnsafeAddrOfPinnedArrayElement(element, 0);

                        for (int j = 0; j < totalSize; j += dataSize)
                        {
                            for (int k = dataSize - 1; k >= 0; k--)
                            {
                                Marshal.WriteByte(ptr0, j + k, reader.ReadByte());
                            }
                        }

                        handle.Free();

                        yield return (TElement)(object)element;
                    }
                }
                else
                {
                    for (int i = 0; i < fileProperties.DataLength; i++)
                    {
                        var element = Array.CreateInstance(fileProperties.DataType, fileProperties.ElementDims);
                        var handle = GCHandle.Alloc(element, GCHandleType.Pinned);
                        var ptr0 = Marshal.UnsafeAddrOfPinnedArrayElement(element, 0);

                        for (int j = 0; j < totalSize; j++)
                        {
                            Marshal.WriteByte(ptr0, j, reader.ReadByte());
                        }

                        handle.Free();

                        yield return (TElement)(object)element;
                    }
                }
            }
        }

        public static IEnumerable<TElement> ReadFile1D<TElement>(string filePath, bool isLittleEndian) where TElement : struct
        {
            using (BinaryReader reader = new BinaryReader(new FileStream(filePath, FileMode.Open)))
            {
                var fileProperties = ReadProperties(reader, isLittleEndian);

                if (typeof(TElement) != fileProperties.DataType)
                    throw new Exception("TElement does't match file's element type.");

                if (fileProperties.ElementDims.Length != 0)
                    throw new Exception("File is ND, not 1D.");

                var dataSize = Marshal.SizeOf(fileProperties.DataType);

                if (BitConverter.IsLittleEndian != isLittleEndian)
                {
                    for (int i = 0; i < fileProperties.DataLength; i++)
                    {
                        TElement element = default;
                        var handle = GCHandle.Alloc(element, GCHandleType.Pinned);
                        var ptr0 = handle.AddrOfPinnedObject();

                        for (int j = dataSize - 1; j >= 0; j--)
                        {
                            Marshal.WriteByte(ptr0, j, reader.ReadByte());
                        }

                        handle.Free();

                        yield return element;
                    }
                }
                else
                {
                    for (int i = 0; i < fileProperties.DataLength; i++)
                    {
                        TElement element = default;
                        var handle = GCHandle.Alloc(element, GCHandleType.Pinned);
                        var ptr0 = handle.AddrOfPinnedObject();

                        for (int j = 0; j < dataSize; j++)
                        {
                            Marshal.WriteByte(ptr0, j, reader.ReadByte());
                        }

                        handle.Free();

                        yield return element;
                    }
                }
            }
        }

    }
}
