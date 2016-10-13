﻿namespace Tests

open UDPMsgBox
open MsgInterface
open NUnit.Framework
open FsUnit
open System.Net.Sockets

module UDP =

    [<Test>]
    let ``Send a Message`` () =
        let u = new UDPMsgBox.UDPMsgBox(9450)
        let f = FakePort.FakeDelegate()
        u.SetParent(f)
        u.My_Thread.Start()
        let client = new UdpClient()
        let msg = System.Text.Encoding.ASCII.GetBytes("test msg")
        client.Send(msg, msg.Length, "localhost", 9450) |> ignore
        u.Stop()

        f.lastMsg |> should equal "test msg"
