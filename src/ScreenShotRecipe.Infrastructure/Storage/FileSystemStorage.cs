using System;
using System.IO;
using System.Threading.Tasks;
using ScreenShotRecipe.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ScreenShotRecipe.Infrastructure.Storage
{
    public class FileSystemStorage : IStorage
    {
        private readonly string _root;
        private readonly ILogger<FileSystemStorage> _logger;

        public FileSystemStorage(string root, ILogger<FileSystemStorage> logger)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _logger = logger;
            Directory.CreateDirectory(_root);
            _logger.LogInformation("FileSystemStorage initialized with root directory: {RootPath}", _root);
        }

        public async Task<string> SaveImageAsync(byte[] bytes, string filename)
        {
            try
            {
                var safe = Path.GetRandomFileName() + "_" + Path.GetFileName(filename);
                var path = Path.Combine(_root, safe);
                await File.WriteAllBytesAsync(path, bytes);
                _logger.LogInformation("Image saved successfully. Original filename: {OriginalFilename}, Saved as: {SavedPath}, Size: {FileSize} bytes", 
                    filename, safe, bytes.Length);
                return path;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving image: {Filename}", filename);
                throw;
            }
        }

        public async Task<byte[]?> ReadImageAsync(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    _logger.LogWarning("Image file not found: {ImagePath}", path);
                    return null;
                }
                var bytes = await File.ReadAllBytesAsync(path);
                _logger.LogInformation("Image read successfully: {ImagePath}, Size: {FileSize} bytes", path, bytes.Length);
                return bytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading image: {ImagePath}", path);
                throw;
            }
        }
    }
}
