using Domain.Entities.ProjectManagement;

namespace Application.Interfaces
{
    public interface IFeatureRepository
    {
        Task<Feature> Post(Feature feature);
        Task<Feature> Update(Feature feature);
        Task SoftDelete(Feature feature);
        Task<Feature?> GetById(Guid id);
        Task<List<Feature>> GetByModuleId(Guid moduleId);
        Task<List<Feature>> GetAll();
        Task<int> SaveChange(CancellationToken cancellationToken);
    }
}
