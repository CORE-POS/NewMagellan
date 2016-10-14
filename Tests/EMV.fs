namespace Tests

open AxLayer
open SPH
open NUnit.Framework
open FsUnit
open System.Net.Sockets;

module EMV =

    [<Test>]
    let ``Send Transactions`` () =
        let emv = new SPH_Datacap_EMVX("VX805XPI:COM1")
        let ax = new FakeAx()
        emv.SPH_Thread.Start()
        while emv.IsListening()=false do
            System.Threading.Thread.Sleep(50)
        emv.SetControls(ax, ax)
        let client = new TcpClient("localhost", 8999)
        let mutable content = ""
        using (client.GetStream()) ( fun stream ->
            let msg = "POST HTTP/1.0\r\nContent-Length: 4\r\n\r\nasdf"
            let bytes = System.Text.Encoding.ASCII.GetBytes(msg)
            stream.Write(bytes, 0, bytes.Length)
            let mutable buffer:byte array = Array.create 1024 ((byte)0)
            stream.Read(buffer, 0, 1024) |> ignore
            content <- System.Text.Encoding.ASCII.GetString(buffer)
            ()
        )
        client.Close()
        emv.Stop()
        content |> should haveSubstring "asdf"
