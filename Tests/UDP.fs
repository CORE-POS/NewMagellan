namespace Tests

open UDPMsgBox
open MsgInterface
open NUnit.Framework
open FsUnit
open System.Net.Sockets

module UDP =

    type Dummy() =
        let mutable lastMsg = ""
        interface DelegateForm with
            member this.MsgSend(msg:string) =
                ()
            member this.MsgRecv(msg:string) =
                lastMsg <- msg
                ()
        member this.value() =
            lastMsg

    [<Test>]
    let ``Send a Message`` () =
        let u = new UDPMsgBox.UDPMsgBox(9450)
        let d = new Dummy()
        u.SetParent(d)
        u.My_Thread.Start()
        let client = new UdpClient()
        let msg = System.Text.Encoding.ASCII.GetBytes("test msg")
        client.Send(msg, msg.Length, "localhost", 9450) |> ignore
        u.Stop()

        d.value() |> should equal "test msg"
