using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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

        private Task CreateSessionTask;
        public string ProcessStatusMessage { get; set; } = "";
        private int filesToProcessCount = 0;
        private bool canDetect = true;

        public MainViewModel()
        {
            network = new Network();
            cts = new CancellationTokenSource();
            CreateSessionTask = Task.Run(() =>
            {
                try
                {
                    ProcessStatusMessage = "Creating session...";
                    network.CreateSession();
                    ProcessStatusMessage = "Session created!";
                }
                catch
                {
                    canDetect = false;
                }
            }, cts.Token);

            ChosenImages = new ObservableCollection<ImageInfo>();

            cancelDetectionCommand = new RelayCommand(_ => CancelDetection());

            chooseFilesAndDetectObjectsCommand = new AsyncRelayCommand(async _ => ChooseFilesAndDetectObjects());
        }

        private void CancelDetection()
        {
            if (filesToProcessCount == 0)
            {
                MessageBox.Show("Images are not currently being processed!");
            }
            else
            {
                cts.Cancel();
                ProcessStatusMessage = $"Processed images ({filesToProcessCount} canceled):";
                RaisePropertyChanged(nameof(ProcessStatusMessage));
            }
        }
        private async void ChooseFilesAndDetectObjects()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = true;
            openFileDialog.Filter = "JPG files (*.jpg)|*.jpg";

            if (openFileDialog.ShowDialog() == true)
            {
                List<Task> tasks = new();

                await CreateSessionTask;

                cts = new CancellationTokenSource();

                if (!canDetect)
                {
                    ProcessStatusMessage = "Error!";
                    RaisePropertyChanged(nameof(ProcessStatusMessage));
                    MessageBox.Show("Unable to create session for image processing!");
                    return;
                }

                ProcessStatusMessage = "Processing...";
                RaisePropertyChanged(nameof(ProcessStatusMessage));

                try
                {
                    filesToProcessCount = openFileDialog.FileNames.Length;
                    foreach (string filename in openFileDialog.FileNames)
                    {
                        var task = DetectAsync(filename, cts);
                        tasks.Add(task);
                    }
                }
                catch (Exception ex) 
                {
                    MessageBox.Show(ex.ToString());
                }
            }
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
                    Size = new SixLabors.ImageSharp.Size(TargetSize, TargetSize),
                    Mode = SixLabors.ImageSharp.Processing.ResizeMode.Pad,
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

            filesToProcessCount--;
            ChosenImages.Add(new ImageInfo(filePath, fileName, task.Count, annotatedImagePath));

            if (filesToProcessCount == 0)
            {
                ProcessStatusMessage = "Processed images:";
                RaisePropertyChanged(nameof(ProcessStatusMessage));
            }

            return task;
        }
    }
    public record ImageInfo(string FilePath, string FileName,
        int DetectedObjectsCount, string AnnotatedImagePath) { }
}
