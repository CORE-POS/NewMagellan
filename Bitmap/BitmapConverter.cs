//-------------------------------------------------------------
// <copyright file="BitmapConverter.cs" company="Whole Foods Co-op">
//  Released under GPL2 license
// </copyright>
//-------------------------------------------------------------

namespace BitmapBPP
{
    using System;

    /// <summary>
    /// Rewrite bitmaps with 1-bit color depth
    /// </summary>
    public class BitmapConverter
    {
       /// <summary>
       /// Theoretically works with 8, 16, 24, or 32 bit input files;
       /// I've only tested with 32. Returns null on 4<c>bpp</c>, file itself
       /// if image is already 1<c>bpp</c>.
       /// Uncompressed DIB3 only. Returns null otherwise.
       /// May not deal well with an existing color palette.
       /// No support for <c>endianness</c> or negative height.
       /// </summary>
       /// <param name="filename">Name of bitmap file</param>
       /// <returns>Byte array of the same bitmap at 1-bit depth</returns>
        public static byte[] To1bpp(string filename)
        {
            byte[] data = System.IO.File.ReadAllBytes(filename);

            int raw_loc = BitmapConverter.GetInt(data, 0xa, 0xd);
            int width = BitmapConverter.GetInt(data, 0x12, 0x15);
            int height = BitmapConverter.GetInt(data, 0x16, 0x19);    
            int bpp = BitmapConverter.GetInt(data, 0x1c, 0x1d);
            int comp = BitmapConverter.GetInt(data, 0x1e, 0x21);

            // 1 bpp => done
            // 4 bpp => unimplemented
            if (bpp == 1)
            {
                return data;
            }
            else if (bpp == 4)
            {
                System.Console.WriteLine("bpp4 - abort");
                return null;
            }

            // compression => unimplemented
            if (comp != 0)
            {
                System.Console.WriteLine("compressed - abort");
                return null;
            }

            byte[,] pixels = new byte[width, height];

            int raw_size = data.Length - raw_loc;
            int bytesPerRow = raw_size / height;

            /* read the existing pixel values into a
             * 2-dimensional array. */
            int x = 0;
            int y = 0;
            int pix_size = bpp / 8;
            int pixcount = 0;
            for (int i = 0; i < raw_size; i += 0)
            {
                if (i > 0 && i % bytesPerRow == 0)
                {
                    x = 0;
                    y++;
                }

                if (x < width)
                {
                    int pixel = BitmapConverter.GetInt(
                        data, i + raw_loc, i + raw_loc + pix_size - 1);
                    i += pix_size;
                    pixel = pixel & 0xffffff; // no alpha
                    if (pixel < 0xeeeeee)
                    {
                        pixels[x, y] = 1;
                    }
                    else
                    {
                        pixels[x, y] = 0;
                    }

                    pixcount++;
                }
                else
                {
                    i++; // row padding
                }

                x++;
            }

            /* create a new byte array to hold 1 bit pixels
             * and add them all to it*/
            int newRowBytes = (int)((width + 31) / 32.0) * 4;
            byte[] newdata = new byte[newRowBytes * height];
            int bits = 0;
            for (int i = 0; i < newdata.Length; i++)
            {
                int x1 = (i % newRowBytes) * 8;
                int y1 = y - (i / newRowBytes);
                int val = 0;
                for (int j = 0; j < 8; j++)
                {
                    // end of pixel row, rest gets padded
                    if (x1 + j >= width) 
                    {
                        break;
                    }

                    val = val | (pixels[x1 + j, y1] << (7 - j));
                    bits++;
                }

                newdata[i] = (byte)val;
            }

            byte[] newbmp = new byte[14 + 40 + 8 + newdata.Length];

            // BMP Header
            newbmp[0x0] = 0x42;
            newbmp[0x1] = 0x4D;
            BitmapConverter.PutInt(ref newbmp, newbmp.Length, 0x2, 0x5); // filesize
            BitmapConverter.PutInt(ref newbmp, 0, 0x6, 0x9); // irrelevant
            BitmapConverter.PutInt(ref newbmp, 14 + 40 + 8, 0xa, 0xd); // image data offset

            // DIB Header
            BitmapConverter.PutInt(ref newbmp, 40, 0xe, 0x11); // DIB3
            BitmapConverter.PutInt(ref newbmp, width, 0x12, 0x15);
            BitmapConverter.PutInt(ref newbmp, height, 0x16, 0x19);
            BitmapConverter.PutInt(ref newbmp, 1, 0x1a, 0x1b); // planes, always 1
            BitmapConverter.PutInt(ref newbmp, 1, 0x1c, 0x1d); // bpp
            BitmapConverter.PutInt(ref newbmp, 0, 0x1e, 0x21); // no compression
            BitmapConverter.PutInt(ref newbmp, newdata.Length, 0x22, 0x25); // image data size
            int dpi = (int)((72 * 39.97) + 0.5);
            BitmapConverter.PutInt(ref newbmp, dpi, 0x26, 0x29); // horizontal dpi
            BitmapConverter.PutInt(ref newbmp, dpi, 0x2a, 0x2d); // vertical dpi
            BitmapConverter.PutInt(ref newbmp, 2, 0x2e, 0x31); // number of colors
            BitmapConverter.PutInt(ref newbmp, 0, 0x32, 0x35); // usually ignored

            // Color Palette
            newbmp[0x36] = 0xff; // white
            newbmp[0x37] = 0xff;
            newbmp[0x38] = 0xff;
            newbmp[0x39] = 0x0;
            newbmp[0x3a] = 0x0; // black
            newbmp[0x3b] = 0x0;
            newbmp[0x3c] = 0x0;
            newbmp[0x3d] = 0x0;

            // raw image data
            Array.Copy(newdata, 0, newbmp, 0x3e, newdata.Length);

            return newbmp;
        }

        /// <summary>
        /// CLI option to convert a file to 1<c>bpp</c>
        /// </summary>
        /// <param name="args">CLI args</param>
        public static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                System.Console.WriteLine("File name required");
            }
            else if (!System.IO.File.Exists(args[0]))
            {
                System.Console.WriteLine("File does not exist: " + args[0]);
            }
            else
            {
                byte[] newbmp = BitmapConverter.To1bpp(args[0]);
                System.Console.WriteLine(newbmp);
                System.IO.File.WriteAllBytes("out.bmp", newbmp);
            }
        }

        /// <summary>
        /// Read integer from several bytes
        /// </summary>
        /// <param name="data">the bytes</param>
        /// <param name="start">start index</param>
        /// <param name="end">end index</param>
        /// <returns>integer value</returns>
        private static int GetInt(byte[] data, int start, int end)
        {
            int val = 0;
            for (int i = start; i <= end; i++)
            {
                val = val | (data[i] << (8 * (i - start)));
            }

            return val;
        }

        /// <summary>
        /// Store integer into byte array
        /// </summary>
        /// <param name="ret">byte array to modify</param>
        /// <param name="val">integer value</param>
        /// <param name="start">start index</param>
        /// <param name="end">end index</param>
        private static void PutInt(ref byte[] ret, int val, int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                ret[i] = (byte)(val & 0xff);
                val = val >> 8;
            }
        }
    }
}
