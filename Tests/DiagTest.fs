module Marksman.DiagTest

open System.IO
open Marksman.Index
open Marksman.Misc
open Xunit

open Marksman.Workspace
open Marksman.Diag
open Marksman.Helpers

let entryToHuman (entry: Entry) =
    let lsp = diagToLsp entry
    lsp.Message

let diagToHuman (diag: seq<PathUri * list<Entry>>) : list<string * string> =
    seq {
        for path, entries in diag do
            for e in entries do
                yield Path.GetFileName path.LocalPath, entryToHuman e
    }
    |> List.ofSeq

[<Fact>]
let documentIndex_1 () =
    let doc = FakeDoc.Mk "# T1\n# T2"

    let titles =
        Doc.index >> Index.titles <| doc
        |> Array.map (fun x -> x.data.title.text)

    Assert.Equal<string>([ "T1"; "T2" ], titles)

[<Fact>]
let nonBreakingWhitespace () =
    let nbsp = "\u00a0"
    let doc = FakeDoc.Mk $"# T1\n##{nbsp}T2"

    match (checkNonBreakingWhitespace doc) with
    | [ NonBreakableWhitespace range ] ->
        Assert.Equal(1, range.Start.Line)
        Assert.Equal(1, range.End.Line)

        Assert.Equal(2, range.Start.Character)
        Assert.Equal(3, range.End.Character)
    | _ -> failwith "Expected NonBreakingWhitespace diagnostic"

[<Fact>]
let noDiagOnShortcutLinks () =
    let doc = FakeDoc.Mk([| "# H1"; "## H2"; "[shortcut]"; "[[#h42]]" |])
    let folder = FakeFolder.Mk([ doc ])
    let diag = checkFolder folder |> diagToHuman

    Assert.Equal<string * string>([ "fake.md", "Link to non-existent heading 'h42'" ], diag)

[<Fact>]
let noDiagOnRealUrls () =
    let doc =
        FakeDoc.Mk([| "# H1"; "## H2"; "[](www.bad.md)"; "[](https://www.good.md)" |])

    let folder = FakeFolder.Mk([ doc ])
    let diag = checkFolder folder |> diagToHuman

    Assert.Equal<string * string>(
        [ "fake.md", "Link to non-existent document at 'www.bad.md'" ],
        diag
    )

[<Fact>]
let noDiagOnNonMarkdownFiles () =
    let doc =
        FakeDoc.Mk(
            [| "# H1"
               "## H2"
               "[](bad.md)"
               "[](another%20bad.md)"
               "[](good/folder)" |]
        )

    let folder = FakeFolder.Mk([ doc ])
    let diag = checkFolder folder |> diagToHuman

    Assert.Equal<string * string>(
        [ "fake.md", "Link to non-existent document at 'bad.md'"
          "fake.md", "Link to non-existent document at 'another%20bad.md'" ],
        diag
    )
