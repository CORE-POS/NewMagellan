//-------------------------------------------------------------
// <copyright file="Signature.cs" company="Whole Foods Co-op">
//  Released under GPL2 license
// </copyright>
//-------------------------------------------------------------
/*******************************************************************************

    Copyright 2010 Whole Foods Co-op

    This file is part of IT CORE.

    IT CORE is free software; you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 2 of the License, or
    (at your option) any later version.

    IT CORE is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    in the file license.txt along with IT CORE; if not, write to the Free Software
    Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA

*********************************************************************************/

namespace BitmapBPP 
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;

    /// <summary>
    /// Create a bitmap signature from list of points
    /// </summary>
    public class Signature
    {
        /// <summary>
        /// Default image width
        /// </summary>
        private const int IMAGE_WIDTH = 512;

        /// <summary>
        /// Default image height
        /// </summary>
        private const int IMAGE_HEIGHT = 128;

        /// <summary>
        /// File where image is saved
        /// </summary>
        private readonly string filename;

        /// <summary>
        /// List of points to draw
        /// </summary>
        private readonly List<Point> points;

        /// <summary>
        /// Bitmap resource
        /// </summary>
        private Bitmap bmp;

        /// <summary>
        /// Initializes a new instance of the <see cref="Signature"/> class.
        /// </summary>
        /// <param name="fn">file name</param>
        /// <param name="pts">list of points</param>
        public Signature(string fn, List<Point> pts)
        {
           this.filename = fn;
           this.points = pts; 

           this.bmp = new Bitmap(IMAGE_WIDTH, IMAGE_HEIGHT);
           this.Draw();
        }

        /// <summary>
        /// Draw image from constructor
        /// </summary>
        public void Draw()
        {
            Graphics g = Graphics.FromImage(this.bmp);

            Brush whiteBrush = new SolidBrush(Color.White);
            this.bmp.SetResolution(IMAGE_HEIGHT / 2, IMAGE_WIDTH / 2);
            g.TranslateTransform(0f, IMAGE_HEIGHT);
            g.ScaleTransform(1f, -1f);
            g.FillRegion(
                whiteBrush,
                new Region(new Rectangle(0, 0, IMAGE_WIDTH, IMAGE_HEIGHT)));

            Brush blackBrush = new SolidBrush(Color.Black);
            Pen p = this.GetPen(blackBrush);
            List<Point> line = new List<Point>();
            foreach (Point point in this.points)
            {
                if (point.IsEmpty)
                {
                    try
                    {
                        g.DrawLines(p, line.ToArray());
                        line.Clear();
                    }
                    catch (Exception)
                    {
                    }
                }
                else
                {
                    line.Add(point);
                }
            }

            this.bmp.Save(this.filename, System.Drawing.Imaging.ImageFormat.Bmp);
            var bpp = BitmapConverter.To1bpp(this.filename);
            File.WriteAllBytes(this.filename, bpp);
        }

        /// <summary>
        /// Create pen for given brush
        /// </summary>
        /// <param name="b">the brush</param>
        /// <returns>the pen</returns>
        private Pen GetPen(Brush b)
        {
            Pen p = new Pen(b);
            p.Width = 10;
            p.StartCap = System.Drawing.Drawing2D.LineCap.Round;
            p.EndCap = System.Drawing.Drawing2D.LineCap.Round;
            p.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;

            return p;
        }
    }
}