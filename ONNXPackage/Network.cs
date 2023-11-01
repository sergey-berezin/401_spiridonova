using System.Net;
using SixLabors.ImageSharp.Drawing.Processing;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.OnnxRuntime;
using SixLabors.Fonts;

namespace ONNXPackage
{
    public class Network
    {
        public InferenceSession? session;

        public void CreateSession()
        {
            if (!File.Exists("tinyyolov2-8.onnx"))
            {
                DownloadNetwork();
            }
            session = new InferenceSession("tinyyolov2-8.onnx");
        }
        public static void DownloadNetwork()
        {
            string remoteUri = "https://storage.yandexcloud.net/dotnet4/tinyyolov2-8.onnx";
            string fileName = "tinyyolov2-8.onnx";
            string myStringWebResource = remoteUri;
            WebClient myWebClient = new WebClient();

            int trialNumber = 0;

            while (!File.Exists("tinyyolov2-8.onnx") && trialNumber++ < 10)
            {
                try
                {
                    myWebClient.DownloadFile(myStringWebResource, fileName);
                }
                catch
                {
                    //Console.WriteLine("Trying to download network again.");
                }
            }

            if (trialNumber == 11)
            {
                throw new Exception("Unable to download network!");
            }
        }

        public Task<List<ImageBox>> GetDetectedObjectsAsync(
            Image<Rgb24> image, CancellationToken ct)
        {
            return Task.Factory.StartNew(() => DetectObjects(image, ct), ct,
                TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public List<ImageBox> DetectObjects(Image<Rgb24> image, CancellationToken ct)
        {

            int imageWidth = image.Width;
            int imageHeight = image.Height;

            // Размер изображения
            const int TargetSize = 416;

            // Изменяем размер изображения до 416 x 416
            var resized = image.Clone(x =>
            {
                x.Resize(new ResizeOptions
                {
                    Size = new Size(TargetSize, TargetSize),
                    Mode = ResizeMode.Pad // Дополнить изображение до указанного размера с сохранением пропорций
                    //PadColor = Color.White
                });
            });

            // Перевод пикселов в тензор и нормализация
            var input = new DenseTensor<float>(new[] { 1, 3, TargetSize, TargetSize });
            resized.ProcessPixelRows(pa =>
            {
                for (int y = 0; y < TargetSize; y++)
                {
                    Span<Rgb24> pixelSpan = pa.GetRowSpan(y);
                    for (int x = 0; x < TargetSize; x++)
                    {
                        input[0, 0, y, x] = pixelSpan[x].R;
                        input[0, 1, y, x] = pixelSpan[x].G;
                        input[0, 2, y, x] = pixelSpan[x].B;
                    }
                }
            });

            // Подготавливаем входные данные нейросети. Имя input задано в файле модели
            var inputs = new List<NamedOnnxValue>
            {
               NamedOnnxValue.CreateFromTensor("image", input),
            };

            if (session == null)
                throw new Exception("Session is null!");

            // Вычисляем предсказание нейросетью
            IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results;
            lock (session)
            {
                results = session.Run(inputs);
            }

            ct.ThrowIfCancellationRequested();

            // Получаем результаты
            var outputs = results.First().AsTensor<float>();

            const int CellCount = 13; // 13x13 ячеек
            const int BoxCount = 5; // 5 прямоугольников в каждой ячейке
            const int ClassCount = 20; // 20 классов

            string[] labels = new string[]
            {
                "aeroplane", "bicycle", "bird", "boat", "bottle",
                "bus", "car", "cat", "chair", "cow",
                "diningtable", "dog", "horse", "motorbike", "person",
                "pottedplant", "sheep", "sofa", "train", "tvmonitor"
            };

            float Sigmoid(float value)
            {
                var e = (float)Math.Exp(value);
                return e / (1.0f + e);
            }

            float[] Softmax(float[] values)
            {
                var exps = values.Select(v => Math.Exp(v));
                var sum = exps.Sum();
                return exps.Select(e => (float)(e / sum)).ToArray();
            }

            int IndexOfMax(float[] values)
            {
                int idx = 0;
                for (int i = 1; i < values.Length; i++)
                    if (values[i] > values[idx])
                        idx = i;
                return idx;
            }

            var anchors = new (double, double)[]
            {
               (1.08, 1.19),
               (3.42, 4.41),
               (6.63, 11.38),
               (9.42, 5.11),
               (16.62, 10.52)
            };

            int cellSize = TargetSize / CellCount;

            List<ObjectBox> objects = new();

            for (var row = 0; row < CellCount; row++)
                for (var col = 0; col < CellCount; col++)
                    for (var box = 0; box < BoxCount; box++)
                    {
                        var rawX = outputs[0, (5 + ClassCount) * box, row, col];
                        var rawY = outputs[0, (5 + ClassCount) * box + 1, row, col];

                        var rawW = outputs[0, (5 + ClassCount) * box + 2, row, col];
                        var rawH = outputs[0, (5 + ClassCount) * box + 3, row, col];

                        var x = (float)((col + Sigmoid(rawX)) * cellSize);
                        var y = (float)((row + Sigmoid(rawY)) * cellSize);

                        var w = (float)(Math.Exp(rawW) * anchors[box].Item1 * cellSize);
                        var h = (float)(Math.Exp(rawH) * anchors[box].Item2 * cellSize);

                        var conf = Sigmoid(outputs[0, (5 + ClassCount) * box + 4, row, col]);

                        if (conf > 0.5)
                        {
                            var classes
                            = Enumerable
                            .Range(0, ClassCount)
                            .Select(i => outputs[0, (5 + ClassCount) * box + 5 + i, row, col])
                            .ToArray();
                            objects.Add(new ObjectBox(x - w / 2, y - h / 2, x + w / 2, y + h / 2, conf, labels[IndexOfMax(Softmax(classes))]));
                        }
                    }

            // Убираем дубликаты
            for (int i = 0; i < objects.Count; i++)
            {
                var o1 = objects[i];
                for (int j = i + 1; j < objects.Count;)
                {
                    var o2 = objects[j];
                    //Console.WriteLine($"IoU({i},{j})={o1.IoU(o2)}");
                    if (o1.Class == o2.Class && o1.IoU(o2) > 0.6)
                    {
                        if (o1.Confidence < o2.Confidence)
                        {
                            objects[i] = o1 = objects[j];
                        }
                        objects.RemoveAt(j);
                    }
                    else
                    {
                        j++;
                    }
                }
            }

            var result = new List<ImageBox>();

            int objectNumber = 1;
            foreach (var obj in objects)
            {
                var final = resized.Clone();
                ObjectBox.Annotate(final, obj);

                var resultFileName = $"detected_{obj.Class}" + $"{objectNumber}" + ".jpg";

                ImageBox imageBox = new(obj, final, resultFileName);
                result.Add(imageBox);
                objectNumber++;
            }
            return result;
        }
    }

    public record ObjectBox(double XMin, double YMin, double XMax, double YMax, double Confidence, string Class)
    {
        public double IoU(ObjectBox b2) =>
            (Math.Min(XMax, b2.XMax) - Math.Max(XMin, b2.XMin)) * (Math.Min(YMax, b2.YMax) - Math.Max(YMin, b2.YMin)) /
            ((Math.Max(XMax, b2.XMax) - Math.Min(XMin, b2.XMin)) * (Math.Max(YMax, b2.YMax) - Math.Min(YMin, b2.YMin)));

        public static void Annotate(Image<Rgb24> target, ObjectBox obj)
        {
            target.Mutate(ctx =>
            {
                ctx.DrawPolygon(
                    Pens.Solid(Color.DeepPink, 2),
                    new PointF[] {
                                new PointF((float)obj.XMin, (float)obj.YMin),
                                new PointF((float)obj.XMin, (float)obj.YMax),
                                new PointF((float)obj.XMax, (float)obj.YMax),
                                new PointF((float)obj.XMax, (float)obj.YMin)
                    });

                ctx.DrawText(
                    $"{obj.Class}",
                    SystemFonts.Families.First().CreateFont(16),
                    Color.DeepPink,
                    new PointF((float)obj.XMin, (float)obj.YMax));
            });
        }
    }

    public record ImageBox(ObjectBox Object, Image<Rgb24> Image, string ImageFile) { }
}
