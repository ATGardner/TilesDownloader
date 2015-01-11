﻿namespace com.atgardner.Downloader
{
    using com.atgardner.Downloader.Properties;
    using Gavaghan.Geodesy;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Runtime.Serialization.Formatters.Binary;
    using System.Security.Cryptography;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    public class Downloader
    {
        private static readonly Regex subDomainRegExp = new Regex(@"\[(.*)\]");
        private static readonly GeodeticCalculator calc = new GeodeticCalculator();
        private static readonly int[] degrees = new[] { 0, 90, 180, 270 };

        private int subDomainNum;

        public IList<Task<string>> DownloadTiles(IEnumerable<GlobalCoordinates> coordinates, int[] zoomLevels, MapSource source)
        {
            subDomainNum = 0;
            var tiles = GenerateTiles(coordinates, zoomLevels);
            var tasks = new List<Task<string>>();
            foreach (var tile in tiles)
            {
                //if (source.Ammount == 0)
                //{
                //    break;
                //}

                var task = DownloadTileAsync(source, tile);
                tasks.Add(task);
            }

            return tasks;
        }

        private IEnumerable<Tile> GenerateTiles(IEnumerable<GlobalCoordinates> coordinates, int[] zoomLevels)
        {
            var uniqueTiles = new HashSet<Tile>();
            foreach (var c in coordinates)
            {
                var lon = c.Longitude.Degrees;
                var lat = c.Latitude.Degrees;
                foreach (var zoom in zoomLevels)
                {
                    var tile = WorldToTilePos(c, zoom);
                    if (!uniqueTiles.Contains(tile))
                    {
                        uniqueTiles.Add(tile);
                        yield return tile;
                    }

                    if (zoom > 12)
                    {
                        foreach (var c2 in GetCoordinatesAround(c, 1609.34))
                        {
                            tile = WorldToTilePos(c2, zoom);
                            if (!uniqueTiles.Contains(tile))
                            {
                                uniqueTiles.Add(tile);
                                yield return tile;
                            }
                        }
                    }
                }
            }
        }

        private async Task<string> DownloadTileAsync(MapSource source, Tile tile)
        {
            var address = GetAddress(source.Address, tile);
            var ext = Path.GetExtension(address);
            var fileName = string.Format("{0}/{1}/{2}/{3}{4}", source.Name, tile.Zoom, tile.X, tile.Y, ext);
            if (File.Exists(fileName))
            {
                return fileName;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(fileName));
            source.LastAccess = DateTime.Today;
            source.Ammount--;
            await PerformDownload(address, fileName);
            return fileName;
        }

        private async Task PerformDownload(string address, string fileName)
        {
            //using (var webClient = new WebClient())
            //{
            //    await webClient.DownloadFileTaskAsync(address, fileName);
            //    IncreaseCounter();
            //}

            await Task.Delay(100);
            Console.WriteLine("downloaded {0}", address);
        }

        private static IEnumerable<GlobalCoordinates> GetCoordinatesAround(GlobalCoordinates origin, double distance)
        {
            for (var i = 500; i < distance; i += 500)
            {

                foreach (var startBearing in degrees)
                {
                    yield return calc.CalculateEndingGlobalCoordinates(Ellipsoid.WGS84, origin, startBearing, i);
                }
            }
        }

        private static Tile WorldToTilePos(GlobalCoordinates coordinate, int zoom)
        {
            var lon = coordinate.Longitude.Degrees;
            var lat = coordinate.Latitude.Degrees;
            var x = (int)((lon + 180.0) / 360.0 * (1 << zoom));
            var y = (int)((1.0 - Math.Log(Math.Tan(lat * Math.PI / 180.0) + 1.0 / Math.Cos(lat * Math.PI / 180.0)) / Math.PI) / 2.0 * (1 << zoom));
            return new Tile(x, y, zoom);
        }

        private string GetAddress(string addressTemplate, Tile tile)
        {
            var match = subDomainRegExp.Match(addressTemplate);
            if (match.Success)
            {
                var subDomain = match.Groups[1].Value;
                var currentSubDomain = subDomain.Substring(subDomainNum, 1);
                subDomainNum = (subDomainNum + 1) % subDomain.Length;
                addressTemplate = subDomainRegExp.Replace(addressTemplate, currentSubDomain);
            }

            return addressTemplate.Replace("{z}", "{zoom}").Replace("{zoom}", tile.Zoom.ToString()).Replace("{x}", tile.X.ToString()).Replace("{y}", tile.Y.ToString());
        }

        //private static string ComputeHash(IEnumerable<GlobalCoordinates> coordinates)
        //{
        //    byte[] bytes;
        //    using (var stream = new MemoryStream())
        //    {
        //        var bf = new BinaryFormatter();
        //        bf.Serialize(stream, coordinates.ToArray());
        //        bytes = stream.ToArray();
        //    }

        //    var md5 = MD5.Create();
        //    var hash = md5.ComputeHash(bytes);
        //    return Convert.ToBase64String(hash);
        //}
    }
}