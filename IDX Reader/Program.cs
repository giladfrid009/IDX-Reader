using System;
using System.Linq;

namespace IDXReader
{
    class Program
    {
        static void Main()
        {
            var test1 = Reader.Read1D<byte>(@"D:\Users\Gilad\Documents\MNIST\train-labels.idx1-ubyte", false).ToArray();
            var test2 = Reader.ReadND<byte[,]>(@"D:\Users\Gilad\Documents\MNIST\train-images.idx3-ubyte", false).ToArray();
            
            Console.ReadKey();
        }
    }
}
