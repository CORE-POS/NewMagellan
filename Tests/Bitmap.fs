namespace Tests

open BitmapBPP
open System.Drawing
open System.IO
open System.Collections.Generic
open NUnit.Framework
open FsUnit

module Bitmap  =

    [<Test>]
    let ``Converts Bitmap`` () =
        let file = Path.GetTempFileName()
        let bmp = new Bitmap(100, 100, Imaging.PixelFormat.Format24bppRgb) 
        bmp.Save(file, Imaging.ImageFormat.Bmp)
        let newBytes = BitmapConverter.To1bpp(file)
        File.Delete(file)
        newBytes |> should not' (be null)

    let generatePoint xmax ymax =
        let r = new System.Random()
        let x= r.Next(0, xmax)
        let y = r.Next(0, ymax)
        let p = new Point(x, y)
        p

    [<Test>]
    let ``Draw Signature`` () =
        let file = Path.GetTempFileName()
        let points = new List<Point>()
        for i in 1 .. 10 do
            points.Add(generatePoint 500 100) 
        points.Add(new Point(0, 0))
        for i in 1 .. 10 do
            points.Add(generatePoint 500 100) 
        points.Add(new Point(0, 0))
        let signature = new Signature(file, points)
        let bytes = File.ReadAllBytes(file)
        File.Delete(file)
        bytes.Length |> should be (greaterThan 0)
