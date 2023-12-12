using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;

namespace DataStorageTools
{
    public class ProcessedImage
    {
        public int Id { get; set; }
        public string ImagePath { get; set; }
        virtual public ICollection<DetectedObject> DetectedObjects { get; set; }
        public byte[] Image { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class DetectedObject
    {
        public int Id { get; set; }
        public double XMin { get; set; }
        public double YMin { get; set; }
        public double XMax { get; set; }
        public double YMax { get; set; }
        public double Confidence { get; set; }
        public string Class { get; set; }
    }

    public class LibraryContext : DbContext
    {
        public DbSet<ProcessedImage> ProcessedImages { get; set; }
        public DbSet<DetectedObject> DetectedObjects { get; set; }

        private string DataBasePath;

        public LibraryContext()
        {
            string path = Environment.CurrentDirectory;
            int ind = path.LastIndexOf("Lab 3");
            path = path.Remove(ind + "Lab 3".Length);
            DataBasePath = Path.Combine(path, "library.db");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder o) 
            => o.UseLazyLoadingProxies().UseSqlite($"Data Source={DataBasePath}");
    }
}
