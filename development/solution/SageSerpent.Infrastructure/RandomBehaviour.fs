﻿#light

namespace SageSerpent.Infrastructure

    open System
    open Wintellect.PowerCollections

    type RandomBehaviour (seed) =
        let randomBehaviour = Random seed
        member this.UnderlyingImplementationForClientUse =
            randomBehaviour
        member this.ChooseAnyNumberFromZeroToOneLessThan =
            int32 >> randomBehaviour.Next >> uint32
        member this.ChooseAnyNumberFromOneTo =
            this.ChooseAnyNumberFromZeroToOneLessThan >> (+) 1u
        member this.HeadsItIs () =
            this.ChooseAnyNumberFromZeroToOneLessThan 2u = 0u
        member this.ChooseOneOf candidates =
            (this.ChooseSeveralOf candidates 1u: array<_>).[0]
                                                 // ^^^ WORKAROUND for suspected compiler bug.
        member this.ChooseSeveralOf candidates (numberToChoose: UInt32) =
            if numberToChoose > uint32 (Seq.length candidates)
            then raise (PreconditionViolationException "Insufficient number of candidates to satisfy number to choose.")
            else Algorithms.RandomSubset (candidates,
                                          int32 numberToChoose,
                                          randomBehaviour)
        member this.Shuffle items =
            Algorithms.RandomShuffle (items,
                                      randomBehaviour)
    
    