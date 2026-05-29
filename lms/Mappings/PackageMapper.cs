using lms.Dtos;
using lms.Entities;
using Riok.Mapperly.Abstractions;

namespace lms.Mappings
{
    [Mapper]
    public partial class PackageMapper
    {
        public partial PackageEntity Map(PackageDto packageDto);
    }
}
