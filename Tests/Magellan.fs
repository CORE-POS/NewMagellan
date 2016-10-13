namespace Tests

open SPH
open NUnit.Framework
open FsUnit

module Magellan =

    let wsp = new FakePort.FakePort()
    let del = new FakePort.FakeDelegate()
    let getInstance () =
        let sph = new SPH_Magellan_Scale("COM1")
        sph.SetWrapper(wsp)
        sph.SetParent(del)
        sph.SetVerbose(1)
        sph

    [<Test>]
    let ``Beep Scale`` () =
        let sph = getInstance()
        sph.HandleMsg("goodBeep")
        wsp.lastMsg |> should equal "S334\r"

    [<Test>]
    let ``Wakeup Scale`` () =
        let sph = getInstance()
        sph.HandleMsg("wakeup")
        wsp.lastMsg |> should equal "S14\r"

    [<Test>]
    let ``Read Barcode`` () =
        let sph = getInstance()
        let data = FakePort.charsToInts ['S';'0';'8';'E';'1';'2';'3';'4';'5';'6';'\r']
        wsp.buffer <- data
        sph.SPH_Thread.Start()
        while wsp.buffer.Length > 0 do
            System.Threading.Thread.Sleep(50)
        sph.Stop()
        del.lastSend |> should equal "1234500006"

    [<Test>]
    let ``Read Weight`` () =
        let sph = getInstance()
        let data = FakePort.charsToInts ['S';'1';'4';'4';'W';'X';'Y';'Z';'\r']
        wsp.buffer <- data
        sph.SPH_Thread.Start()
        while wsp.buffer.Length > 0 do
            System.Threading.Thread.Sleep(50)
        sph.Stop()
        del.lastSend |> should equal "S11WXYZ"
