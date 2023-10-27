using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using ONNXPackage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        private Network network;

        private CancellationTokenSource cts;
        public ObservableCollection<ImageInfo> ChosenImages { get; set; }

        private readonly ICommand cancelDetectionCommand;
        private readonly ICommand chooseFilesAndDetectObjectsCommand;
        public ICommand CancelDetectionCommand => cancelDetectionCommand;
        public ICommand ChooseFilesAndDetectObjectsCommand => chooseFilesAndDetectObjectsCommand;
        public string ProcessStatus { get; set; } = "";
        private Task CreateSessionTask;

        public MainViewModel()
        {
            network = new Network();
            cts = new CancellationTokenSource();
            CreateSessionTask = Task.Run(() => network.CreateSession(), cts.Token);

            ChosenImages = new ObservableCollection<ImageInfo>();
            
            cancelDetectionCommand = new RelayCommand(_ => CancelDetection());

            chooseFilesAndDetectObjectsCommand = new AsyncRelayCommand(async _ => ChooseFilesAndDetectObjects());
        }

        private void CancelDetection()
        {
            cts.Cancel();
        }
        private async void ChooseFilesAndDetectObjects()
        {
            ProcessStatus = "Processing...";
            RaisePropertyChanged(nameof(ProcessStatus));

            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = true;
            openFileDialog.Filter = "JPG files (*.jpg)|*.jpg";

            if (openFileDialog.ShowDialog() == true)
            {
                List<Task> tasks = new();

                await CreateSessionTask;

                foreach (string filename in openFileDialog.FileNames)
                {
                    var task = DetectAsync(filename, cts);
                    tasks.Add(task);
                }
            }
            ProcessStatus = "Processed";
            RaisePropertyChanged(nameof(ProcessStatus));
        }

        private async Task<List<ImageBox>> DetectAsync(string filePath,
            CancellationTokenSource cts)
        {
            var image = Image.Load<Rgb24>(filePath);
            var task = await network.GetDetectedObjectsAsync(image, cts.Token);

            const int TargetSize = 416;
            var annotated = image.Clone(x =>
            {
                x.Resize(new ResizeOptions
                {
                    Size = new Size(TargetSize, TargetSize),
                    Mode = ResizeMode.Pad,
                    PadColor = Color.White
                });
            });

            foreach (var imageBox in task)
            {
                Network.Annotate(annotated, imageBox.Object);
            }

            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string annotatedImagePath = Environment.CurrentDirectory + "/" + fileName + "_annotated.jpg";
            annotated.SaveAsJpeg(annotatedImagePath);

            ChosenImages.Add(new ImageInfo(filePath, fileName, task.Count, annotatedImagePath));
            
            return task;
        }
    }
    public record ImageInfo(string FilePath, string FileName,
        int DetectedObjectsCount, string AnnotatedImagePath) { }
}
