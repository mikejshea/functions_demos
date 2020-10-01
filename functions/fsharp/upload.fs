namespace HillbornFitness.GpxUpload

open System
open Microsoft.AspNetCore.Mvc
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.Http
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open FSharp.Data

module HttpTrigger =
    [<AllowNullLiteral>]
    type NameContainer() =
        member val Name = "" with get, set
    type GPX = XmlProvider<"sample.xml">
    type LocEle =
      struct
          val Location: (float * float)
          val Ele: float

          new (lat, lon, ele) = {
            Location = (lat, lon)
            Ele = ele 
          }
      end

    type DistEle =
      struct
        val Dist: float
        val Ele: float
        new (dist: float, ele:float) = { Dist = dist; Ele = ele }
      end

    let rev xs = Seq.fold (fun acc x -> x::acc) [] xs
    let calcdist (p1Latitude,p1Longitude) (p2Latitude,p2Longitude) =
        let r = 6372800.0; // km
        let dLat = (p2Latitude - p1Latitude) * Math.PI / 180.0
        let dLon = (p2Longitude - p1Longitude) * Math.PI / 180.0
        let lat1 = p1Latitude * Math.PI / 180.0
        let lat2 = p2Latitude * Math.PI / 180.0

        let a = Math.Sin(dLat/2.0) * Math.Sin(dLat/2.0) +
                Math.Sin(dLon/2.0) * Math.Sin(dLon/2.0) * Math.Cos(lat1) * Math.Cos(lat2)
        let c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0-a))
        (r * c) * 0.000621371

    [<FunctionName("upload")>]
    let run ([<HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)>]req: HttpRequest) (log: ILogger) =
        async {
            log.LogInformation("F# HTTP trigger upload gpx file request.")

            let x = req.Form.Files.["gpxfile"]
            
            let sample = GPX.Load(x.OpenReadStream())
            let minEle = sample.Trk.Trkseg.Trkpts |> Seq.minBy(fun x -> x.Ele) |> (fun x -> x.Ele)
            let eleProfile = Seq.pairwise (sample.Trk.Trkseg.Trkpts |> Seq.map (fun x -> LocEle(float x.Lat, float x.Lon, float ((x.Ele-minEle) * 3.28084M))))
                            |> Seq.map(fun (x:LocEle*LocEle) -> DistEle(calcdist (fst x).Location (snd x).Location, (fst x).Ele))

            let x = rev (eleProfile |> Seq.fold (fun x (y:DistEle) -> 
                let z:DistEle = x |> Seq.head 
                Seq.append [(DistEle(y.Dist+z.Dist, y.Ele))] x) (Seq.singleton (eleProfile |> Seq.head))) |>
                    Seq.map (fun x -> sprintf "[%f, %f]" x.Dist x.Ele ) |> String.concat ", "
            return OkObjectResult(sprintf "{\"data\":\"%s\"}" x ) :> IActionResult
        } |> Async.StartAsTask
