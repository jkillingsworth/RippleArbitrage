﻿module Program

open Chiron
open RippleArbitrage
open RippleArbitrage.Types
open RippleArbitrage.Command.Request
open RippleArbitrage.Command.Response

//-------------------------------------------------------------------------------------------------

let getRequest currencyGets currencyPays =

    { Ledger = Some Ledger.Validated
      TakerGets = currencyGets
      TakerPays = currencyPays }

let getBookOffers requests =

    requests
    |> Array.map Json.ofBookOffersRequest
    |> Array.map Json.format
    |> Command.execute Config.serverUri
    |> Array.map Json.parse
    |> Array.map Json.toBookOffersResponse

//-------------------------------------------------------------------------------------------------

let rec computeOffersToTake offers takes amount =
    match offers with
    | []
        -> failwith "Market not deep enough."
    | head :: tail when amount = 0m
        -> takes
    | head :: tail when head.TakerPays.Value > amount
        ->
        let amountGets = amount * (head.TakerGets.Value / head.TakerPays.Value)
        let amountPays = amount
        let takerGets = { Value = amountGets; Currency = head.TakerGets.Currency }
        let takerPays = { Value = amountPays; Currency = head.TakerPays.Currency }
        { TakerGets = takerGets; TakerPays = takerPays } :: takes
    | head :: tail
        ->
        let takes = head :: takes
        let amount = amount - head.TakerPays.Value
        computeOffersToTake tail takes amount

let computeAmount = List.sumBy (fun x -> x.TakerGets.Value)

//-------------------------------------------------------------------------------------------------

let currency0 = NativeCurrency Xrp
let currency1 = IssuedCurrency { Code = "JPY"; Issuer = "r94s8px6kSw1uZ1MV98dhSRTvc6VMPoPcN" }
let currency2 = IssuedCurrency { Code = "CNY"; Issuer = "rKiCet8SdvWxPXnAgYarFUXMh1zCPz432Y" }

let requests =
    [| getRequest currency1 currency0
       getRequest currency2 currency1
       getRequest currency0 currency2 |]

let offers = getBookOffers requests

let amountStart = 50000m
let amountFinal =
    amountStart
    |> computeOffersToTake offers.[0] []
    |> computeAmount
    |> computeOffersToTake offers.[1] []
    |> computeAmount
    |> computeOffersToTake offers.[2] []
    |> computeAmount

let profit = amountFinal - amountStart
let profitRatio = profit / amountStart

printfn "Start amount: % 12.5f" amountStart
printfn "Final amount: % 12.5f" amountFinal
printfn "Profit:       %+12.5f" profit
printfn "Profit ratio: %+12.5f" profitRatio
