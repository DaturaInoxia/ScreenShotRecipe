using System;
using System.IO;
using System.Threading.Tasks;
using ScreenShotRecipe.Domain.Interfaces;

namespace ScreenShotRecipe.Infrastructure.Storage
{
    public class FileSystemStorage : IStorage
    {
        private readonly string _root;

        public FileSystemStorage(string root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            Directory.CreateDirectory(_root);
        }

        public async Task<string> SaveImageAsync(byte[] bytes, string filename)
        {
            var safe = Path.GetRandomFileName() + "_" + Path.GetFileName(filename);
            var path = Path.Combine(_root, safe);
            await File.WriteAllBytesAsync(path, bytes);
            return path;
        }

        public async Task<byte[]?> ReadImageAsync(string path)
        {
            if (!File.Exists(path)) return null;
            return await File.ReadAllBytesAsync(path);
        }
    }
}
