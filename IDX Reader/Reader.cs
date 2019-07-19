using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace IDXReader
{
    /// <summary>
    /// Contains properties of an given IDX file.
    /// </summary>
    public class IDXProperties
    {
        /// <summary>
        /// Data type of the dataset.
        /// </summary>
        public Type DataType { get; }

        /// <summary>
        /// Dataset's element dimensions.
        /// </summary>
        public int[] ElementDims { get; }

        /// <summary>
        /// Dataset's length.
        /// </summary>
        public int DataLength { get; }

        /// <param name="dataLength">Dataset's length.</param>
        /// <param name="elementDims">Dataset's element dimensions.</param>
        /// <param name="dataType">Data type of the dataset.</param>
        public IDXProperties(int dataLength, int[] elementDims, Type dataType)
        {
            DataLength = dataLength;
            ElementDims = (int[])elementDims.Clone();
            DataType = dataType;
        }
    }

    /// <summary>
    /// IDX reader class.
    /// </summary>
    public static class Reader
    {
        /// <summary>
        /// Gets a type from a byte value encoding.
        /// </summary>
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

        /// <summary>
        /// Reads the properties of an IDX file.
        /// </summary>
        /// <param name="reader">BinaryReader to rarget file.</param>
        /// <param name="isLittleEndian">IDX file data structure.</param>
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

        /// <summary>
        /// Reads the properties of an IDX file.
        /// </summary>
        /// <param name="filePath">Path to target file.</param>
        /// <param name="isLittleEndian">IDX file data structure.</param>
        public static IDXProperties ReadProperties(string filePath, bool isLittleEndian)
        {
            using (BinaryReader reader = new BinaryReader(new FileStream(filePath, FileMode.Open)))
            {
                return ReadProperties(reader, isLittleEndian);
            }
        }

        /// <summary>
        /// Reads a dataset from an IDX file, when each element of the dataset is a collection (when the dataset is not 1D).
        /// </summary>
        /// <typeparam name="TElement">Collection's element type (i.e: double[,] if the dataset has 3D data, with double data type). | Must be an array.</typeparam>
        /// <param name="filePath">Path to target file.</param>
        /// <param name="isLittleEndian">IDX file data structure.</param>
        public static IEnumerable<TElement> ReadND<TElement>(string filePath, bool isLittleEndian) where TElement : ICollection
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

        /// <summary>
        /// Reads a dataset from an IDX file, when the dataset is 1D.
        /// </summary>
        /// <typeparam name="TElement">Collection's element type (e.g double)</typeparam>
        /// <param name="filePath">Path to target file.</param>
        /// <param name="isLittleEndian">IDX file data structure.</param>
        public static IEnumerable<TElement> Read1D<TElement>(string filePath, bool isLittleEndian) where TElement : struct
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
