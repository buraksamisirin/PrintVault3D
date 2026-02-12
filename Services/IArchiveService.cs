using System.Threading;
using System.Threading.Tasks;

namespace PrintVault3D.Services
{
    public interface IArchiveService
    {
        bool IsArchive(string filePath);
        Task<List<string>> ExtractAndFilterAsync(string archivePath, string destinationFolder);
    }
}
