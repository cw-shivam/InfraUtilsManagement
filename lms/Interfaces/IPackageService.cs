using lms.Entities;

namespace lms.Interfaces
{
    public interface IPackageService
    {
        Task<String> CalculateNextTag(PackageEntity PackageEntity);
    }
}
