namespace ONNXPackage
{
    class Program
    {
        static Network network = new Network();

        static void Main(string[] args)
        {
            var cts = new CancellationTokenSource();

            Task[] tasks;

            try
            {
                CheckArgs(args);
                List<string> fileNames = args.ToList();

                tasks = new Task[fileNames.Count];

                int taskNumber = 0;
                foreach (var fileName in fileNames)
                {
                    var csvFileName = fileName.Replace('.', '_') + "_detected_objects.csv";
                    var task = DetectAsync(fileName, cts, csvFileName);
                    tasks[taskNumber] = task;
                    taskNumber++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }

            Task.WaitAll(tasks);
        }

        static void CheckArgs(string[] args)
        {
            if (args.Length == 0)
                throw new Exception("No files given!");

            foreach (var fileName in args)
            {
                if (!File.Exists(fileName))
                    throw new Exception($"File {fileName} doesn't exist");
            }
        }

        static async Task<List<ImageBox>> DetectAsync(string fileName,
            CancellationTokenSource cts, string csvFileName)
        {
            var image = Image.Load<Rgb24>(fileName);
            var task = await network.GetDetectedObjectsAsync(image, cts.Token);
            foreach (var imageBox in task)
            {
                imageBox.Image.SaveAsJpeg(fileName.Replace('.', '_') + "_" + imageBox.ImageFile);

                if (!File.Exists(csvFileName))
                    File.AppendAllLines(csvFileName, new List<string>() {
                            $"FileName,Class,X,Y,Width,Height" });

                File.AppendAllLines(csvFileName, new List<string>() {
                    $"{fileName.Replace('.', '_') + '_' + imageBox.ImageFile}," +
                    $"{imageBox.Object.Class}," +
                    $"{imageBox.Object.XMin.ToString().Replace(',', '.')}," +
                    $"{imageBox.Object.YMin.ToString().Replace(',', '.')}," +
                    $"{(imageBox.Object.XMax - imageBox.Object.XMin).ToString().Replace(',', '.')}," +
                    $"{(imageBox.Object.YMax - imageBox.Object.YMin).ToString().Replace(',', '.')}"
                });
            }
            return task;
        }
    }
}
