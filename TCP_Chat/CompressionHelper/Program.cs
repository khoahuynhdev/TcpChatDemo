using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CompressionHelper
{
    class Program
    {
        static void Main(string[] args)
        {
            string fileToCompress = "file.png";
            byte[] uncompressedByte = File.ReadAllBytes(fileToCompress);
            //Console.WriteLine(uncompressedByte.Length); //1132929

            // Benchmarking
            Stopwatch timer = new Stopwatch();

            // Display some information
            long uncompressedFileSize = uncompressedByte.LongLength;
            Console.WriteLine($"{fileToCompress} uncompressed is {ComputeSizeInMB(uncompressedFileSize)} MB large.");

            // compress it using deflate (optinal)
            using (MemoryStream compressedStream = new MemoryStream())
            {
                // init
                DeflateStream deflateStream = new DeflateStream(compressedStream, CompressionLevel.Optimal, true);

                // Run the compression
                timer.Start();
                deflateStream.Write(uncompressedByte, 0, uncompressedByte.Length); // block                
                deflateStream.Close();
                timer.Stop();

                // print some info
                long compressedFileSize = compressedStream.Length;
                Console.WriteLine("Compressed using DeflateStream (optimal): {0:0.0000} MB [{1:0.00}%] in {2}ms",
                    ComputeSizeInMB(compressedFileSize),
                    100f * (float)compressedFileSize / (float)uncompressedFileSize,
                    timer.ElapsedMilliseconds);

                //clean up resource
                timer.Reset();
            }

            // compress it using deflate (fast)
            using (MemoryStream compressedStream = new MemoryStream())
            {
                // init
                DeflateStream deflateStream = new DeflateStream(compressedStream, CompressionLevel.Fastest, true);

                // Run the compression
                timer.Start();
                deflateStream.Write(uncompressedByte, 0, uncompressedByte.Length); // block                
                deflateStream.Close();
                timer.Stop();

                // print some info
                long compressedFileSize = compressedStream.Length;
                Console.WriteLine("Compressed using DeflateStream (fast): {0:0.0000} MB [{1:0.00}%] in {2}ms",
                    ComputeSizeInMB(compressedFileSize),
                    100f * (float)compressedFileSize / (float)uncompressedFileSize,
                    timer.ElapsedMilliseconds);

                //clean up resource
                timer.Reset();
            }

            // Compress it using GZIP (save it)
            string savedArchive = fileToCompress + ".gz";
            using (var compressedStream = new MemoryStream())
            {
                // init
                GZipStream gZipStream = new GZipStream(compressedStream, CompressionMode.Compress, true);

                // run the compression
                timer.Start();
                gZipStream.Write(uncompressedByte, 0, uncompressedByte.Length); //block
                gZipStream.Close();
                timer.Stop();
                // Print some info
                long compressedFileSize = compressedStream.Length;
                Console.WriteLine("Compressed using GZipStream: {0:0.0000} MB [{1:0.00}%] in {2}ms",
                    ComputeSizeInMB(compressedFileSize),
                    100f * (float)compressedFileSize / (float)uncompressedFileSize,
                    timer.ElapsedMilliseconds);

                // save it
                using (var fileStream = new FileStream(savedArchive, FileMode.OpenOrCreate))
                {
                    compressedStream.Position = 0;
                    compressedStream.CopyTo(fileStream);
                }

                // clean up
                timer.Reset();
            }
        }
        public static double ComputeSizeInMB(long size)
        {
            return (double)size / 1024 / 1024;
        }

    }
}
