using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
            if (filesToProcessCount <= 0)
            {
                MessageBox.Show("Images are not currently being processed!");
            }
            else
            {
                cts.Cancel();
                ProcessStatusMessage = $"Processed images ({filesToProcessCount} canceled):";
                RaisePropertyChanged(nameof(ProcessStatusMessage));
                filesToProcessCount = 0;
            }
        }
        private async void ChooseFilesAndDetectObjects()
        {
            if (filesToProcessCount > 0)
            {
                MessageBox.Show("Other files are currently being processed! Please wait...");
                return;
            }

            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = true;
            openFileDialog.Filter = "JPG files (*.jpg)|*.jpg";

            if (openFileDialog.ShowDialog() == true)
            {
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
                        _ = DetectAsync(filename, cts);
                    }
                }
                catch (Exception ex) 
                {
                    MessageBox.Show(ex.ToString());
                }
            }
        }

        private async Task DetectAsync(string filePath,
            CancellationTokenSource cts)
        {
            var image = Image.Load<Rgb24>(filePath);

            var task = await network.GetDetectedObjectsAsync(image, cts.Token);
            filesToProcessCount--;
            
            const int TargetSize = 416;
            var annotated = image.Clone(x =>
            {
                x.Resize(new ResizeOptions
                {
                    Size = new SixLabors.ImageSharp.Size(TargetSize, TargetSize),
                    Mode = SixLabors.ImageSharp.Processing.ResizeMode.Pad,
                    PadColor = SixLabors.ImageSharp.Color.White
                });
            });

            foreach (var imageBox in task)
            {
                ObjectBox.Annotate(annotated, imageBox.Object);
            }

            byte[] pixels = new byte[annotated.Width * annotated.Height * Unsafe.SizeOf<Rgb24>()];
            annotated.CopyPixelDataTo(pixels);

            BitmapSource annotatedImage = BitmapSource.Create(annotated.Width, annotated.Height, 96, 96,
                PixelFormats.Rgb24, null, pixels, 3 * annotated.Width);

            string fileName = Path.GetFileNameWithoutExtension(filePath);

            BitmapSource bitmapImage = new BitmapImage(new Uri(filePath));

            ChosenImages.Add(new ImageInfo(fileName, bitmapImage, task.Count, annotatedImage));

            if (filesToProcessCount == 0)
            {
                ProcessStatusMessage = "Processed images:";
                RaisePropertyChanged(nameof(ProcessStatusMessage));
            }
        }
    }
    public record ImageInfo(string FileName, BitmapSource Image,
        int DetectedObjectsCount, BitmapSource AnnotatedImage) { }
}
