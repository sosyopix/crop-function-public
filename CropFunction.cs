using System;
using System.Net.Http;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using System.Globalization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;

namespace inits.crop.function
{
    public static class CropFunction
    {
        private static HttpClient httpClient = new HttpClient();

        [FunctionName("CropFunction")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest request)
        {
            Image image = await LoadImage(request.Query);
            if (image == null) return RenderErrorImage();

            image = Rotate(image, request.Query);
            image = Crop(image, request.Query);
            image = Scale(image, request.Query);
            return RenderImage(image);
        }

        private static Image Rotate(Image image, IQueryCollection query)
        {
            int angle = Convert.ToInt32(query["a"]);
            if (angle == 90)
            {
                image.Mutate(x => x.Rotate(RotateMode.Rotate90));
            }
            else if (angle == 180)
            {
                image.Mutate(x => x.Rotate(RotateMode.Rotate180));
            }
            else if (angle == 270)
            {
                image.Mutate(x => x.Rotate(RotateMode.Rotate270));
            }
            return image;
        }

        private static Image Scale(Image image, IQueryCollection query)
        {
            if (!query.ContainsKey("s"))
            {
                return image;
            }

            double scale = double.Parse(query["s"], CultureInfo.InvariantCulture);
            if(scale < 1)
            {
                int resizeWidth = Convert.ToInt32(image.Width * scale);
                int resizeHeight = Convert.ToInt32(image.Height * scale);
                image.Mutate(x => x.Resize(resizeWidth, resizeHeight));
            }
            return image;
        }

        private static Image Crop(Image image, IQueryCollection query)
        {
            if (!query.ContainsKey("x") || !query.ContainsKey("y") || !query.ContainsKey("w") || !query.ContainsKey("h"))
            {
                return image;
            }

            if (!IsNumber(query["x"]) || !IsNumber(query["y"]) || !IsNumber(query["w"]) || !IsNumber(query["h"]))
            {
                return image;
            }

            int x = Decimal.ToInt32(Convert.ToDecimal(query["x"].ToString().Replace(',', '.'), CultureInfo.InvariantCulture));
            int y = Decimal.ToInt32(Convert.ToDecimal(query["y"].ToString().Replace(',', '.'), CultureInfo.InvariantCulture));
            int width = Decimal.ToInt32(Convert.ToDecimal(query["w"].ToString().Replace(',', '.'), CultureInfo.InvariantCulture));
            int height = Decimal.ToInt32(Convert.ToDecimal(query["h"].ToString().Replace(',', '.'), CultureInfo.InvariantCulture));

            // Make sure cropping rectangle is not larger than the image size
            if(x + width > image.Width) width = image.Width - x;
            if(y + height > image.Height) height = image.Height - y;

            image.Mutate(i => i.Crop(new Rectangle(x, y, width, height)));
            return image;
        }

        private static async Task<Image> LoadImage(IQueryCollection query)
        {
            try
            {
                var response = await httpClient.GetAsync("-YOUR IMAGE PATH-");
                if (response.IsSuccessStatusCode)
                {
                    var stream = await response.Content.ReadAsStreamAsync();
                    return Image.Load(stream);
                }
                else
                {
                    return null;
                }
            }
            catch (System.Exception)
            {
                return null;
            }
        }
        private static bool IsNumber(string value)
        {
            return Char.IsNumber(value.ToString().ToCharArray()[0]);
        }

        private static FileContentResult RenderImage(Image image)
        {
            using (var returnImageStream = new MemoryStream())
            {
                image.SaveAsJpeg(returnImageStream);
                return new FileContentResult(returnImageStream.ToArray(), "image/jpg");
            }
        }

        private static FileContentResult RenderErrorImage()
        {
            return RenderImage(
                Image.Load(System.Convert.FromBase64String("R0lGODlhAQABAIAAAP///wAAACH5BAEAAAAALAAAAAABAAEAAAICRAEAOw=="))
            );
        }
    }
}
