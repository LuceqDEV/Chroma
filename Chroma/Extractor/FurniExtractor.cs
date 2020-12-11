﻿using Chroma;
using Flazzy;
using Flazzy.ABC;
using Flazzy.ABC.AVM2;
using Flazzy.ABC.AVM2.Instructions;
using Flazzy.IO;
using Flazzy.Tags;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Extractor
{
    public class FurniExtractor
    {
        public static bool Parse(string file)
        {
            string fileName = Path.GetFileNameWithoutExtension(file);

            //if (Directory.Exists(@"furni_export\" + fileName))
            //    return false;

            var flash = new ShockwaveFlash(file);
            flash.Disassemble();


            if (!Directory.Exists(@"furni_export/" + fileName))
                Directory.CreateDirectory(@"furni_export/" + fileName);

            var symbolClass = flash.Tags.Where(t => t.Kind == TagKind.SymbolClass).Cast<SymbolClassTag>().First();
            var imageTags = flash.Tags.Where(t => t.Kind == TagKind.DefineBitsLossless2).Cast<DefineBitsLossless2Tag>();
            var dataTags = flash.Tags.Where(t => t.Kind == TagKind.DefineBinaryData).Cast<DefineBinaryDataTag>();

            var furni = new Json.JsonFurniData();
            furni.visualization = new Json.Visualizations();
            furni.logic = new Json.Logic();
            furni.assets = new Dictionary<string, Json.Asset>();

            foreach (var data in dataTags)
            {
                var name = symbolClass.Names[symbolClass.Ids.IndexOf(data.Id)];
                var type = name.Split('_')[name.Split('_').Length - 1];
                var txt = Encoding.Default.GetString(data.Data);

                if (!Directory.Exists(@"furni_export/" + fileName + "/xml"))
                    Directory.CreateDirectory(@"furni_export/" + fileName + "/xml");

                if (!File.Exists(@"furni_export/" + fileName + "/xml/" + type + ".xml"))
                    File.WriteAllText(@"furni_export/" + fileName + "/xml/" + type + ".xml", txt);
            }

            var assetDocument = FileUtil.SolveXmlFile(@"furni_export/" + fileName + "/xml", "assets");
            var assets = assetDocument.SelectNodes("//assets/asset");

            foreach (var image in imageTags)
            {
                var name = symbolClass.Names[symbolClass.Ids.IndexOf(image.Id)];
                var xmlName = name.Substring(fileName.Length + 1);

                if (!File.Exists(@"furni_export/" + fileName + "/" + xmlName + ".png"))
                {
                    System.Drawing.Color[,] table = image.GetARGBMap();

                    int width = table.GetLength(0);
                    int height = table.GetLength(1);
                    using (var payload = new Image<Rgba32>(width, height))
                    {
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                System.Drawing.Color pixel = table[x, y];
                                payload[x, y] = new Rgba32(pixel.R, pixel.G, pixel.B, pixel.A);
                            }
                        }

                        /*if (asset.Attributes.GetNamedItem("flipH") != null &&
                            asset.Attributes.GetNamedItem("flipH").InnerText == "1")
                        {
                            payload.Mutate(ctx => ctx.Flip(FlipMode.Vertical));
                        }*/

                        using (var output = new StreamWriter(@"furni_export/" + fileName + "/" + xmlName + ".png"))
                        {
                            payload.SaveAsPng(output.BaseStream);
                        }
                    }
                }
            }

            for (int i = 0; i < assets.Count; i++)
            {
                var asset = assets.Item(i);

                if (asset.Attributes.GetNamedItem("source") == null)
                {
                    continue;
                }

                var source = asset.Attributes.GetNamedItem("source").InnerText;
                var image = asset.Attributes.GetNamedItem("name").InnerText;

                var assetImage = FileUtil.SolveFile(@"furni_export/" + fileName, source);

                if (assetImage != null)
                {
                    var newName = image + ".png";
                    var newPath = Path.Combine(@"furni_export", fileName, newName);

                    if (!File.Exists(newPath))
                    {
                        File.Copy(assetImage, newPath);

                        if (asset.Attributes.GetNamedItem("flipH") != null &&
                            asset.Attributes.GetNamedItem("flipH").InnerText == "1")
                        {
                            var bitmap1 = (Bitmap)Bitmap.FromFile(newPath);
                            bitmap1.RotateFlip(RotateFlipType.Rotate180FlipY);
                            bitmap1.Save(newPath);
                            bitmap1.Dispose();
                        }
                    }
                }
            }

             return true;
        }
    }
}