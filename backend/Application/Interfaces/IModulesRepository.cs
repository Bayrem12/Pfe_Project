using Domain.Entities.ProjectManagement;

namespace Application.Interfaces.Repositories
{
    /// <summary>
    /// Interface pour le repository des modules.
    /// Définit les méthodes disponibles pour gérer les modules.
    /// </summary>
    public interface IModulesRepository : IGenericRepository<Module>
    {
        /// <summary>
        /// Ajoute un nouveau module à la base de données.
        /// Correspond à la commande POST.
        /// </summary>
        /// <param name="module">Le module à ajouter</param>
        /// <returns>Le module ajouté avec son Id généré</returns>
        Task<Module> AddAsync(Module module);

        /// <summary>
        /// Récupère un module par son Id.
        /// Correspond à la requête GET by Id.
        /// </summary>
        /// <param name="id">Identifiant du module</param>
        /// <returns>Le module si trouvé, sinon null</returns>
        Task<Module?> GetByIdAsync(Guid id);

        /// <summary>
        /// Récupère tous les modules liés à un projet spécifique.
        /// Méthode utile pour lister tous les modules d’un projet.
        /// </summary>
        /// <param name="projectId">Identifiant du projet</param>
        /// <returns>Liste des modules du projet</returns>
        Task<IReadOnlyList<Module>> GetByProjectIdAsync(Guid projectId);
    }
}