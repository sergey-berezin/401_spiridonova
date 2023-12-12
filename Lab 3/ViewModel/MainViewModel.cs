using System;
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
using System.Collections.Generic;
using DataStorageTools;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        private Network network;

        private CancellationTokenSource cts;
        public ObservableCollection<ImageInfo> ChosenImages { get; set; }

        private readonly ICommand cancelDetectionCommand;
        private readonly ICommand chooseFilesAndDetectObjectsCommand;
        private readonly ICommand clearDataCommand;
        public ICommand CancelDetectionCommand => cancelDetectionCommand;
        public ICommand ChooseFilesAndDetectObjectsCommand => chooseFilesAndDetectObjectsCommand;
        public ICommand ClearDataCommand => clearDataCommand;
        
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

            DisplayStoredData();

            cancelDetectionCommand = new RelayCommand(_ => CancelDetection());

            chooseFilesAndDetectObjectsCommand = new AsyncRelayCommand(async _ => ChooseFilesAndDetectObjects());

            clearDataCommand = new RelayCommand(_ => ClearData());
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

        private void ClearData()
        {
            ChosenImages.Clear();
            DataBaseTools.Clear();
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
                        if (DataBaseTools.IsInDataBase(filename))
                        {
                            filesToProcessCount--;
                            if (filesToProcessCount == 0)
                            {
                                ProcessStatusMessage = "Processed images:";
                                RaisePropertyChanged(nameof(ProcessStatusMessage));
                            }
                        }
                        else
                        {
                            _ = DetectAsync(filename, cts);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            }
        }

        private async Task DetectAsync(string filePath, CancellationTokenSource cts)
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

            List<DetectedObject> detectedObjects = new ();
            foreach (var imageBox in task)
            {
                ObjectBox.Annotate(annotated, imageBox.Object);

                detectedObjects.Add(new DetectedObject
                {
                    XMin = imageBox.Object.XMin,
                    YMin = imageBox.Object.YMin,
                    XMax = imageBox.Object.XMax,
                    YMax = imageBox.Object.YMax,
                    Confidence = imageBox.Object.Confidence,
                    Class = imageBox.Object.Class
                });
            }

            byte[] bytes = new byte[image.Width * image.Height * Unsafe.SizeOf<Rgb24>()];
            image.CopyPixelDataTo(bytes);
            DataBaseTools.Add(filePath, bytes, image.Height, image.Width, detectedObjects);

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

        private void DisplayStoredData()
        {
            using (var db = new LibraryContext())
            {
                foreach (var img in db.ProcessedImages)
                {
                    var image = Image.LoadPixelData<Rgb24>(img.Image, img.Width, img.Height);

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

                    int objectsCount = 0;
                    foreach (var obj in img.DetectedObjects)
                    {
                        ObjectBox objectBox = new(
                            obj.XMin,
                            obj.YMin,
                            obj.XMax,
                            obj.YMax,
                            obj.Confidence,
                            obj.Class
                        );
                        ObjectBox.Annotate(annotated, objectBox);
                        objectsCount++;
                    }

                    byte[] pixels = new byte[annotated.Width * annotated.Height * Unsafe.SizeOf<Rgb24>()];
                    annotated.CopyPixelDataTo(pixels);

                    BitmapSource annotatedImage = BitmapSource.Create(annotated.Width, annotated.Height, 96, 96,
                        PixelFormats.Rgb24, null, pixels, 3 * annotated.Width);

                    string fileName = Path.GetFileNameWithoutExtension(img.ImagePath);

                    BitmapSource bitmapImage = new BitmapImage(new Uri(img.ImagePath));

                    ChosenImages.Add(new ImageInfo(fileName, bitmapImage, objectsCount, annotatedImage));
                }
            }
        }
    }

    public record ImageInfo(string FileName, BitmapSource Image,
        int DetectedObjectsCount, BitmapSource AnnotatedImage){ }
}
