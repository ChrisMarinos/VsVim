﻿#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor

type internal SearchService 
    (
        _search : ITextSearchService,
        _settings : IVimGlobalSettings ) = 

    let mutable _lastSearch = { Text = Pattern(StringUtil.empty); Kind = SearchKind.ForwardWithWrap; Options = SearchOptions.None }
    let _lastSearchChanged = Event<SearchData>()

    member private x.CreateFindOptions (text:SearchText) kind searchOptions =
        let caseOptions = 
            if Utils.IsFlagSet searchOptions SearchOptions.AllowIgnoreCase && _settings.IgnoreCase then
                let hasUpper () = text.RawText |> Seq.filter CharUtil.IsLetter |> Seq.filter CharUtil.IsUpper |> SeqUtil.isNotEmpty
                if Utils.IsFlagSet searchOptions SearchOptions.AllowSmartCase && _settings.SmartCase && hasUpper() then FindOptions.MatchCase
                else FindOptions.None
            else FindOptions.MatchCase
        let revOptions = if SearchKindUtil.IsBackward kind then FindOptions.SearchReverse else FindOptions.None

        let searchKindOptions = 
            match text with
            | Pattern(_) -> FindOptions.UseRegularExpressions
            | WholeWord(_) -> FindOptions.WholeWord
            | StraightText(_) -> FindOptions.None

        caseOptions ||| revOptions ||| searchKindOptions

    member private x.FindNextMultiple (searchData:SearchData) point nav count =

        let tss = SnapshotPointUtil.GetSnapshot point
        let isWrap = SearchKindUtil.IsWrap searchData.Kind
        let opts = x.CreateFindOptions searchData.Text searchData.Kind searchData.Options
        let findData = FindData(searchData.Text.RawText, tss, opts, nav) 
        
        // Create a function which will give us the next search position
        let getNextPoint = 
            if SearchKindUtil.IsForward searchData.Kind then
                (fun (span:SnapshotSpan) -> span.End |> Some)
            else 
                let isWrap = SearchKindUtil.IsWrap searchData.Kind
                (fun (span:SnapshotSpan) -> 
                    if span.Start.Position = 0 && isWrap then SnapshotUtil.GetEndPoint tss |> Some
                    elif span.Start.Position = 0 then None
                    else span.Start.Subtract(1) |> Some )
                    
        // Recursive loop to perform the search "count" times
        let rec doFind count position = 
            let result = _search.FindNext(position, isWrap, findData) |> NullableUtil.toOption
            match result,count > 1 with
            | Some(span),false -> Some(span)
            | Some(span),true -> 
                match getNextPoint span with
                | Some(point) -> doFind (count-1) point.Position
                | None -> None
            | _ -> None

        let count = max 1 count
        let pos = SnapshotPointUtil.GetPosition point
        doFind count pos

    member private x.FindNext searchData point nav = x.FindNextMultiple searchData point nav 1

    interface ISearchService with
        member x.LastSearch 
            with get() = _lastSearch
            and set value = 
                _lastSearch <- value
                _lastSearchChanged.Trigger value
        [<CLIEvent>]
        member x.LastSearchChanged = _lastSearchChanged.Publish
        member x.FindNext searchData point nav = x.FindNext searchData point nav
        member x.FindNextMultiple searchData point nav count = x.FindNextMultiple searchData point nav count


