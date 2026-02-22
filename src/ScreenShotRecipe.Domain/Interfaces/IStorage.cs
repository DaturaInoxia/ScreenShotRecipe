using System.Threading.Tasks;

namespace ScreenShotRecipe.Domain.Interfaces
{
    public interface IStorage
    {
        Task<string> SaveImageAsync(byte[] bytes, string filename);
        Task<byte[]?> ReadImageAsync(string path);
    }
}
