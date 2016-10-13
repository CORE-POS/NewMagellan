
namespace Tests

open System
open Discover
open NUnit.Framework
open FsUnit

module Discover =

    let d = new Discover()

    [<Test>]
    let ``Find By String`` () =
        let t = d.GetType("Discover.Discover")
        t |> should be instanceOfType<System.Type>

    [<Test>]
    let ``Find Subclasses`` () =
        let subs = d.GetSubClasses("Discover.Discover")
        subs.Count |> should equal 0 

    [<Test>]
    let ``Both Lookups`` () =
        let a = d.GetSubClasses("Discover.Discover")
        let b = d.GetSubClasses(typeof<Discover>)
        a |> should equal b