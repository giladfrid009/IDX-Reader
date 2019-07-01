using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;

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

        // todo: increase performence. rewrite better
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
                        
                        for (int j = 0; j < totalSize; j += dataSize)
                        {
                            for (int k = dataSize - 1; k >= 0; k--)
                            {
                                Buffer.SetByte(element, j + k, reader.ReadByte());
                            }
                        }
                                                
                        yield return (TElement)(object)element;
                    }
                }
                else
                {
                    for (int i = 0; i < fileProperties.DataLength; i++)
                    {
                        var element = Array.CreateInstance(fileProperties.DataType, fileProperties.ElementDims);

                        for (int j = 0; j < totalSize; j++)
                        {
                            Buffer.SetByte(element, j, reader.ReadByte());
                        }

                        yield return (TElement)(object)element;
                    }
                }
            }
        }

        private static Func<byte[], object> GetConverterFunc(Type destType)
        {
            switch (destType.Name)
            {
                case "Byte": return (bytes) => bytes[0];

                case "SByte": return (bytes) => unchecked((sbyte)bytes[0]);

                case "Int16": return (bytes) => BitConverter.ToInt16(bytes);

                case "Int32": return (bytes) => BitConverter.ToInt32(bytes);

                case "Single": return (bytes) => BitConverter.ToSingle(bytes);

                case "Double": return (bytes) => BitConverter.ToDouble(bytes);

                default: throw new Exception("Unsupported conversionType.");
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

                var converter = GetConverterFunc(fileProperties.DataType);
                var dataSize = Marshal.SizeOf(fileProperties.DataType);

                if (BitConverter.IsLittleEndian != isLittleEndian)
                {
                    for (int i = 0; i < fileProperties.DataLength; i++)
                    {
                        byte[] element = new byte[dataSize];
                        
                        for (int j = dataSize - 1; j >= 0; j--)
                        {
                            element[j] = reader.ReadByte();
                        }
                        
                       yield return (TElement)converter(element);
                    }
                }
                else
                {
                    for (int i = 0; i < fileProperties.DataLength; i++)
                    {
                        yield return (TElement)converter(reader.ReadBytes(dataSize));
                    }
                }
            }
        }
    }
}
