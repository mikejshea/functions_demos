using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Hillbornfitness.com
{
    public static class upload
    {
        private static StringBuilder sb = new StringBuilder(40000);
        class DistEle
        {
            public DistEle(double dist, double ele)
            {
                Dist = dist;
                Ele = ele;
            }
            public double Dist { get; }
            public double Ele { get; }
            public override string ToString()
            {
                return string.Format("[{0:0.0000}, {1}]", Dist, (int)Ele);
            }
        }
        struct Trkpt {
            public Trkpt(double lat, double lon, double ele)
            {
                Lat = lat;
                Lon = lon;
                Ele = ele;
            }

            public double Lat { get; set;}
            public double Lon { get; set; }
            public double Ele { get; set; }

            public override string ToString() => $"({Lat}, {Lon}, {Ele})";
        }
        private static double GetDistanceBetweenPoints(double lat1, double lon1, double lat2, double lon2)
        {
            if ((lat1 == lat2) && (lon1 == lon2)) {
                return 0;
            }

            double theta = lon1 - lon2;
            double dist = Math.Sin(deg2rad(lat1)) * Math.Sin(deg2rad(lat2)) + Math.Cos(deg2rad(lat1)) * Math.Cos(deg2rad(lat2)) * Math.Cos(deg2rad(theta));
            dist = Math.Acos(dist);
            dist = rad2deg(dist);
            dist = dist * 60 * 1.1515;
            return (dist);
        }

        private static double deg2rad(double deg) {
            return (deg * Math.PI / 180.0);
        }
        private static double rad2deg(double rad) {
            return (rad / Math.PI * 180.0);
        }

        private static String convertTheGPX(IFormFile gpx) {
            XElement xml = XElement.Load(gpx.OpenReadStream());
            
            var mapList = 
                from c 
                    in xml.Elements().Where(e => e.Name.LocalName == "trk").Elements().Where(e => e.Name.LocalName == "trkseg").Elements().Where(e => e.Name.LocalName == "trkpt")
                select new Trkpt(
                    double.Parse(c.Attribute("lat").Value), 
                    double.Parse(c.Attribute("lon").Value),
                    double.Parse(c.Elements().First(e => e.Name.LocalName == "ele").Value) * 3.28084) ;
            
            var mapEnumerable = mapList.ToList();
            double minEle = mapEnumerable.Min(x => x.Ele);
            sb.Clear();
            sb.Append("{\"data\": \"");
            double totalDist = 0.0;
            
            for (int i = 0; i < mapEnumerable.Count() - 1; i++)
            {
                totalDist += GetDistanceBetweenPoints(
                    mapEnumerable[i].Lat,
                    mapEnumerable[i].Lon,
                    mapEnumerable[i + 1].Lat,
                    mapEnumerable[i + 1].Lon);
                sb.Append("[").Append(totalDist.ToString("0.0000"))
                    .Append(", ")
                    .Append(((int)(mapEnumerable[i].Ele-minEle)).ToString())
                    .Append("], ");
            }

            sb[^2] = '"';
            sb[^1] = '}';
            
            return sb.ToString();
        }

        [FunctionName("upload")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed an upload request.");
            IFormFile formFile = req.Form.Files["gpxfile"];
            return new OkObjectResult(convertTheGPX(formFile));
        }
    }
}
