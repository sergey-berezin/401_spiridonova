using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace DataStorageTools
{
    public class DataBaseTools
    {
        public static bool IsInDataBase(string fileName)
        {
            using (var db = new LibraryContext())
            {
                var query = db.ProcessedImages.Where(a => a.ImagePath == fileName);
                return query.FirstOrDefault() != null;
            }
        }

        public static void Add(string imagePath, byte[] pixels, int heigth, int width,
            ICollection<DetectedObject> detectedObjects)
        {
            using (var db = new LibraryContext())
            {
                db.Add(new ProcessedImage
                {
                    ImagePath = imagePath,
                    DetectedObjects = detectedObjects,
                    Image = pixels,
                    Width = width,
                    Height = heigth,
                });
                db.SaveChanges();
            }
        }

        public static void Clear()
        {
            using (var db = new LibraryContext())
            {
                db.ProcessedImages.RemoveRange(db.ProcessedImages);
                db.DetectedObjects.RemoveRange(db.DetectedObjects);
                db.SaveChanges();
            }
        }
    }
}
