using Domain.Entities.Scenarios;

namespace Application.Interfaces.Repositories
{
    /// <summary>
    /// Repository pour la gestion des tags de scénarios.
    /// </summary>
    public interface ITagsRepository : IGenericRepository<Tag>
    {
        /// <summary>
        /// Ajouter un tag.
        /// </summary>
        Task<Tag> AddAsync(Tag tag);

        /// <summary>
        /// Récupérer les tags d'un projet.
        /// </summary>
        Task<IReadOnlyList<Tag>> GetByProjectIdAsync(Guid projectId);
    }
}
