﻿namespace SageSerpent.Infrastructure
    open System
    open System.Collections.Generic
    open Microsoft.FSharp.Collections

    module MapWithRunLengthsDetail =
        [<CustomComparison; CustomEquality>]
        type SlotKey =
            Singleton of UInt32
          | Interval of UInt32 * UInt32 // These are the *inclusive* lower- and upper-bounds, respectively.
                                        // No need to worry about empty and singleton intervals, obviously.
            interface IComparable<SlotKey> with
                // NOTE: be careful here; the comparison semantics are deliberately sloppy - they violate the rules for a total ordering, because
                // it is possible to take one slot key A that is less than another slot key B, and then use encompassing or overlapping interval
                // slot keys to 'bridge the gap' between A and B via of chain of intervals that compare equal to each other and to A and B.
                // As long as the client code (which is actually 'MapWithRunLengths') doesn't allow multiple items that compare equal in the same
                // data structure, then this won't cause a problem.
                member this.CompareTo another =
                    match this
                            , another with
                        Singleton lhs
                        , Singleton rhs ->
                            compare lhs rhs
                        | Singleton lhs
                        , Interval (rhsLowerBound
                                    , rhsUpperBound) ->
                            if lhs < rhsLowerBound
                            then
                                -1
                            else if lhs > rhsUpperBound
                            then
                                1
                            else
                                0
                        | Interval (lhsLowerBound
                                    , lhsUpperBound)
                        , Singleton rhs ->
                            if rhs < lhsLowerBound
                            then
                                1
                            else if rhs > lhsUpperBound
                            then
                                -1
                            else
                                0  
                        | Interval (_
                                    , lhsUpperBound)
                        , Interval (rhsLowerBound
                                    , _) when lhsUpperBound < rhsLowerBound ->
                            -1
                        | Interval (lhsLowerBound
                                    , _)
                        , Interval (_
                                    , rhsUpperBound) when rhsUpperBound < lhsLowerBound ->
                            1
                        | _ ->
                            0

            interface IComparable with
                member this.CompareTo another =
                    match another with
                        :? SlotKey as another ->
                            (this :> IComparable<SlotKey>).CompareTo another
                      | _ ->
                            raise (ArgumentException (sprintf "Rhs of comparison must also be of type: %A"
                                                              typeof<SlotKey>.Name))

            override this.Equals another =
                match another with
                    :? SlotKey as another ->
                        0 = compare this
                                    another
                  | _ -> 
                        false

            override this.GetHashCode () =
                match this with
                    Singleton key ->
                        1 + hash key
                  | Interval (_) as pair ->
                        7 * (3 + hash pair)

        let (|KeyValue|) (keyValuePair: C5.KeyValuePair<'Key, 'Value>) =
            keyValuePair.Key
            , keyValuePair.Value

        let isNotGap (KeyValue (_,
                                value: Option<_>)) =
            value.IsSome

    open MapWithRunLengthsDetail

    type MapWithRunLengths<'Value when 'Value: comparison> (representation: C5.ISortedDictionary<SlotKey, Option<'Value>>) =
        do
            let shouldBeTrue =
                representation.IsEmpty
                || representation
                   |> Seq.exists isNotGap
            if not shouldBeTrue
            then
                raise (InvariantViolationException "Representation is composed solely of gaps: should be empty instead.")
            let shouldBeTrue =
                representation.IsEmpty
                || representation.FindMin ()
                |> isNotGap
            if not shouldBeTrue
            then
                raise (InvariantViolationException "Leading gap detected in representation.")
            let shouldBeTrue =
                representation.IsEmpty
                || representation.FindMax ()
                |> isNotGap
            if not shouldBeTrue
            then
                raise (InvariantViolationException "Trailing gap detected in representation.")
            for KeyValue (key
                          , value) in representation do
                if Unchecked.defaultof<SlotKey> = key
                then
                    raise (InvariantViolationException "Null key detected.")
            for slotKey in representation.Keys do
                match slotKey with
                    Interval (lowerBound
                              , upperBound) ->
                        if upperBound + 1u < lowerBound
                        then
                            raise (InvariantViolationException "Malformed interval slot key detected: bounds are ordered in reverse.")
                        else if upperBound + 1u = lowerBound
                        then
                            raise (InvariantViolationException "Empty interval slot key detected - should have been represented by the absence of the slot key altogether.")
                        else if upperBound = lowerBound
                        then
                            raise (InvariantViolationException "One-item interval slot key detected - should have been represented by a singleton slot key instead.")
                  | _ ->
                        ()
            for (KeyValue (predecessorSlotKey
                           , predecessorValue)
                 , KeyValue (successorSlotKey
                             , successorValue)) in representation
                                                   |> Seq.pairwise do
                if predecessorValue <> successorValue
                then
                    let shouldBeTrue =
                        match predecessorSlotKey
                              , successorSlotKey with
                            Singleton predecessorKey
                            , Singleton successorKey when 1u + predecessorKey = successorKey ->
                                true
                          | Interval (_
                                      , predecessorUpperBound)
                            , Singleton successorKey when 1u + predecessorUpperBound = successorKey ->
                                true
                          | Singleton predecessorKey
                            , Interval (successorLowerBound
                                        , _) when 1u + predecessorKey = successorLowerBound ->
                                true
                          | Interval (_
                                      , predecessorUpperBound)
                            , Interval (successorLowerBound
                                        , _) when 1u + predecessorUpperBound = successorLowerBound ->
                                true
                          | _ ->
                                false
                    if not shouldBeTrue
                    then
                        raise (InvariantViolationException "Adjacent slots found that refer to different associated values but are non-contiguous - should have an explicit contiguous gap between them.")
                else
                    raise (InvariantViolationException "Adjacent and contiguous slots found referring to the same associated value - should either be fused or have an intervening gap.")

        member this.Keys: ICollection<UInt32> =
            [|
                for KeyValue (slotKey
                              , value) in representation do
                    match value with
                        Some _ ->
                            match slotKey with
                                Singleton key ->
                                    yield key
                              | Interval (lowerBound
                                          , upperBound) ->
                                    for key in lowerBound .. upperBound do
                                        yield key
                      | _ ->
                            ()
            |] :> ICollection<UInt32>

        member this.Values: ICollection<'Value> =
            [|
                for KeyValue (slotKey
                              , value) in representation do
                    match value with
                        Some value ->
                            match slotKey with
                                Singleton _ ->
                                    yield value
                              | Interval (lowerBound
                                          , upperBound) ->
                                    for _ in lowerBound .. upperBound do
                                        yield value
                      | _ ->
                            ()
            |] :> ICollection<'Value>

        member this.Item
            with get (key: UInt32): 'Value =
                match representation.[Singleton key] with
                    Some value ->
                        value
                  | _ ->
                        raise (KeyNotFoundException (sprintf "Key '%A' not present in map." key))

        member this.Count: int32 =
            representation
            |> Seq.fold (fun count
                             (KeyValue (slotKey
                                       , value)) ->
                                match value with
                                    Some _ ->
                                        count +
                                        match slotKey with
                                            Singleton _ ->
                                                1
                                          | Interval (lowerBound
                                                     , upperBound) ->
                                                upperBound + 1u - lowerBound
                                                |> int32
                                  | _ ->
                                        count)
                        0

        member this.IsEmpty =
            representation.IsEmpty

        static member FuseAndDiscardConflictingAssociations inefficientRepresentation =
            let rec discardConflicts representation
                                     reversedPrefixOfResult =
                match representation with
                    [] ->
                        reversedPrefixOfResult
                  | [singleton] ->
                        singleton :: reversedPrefixOfResult
                  | (firstKey
                     , _) as first :: (((secondKey
                                         , _) :: _) as nonEmptyTail) when firstKey = secondKey ->
                        discardConflicts nonEmptyTail
                                         reversedPrefixOfResult
                  | first :: ((_ :: _) as nonEmptyList) ->
                         discardConflicts nonEmptyList
                                          (first :: reversedPrefixOfResult)
            let reversedRepresentationWithoutConflicts =
                discardConflicts inefficientRepresentation
                                 []
            let makeGapBetween oneBeforeLowerBound
                               oneAfterUpperBound =
                if 2u + oneBeforeLowerBound = oneAfterUpperBound
                then
                    Singleton (1u + oneBeforeLowerBound)
                else
                    Interval (1u + oneBeforeLowerBound
                              , oneAfterUpperBound - 1u)
                , None
            let rec fuse representation
                         reversedPrefixOfResult =
                match representation with
                    [] ->
                        reversedPrefixOfResult
                  | [singleton] ->
                        singleton :: reversedPrefixOfResult
                  | (firstKey
                     , firstValue) as first :: (((secondKey
                                                  , secondValue) :: tail) as nonEmptyTail) ->
                        match firstKey
                              , secondKey with
                            Singleton firstKey
                            , Singleton secondKey when 1u + firstKey = secondKey
                                                       && firstValue = secondValue ->
                                fuse ((Interval (firstKey
                                               , secondKey)
                                      , firstValue) :: tail)
                                     reversedPrefixOfResult
                          | Singleton firstKey
                            , Singleton secondKey when 1u + firstKey < secondKey ->
                                fuse (first :: makeGapBetween firstKey
                                                              secondKey :: nonEmptyTail)
                                     reversedPrefixOfResult
                          | Interval (firstLowerBound
                                      , firstUpperBound)
                            , Interval (secondLowerBound
                                        , secondUpperBound) when 1u + firstUpperBound = secondLowerBound
                                                                 && firstValue = secondValue ->
                                fuse ((Interval (firstLowerBound
                                                , secondUpperBound)
                                      , firstValue) :: tail)
                                     reversedPrefixOfResult
                          | Interval (firstLowerBound
                                      , firstUpperBound)
                            , Interval (secondLowerBound
                                        , secondUpperBound) when 1u + firstUpperBound < secondLowerBound ->
                                fuse (first :: makeGapBetween firstUpperBound
                                                              secondLowerBound :: nonEmptyTail)
                                     reversedPrefixOfResult
                          | Singleton firstKey
                            , Interval (secondLowerBound
                                        , secondUpperBound) when 1u + firstKey = secondLowerBound
                                                                 && firstValue = secondValue ->
                                fuse ((Interval (firstKey
                                                 , secondUpperBound)
                                       , firstValue) :: tail)
                                     reversedPrefixOfResult
                          | Singleton firstKey
                            , Interval (secondLowerBound
                                        , secondUpperBound) when 1u + firstKey < secondLowerBound ->
                                fuse (first :: makeGapBetween firstKey
                                                              secondLowerBound :: nonEmptyTail)
                                     reversedPrefixOfResult
                          | Interval (firstLowerBound
                                      , firstUpperBound)
                            , Singleton secondKey when 1u + firstUpperBound = secondKey
                                                       && firstValue = secondValue ->
                                fuse ((Interval (firstLowerBound
                                                , secondKey)
                                      , firstValue) :: tail)
                                     reversedPrefixOfResult
                          | Interval (firstLowerBound
                                      , firstUpperBound)
                            , Singleton secondKey when 1u + firstUpperBound < secondKey ->
                                fuse (first :: makeGapBetween firstUpperBound
                                                              secondKey :: nonEmptyTail)
                                     reversedPrefixOfResult
                          | _ ->
                                fuse nonEmptyTail
                                     (first :: reversedPrefixOfResult)
            let result =
                fuse (reversedRepresentationWithoutConflicts
                      |> List.rev)
                     List.empty
                |> List.rev
                |> List.map (fun (slotKey
                                  , value) ->
                                C5.KeyValuePair (slotKey, value))
            let unwind (slotKey
                        , value) =
                match slotKey with
                    Singleton key ->
                        [key
                         , value]
                  | Interval (lowerBound
                              , upperBound) ->
                        [
                            for key in lowerBound .. upperBound do
                                yield key
                                      , value
                        ]
            let shouldBeTrue =
                (inefficientRepresentation
                 |> List.map unwind
                 |> List.concat
                 |> Set.ofList).IsSupersetOf (result
                                              |> List.filter isNotGap
                                              |> List.map (|KeyValue|)
                                              |> List.map unwind
                                              |> List.concat
                                              |> Set.ofList)
            if not shouldBeTrue
            then
                raise (LogicErrorException "Postcondition violation: the fused representation should only cover key-value pairs that are also covered by the inefficient representation.")
            let shouldBeTrue =
                (inefficientRepresentation
                 |> List.map unwind
                 |> List.concat
                 |> List.length) >= (result
                                     |> List.filter isNotGap
                                     |> List.map (|KeyValue|)
                                     |> List.map unwind
                                     |> List.concat
                                     |> List.length)
            if not shouldBeTrue
            then
                raise (LogicErrorException "Postcondition violation: the fused representation should either more or equally as compact as the inefficient representation.")
            result :> seq<_>

        member this.ToList =
            [
                for KeyValue (slotKey
                              , value) in representation do
                    match value with
                        Some value ->
                            match slotKey with
                                Singleton key ->
                                    yield key
                                          , value
                              | Interval (lowerBound
                                          , upperBound) ->
                                    for key in lowerBound .. upperBound do
                                        yield key
                                              , value
                      | _ ->
                            ()
            ]

        member this.ToSeq =
            seq
                {
                    for KeyValue (slotKey
                                  , value) in representation do
                        match value with
                            Some value ->
                                match slotKey with
                                    Singleton key ->
                                        yield key
                                              , value
                                  | Interval (lowerBound
                                              , upperBound) ->
                                        for key in lowerBound .. upperBound do
                                            yield key
                                                  , value
                          | _ ->
                                ()
                }

        member this.Add (key: UInt32,
                         value: 'Value) =
            let liftedValue =
                Some value
            let locallyMutatedRepresentation =
                C5.TreeDictionary<SlotKey, Option<'Value>> ()
            let liftedKey =
                Singleton key
            if representation.IsEmpty
            then
                locallyMutatedRepresentation.Add (liftedKey, liftedValue)
            else
                let greatestLowerBound =
                    ref Unchecked.defaultof<C5.KeyValuePair<SlotKey, Option<'Value>>>
                let hasGreatestLowerBound =
                    ref false
                let leastUpperBound =
                    ref Unchecked.defaultof<C5.KeyValuePair<SlotKey, Option<'Value>>>
                let hasLeastUpperBound =
                    ref false
                let entriesWithMatchingKeysToBeAddedIn =
                    if representation.Cut (liftedKey, greatestLowerBound, hasGreatestLowerBound, leastUpperBound, hasLeastUpperBound)
                    then
                        let slotKeyMatchingLiftedKey =
                            ref liftedKey
                        let associatedValue =
                            ref Unchecked.defaultof<Option<'Value>>
                        representation.Find(slotKeyMatchingLiftedKey, associatedValue)
                        |> ignore
                        if liftedValue <> !associatedValue
                        then
                            match !slotKeyMatchingLiftedKey with
                                Singleton _ ->
                                    [liftedKey
                                     , liftedValue]
                              | Interval (lowerBound
                                          , upperBound) when 1u + lowerBound = upperBound ->
                                    if lowerBound = key
                                    then
                                        [(liftedKey
                                          , liftedValue); (Singleton upperBound
                                                     , !associatedValue)]
                                    else
                                        [(Singleton lowerBound
                                          , !associatedValue); (liftedKey
                                                                , liftedValue)]
                              | Interval (lowerBound
                                          , upperBound) ->
                                    if lowerBound = key
                                    then
                                        [(liftedKey
                                          , liftedValue);
                                         (Interval (1u + key
                                                    , upperBound)
                                          , !associatedValue)]
                                    else if upperBound = key
                                    then
                                        [(Interval (lowerBound
                                                    , key - 1u)
                                          , !associatedValue);
                                         (liftedKey
                                          , liftedValue)]
                                    else
                                        [((if lowerBound + 1u = key
                                           then
                                            Singleton lowerBound
                                           else
                                            Interval (lowerBound
                                                      , key - 1u))
                                          , !associatedValue);
                                         (liftedKey
                                          , liftedValue);
                                         ((if 1u + key = upperBound
                                           then
                                            Singleton upperBound
                                           else
                                            Interval (1u + key
                                                      , upperBound))
                                          , !associatedValue)]
                        else
                            [!slotKeyMatchingLiftedKey
                             , !associatedValue]
                    else
                        [liftedKey
                         , liftedValue]
                match !hasGreatestLowerBound
                      , !hasLeastUpperBound with
                    false
                    , false ->
                        locallyMutatedRepresentation.AddSorted (entriesWithMatchingKeysToBeAddedIn
                                                                |> MapWithRunLengths<'Value>.FuseAndDiscardConflictingAssociations)
                  | true
                    , false ->
                        locallyMutatedRepresentation.AddSorted (representation.RangeTo ((!greatestLowerBound).Key))
                        locallyMutatedRepresentation.AddSorted ([
                                                                    yield match !greatestLowerBound with
                                                                            KeyValue greatestLowerBound ->
                                                                                greatestLowerBound
                                                                    yield! entriesWithMatchingKeysToBeAddedIn
                                                                ]
                                                                |> MapWithRunLengths<'Value>.FuseAndDiscardConflictingAssociations)
                  | false
                    , true ->
                        locallyMutatedRepresentation.AddSorted ([
                                                                    yield! entriesWithMatchingKeysToBeAddedIn
                                                                    yield match !leastUpperBound with
                                                                            KeyValue leastUpperBound ->
                                                                                leastUpperBound
                                                                ]
                                                                |> MapWithRunLengths<'Value>.FuseAndDiscardConflictingAssociations)
                        locallyMutatedRepresentation.AddSorted (representation.RangeFrom ((!leastUpperBound).Key)
                                                                |> Seq.skip 1)
                  | true
                    , true ->
                        locallyMutatedRepresentation.AddSorted (representation.RangeTo ((!greatestLowerBound).Key))
                        locallyMutatedRepresentation.AddSorted ([
                                                                    yield match !greatestLowerBound with
                                                                            KeyValue greatestLowerBound ->
                                                                                greatestLowerBound
                                                                    yield! entriesWithMatchingKeysToBeAddedIn
                                                                    yield match !leastUpperBound with
                                                                            KeyValue leastUpperBound ->
                                                                                leastUpperBound
                                                                ]
                                                                |> MapWithRunLengths<'Value>.FuseAndDiscardConflictingAssociations)
                        locallyMutatedRepresentation.AddSorted (representation.RangeFrom ((!leastUpperBound).Key)
                                                                |> Seq.skip 1)
            let result = 
                MapWithRunLengths locallyMutatedRepresentation
            let differences =
                (result.ToList |> Set.ofList)
                - (this.ToList |> Set.ofList)
            let noOperationCase =
                (this :> IDictionary<_, _>).Contains (KeyValuePair (key, value))
            if noOperationCase
            then
                let shouldBeTrue =
                    differences.IsEmpty
                if not shouldBeTrue
                then
                    raise (LogicErrorException "Postcondition failure: adding in an existing key-value pair (as distinct from overwriting an associated value under an existing key) should result in an identical map.")
            else
                let shouldBeTrue =
                    differences
                    |> Set.count
                     = 1
                if not shouldBeTrue
                then
                    raise (LogicErrorException "Postcondition failure: the result should contain exactly one key-value pair that doesn't exist in the original (NOTE: the converse *may* also be true in the overwritten value case).")
                let shouldBeTrue =
                    differences.Contains (key, value)
                if not shouldBeTrue
                then
                    raise (LogicErrorException "Postcondition failure: the key-value pair contained in the result but not in the original should be the one added in.")
            result

        interface IComparable with
            member this.CompareTo another =
                match another with
                    :? MapWithRunLengths<'Value> as another -> 
                        compare this.ToList
                                another.ToList
                  | _ ->
                        raise (ArgumentException (sprintf "Rhs of comparison must also be of type: %A"
                                                          typeof<MapWithRunLengths<'Value>>.Name))

        interface IDictionary<UInt32, 'Value> with
            member this.Item
                with get (key: UInt32): 'Value =
                    this.[key]
                and set (key: UInt32)
                        (value: 'Value): unit =
                    failwith "The collection is immutable."

            member this.Keys =
                this.Keys

            member this.Values =
                this.Values

            member this.ContainsKey key =
                representation.Contains (Singleton key)

            member this.Remove (key: UInt32): bool =
                failwith "The collection is immutable."

            member this.TryGetValue (key,
                                     value) =
                let mutableValue
                    = ref Unchecked.defaultof<Option<'Value>>
                if representation.Find (ref (Singleton key), mutableValue)
                then
                    match !mutableValue with
                        Some retrievedValue ->
                            value <- retrievedValue
                            true
                      | _ ->
                            false
                else
                    false

            member this.Count =
                this.Count

            member this.IsReadOnly =
                true

            member this.Add (key,
                             value) =
                failwith "The collection is immutable."

            member this.Add keyValuePair =
                failwith "The collection is immutable."

            member this.Clear () =
                failwith "The collection is immutable."

            member this.Contains keyValuePair =
                let mutableValue =
                    ref Unchecked.defaultof<Option<'Value>>
                representation.Find (ref (Singleton keyValuePair.Key), mutableValue)
                && !mutableValue = Some keyValuePair.Value

            member this.CopyTo (keyValuePairs,
                                offsetIndexIntoKeyValuePairs) =
                failwith "Not implemented yet."

            member this.GetEnumerator (): IEnumerator<KeyValuePair<UInt32, 'Value>> =
                (this.ToSeq
                 |> Seq.map (fun (key
                                  , value) -> KeyValuePair (key
                                                            , value))).GetEnumerator ()

            member this.Remove (keyValuePair: KeyValuePair<UInt32, 'Value>): bool =
                failwith "The collection is immutable."

            member this.GetEnumerator (): System.Collections.IEnumerator =
                (this :> seq<KeyValuePair<UInt32, 'Value>>).GetEnumerator () :> System.Collections.IEnumerator

    module MapWithRunLengths =
        let inline isEmpty (mapWithRunLengths: MapWithRunLengths<_>): bool =
            mapWithRunLengths.IsEmpty

        let ofList (list: List<UInt32 * 'Value>): MapWithRunLengths<'Value> =
            let sortedList =
                list
                |> List.sortBy fst
            let locallyMutatedRepresentation =
                C5.TreeDictionary<SlotKey, Option<'Value>> ()
            let fusedList =
                MapWithRunLengths<'Value>.FuseAndDiscardConflictingAssociations (sortedList
                                                                                 |> List.map (fun (key
                                                                                                   , value) ->
                                                                                                Singleton key
                                                                                                , Some value))
            locallyMutatedRepresentation.AddSorted fusedList
            MapWithRunLengths locallyMutatedRepresentation

        let inline toList (mapWithRunLengths: MapWithRunLengths<'Value>): List<UInt32 * 'Value> =
            mapWithRunLengths.ToList

        let ofSeq (seq: seq<UInt32 * 'Value>): MapWithRunLengths<'Value> =
            seq
            |> List.ofSeq
            |> ofList

        let inline toSeq (mapWithRunLengths: MapWithRunLengths<'Value>): seq<UInt32 * 'Value> =
            mapWithRunLengths.ToSeq

        [<GeneralizableValue>]
        let empty<'Value when 'Value: comparison> =
            MapWithRunLengths<'Value> (C5.TreeDictionary ())

        let fold (foldOperation: 'State -> UInt32 -> 'Value -> 'State)
                 (state: 'State)
                 (mapWithRunLengths: MapWithRunLengths<'Value>) =
            mapWithRunLengths.ToSeq
            |> Seq.fold (fun state
                             (key
                              , value) ->
                                foldOperation state
                                              key
                                              value)
                        state

        let foldBack (foldOperation: UInt32 -> 'Value -> 'State -> 'State)
                     (mapWithRunLengths: MapWithRunLengths<'Value>)
                     (state: 'State): 'State =
            mapWithRunLengths.ToList
            |> BargainBasement.Flip (List.foldBack (fun (key
                                                         , value)
                                                        state ->
                                                            foldOperation key
                                                                          value
                                                                          state))
                                    state
            

        let add (key: UInt32)
                (value: 'Value)
                (mapWithRunLengths: MapWithRunLengths<'Value>) =
            mapWithRunLengths.Add (key,
                                   value)

        let map (transformation: UInt32 -> 'Value -> 'TransformedValue)
                (mapWithRunLengths: MapWithRunLengths<'Value>): MapWithRunLengths<'TransformedValue> =
                mapWithRunLengths
                |> toList
                |> List.map (fun (key
                                  , value) ->
                                key
                                , transformation key
                                                 value)
                |> ofList

        let tryFind (key: UInt32)
                    (mapWithRunLengths: MapWithRunLengths<'Value>): Option<'Value> =
            let mutableValue =
                ref Unchecked.defaultof<'Value>
            match (mapWithRunLengths :> IDictionary<_, _>).TryGetValue (key, mutableValue) with
                true ->
                    Some !mutableValue
              | false ->
                    None

        let iter (operation: UInt32 -> 'Value -> unit)
                 (mapWithRunLengths: MapWithRunLengths<'Value>): unit =
            failwith "Not implemented yet."
                    
