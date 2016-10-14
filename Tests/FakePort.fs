namespace Tests

open SPH
open MsgInterface

module FakePort =

    type FakePort() =
        member val lastMsg = "" with get,set
        member val buffer:int list = [] with get,set
        member val isOpen = false with get,set
        interface IPortWrapper with
            member this.Open() =
                this.isOpen <- true
                ()
            member this.Close() =
                this.isOpen <- false
                ()
            member this.ReadByte() =
                match this.buffer with
                | x::xs -> 
                    this.buffer <- xs
                    x
                | [] ->
                    raise (new System.ServiceProcess.TimeoutException())

            member this.Write(msg) =
                this.lastMsg <- msg
                ()
            member this.Write(msg, offset, count) =
                this.lastMsg <- System.Text.Encoding.ASCII.GetString(msg)
                ()

    type FakeDelegate() =
        member val lastMsg = "" with get,set
        member val lastSend = "" with get,set
        interface IDelegateForm with
            member this.MsgSend(msg:string) =
                this.lastSend <- msg
                ()
            member this.MsgRecv(msg:string) =
                this.lastMsg <- msg
                ()

    let charsToInts (chars:char list) =
        chars |> List.map (fun i -> int i)