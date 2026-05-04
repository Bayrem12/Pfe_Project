using Application.Interfaces;
using Application.Interfaces.Repositories;
using Domain.Entities.ProjectManagement;
using Domain.Entities.Scenarios;
using Microsoft.EntityFrameworkCore;
using Persistance.Data;

namespace Persistance.Repositories
{
    /// <summary>
    /// Implémentation concrète du repository des modules.
    /// Permet d’interagir avec la base de données via DbContext.
    /// </summary>
    public class ModulesRepository : GenericRepository<Module>, IModulesRepository
    {
        public ModulesRepository(TestAutoumatisationContext context) : base(context)
        {
        }

        /// <summary>
        /// Ajoute un module dans la base de données.
        /// </summary>
        /// <param name="module">Le module à ajouter</param>
        /// <returns>Le module ajouté avec Id généré</returns>
        public async Task<Module> AddAsync(Module module)
        {
            // Ajoute le module dans le DbSet
            await _context.Modules.AddAsync(module);

            // Sauvegarde les changements dans la base de données
            await _context.SaveChangesAsync();

            return module;
        }

        /// <summary>
        /// Récupère un module par son identifiant.
        /// </summary>
        /// <param name="id">Id du module</param>
        /// <returns>Le module si trouvé, sinon null</returns>
        public async Task<Module?> GetByIdAsync(Guid id)
        {
            return await _context.Modules
                                 // Inclure les features liées au module
                                 .Include(m => m.Features)
                                 // Inclure le projet auquel appartient le module
                                 .Include(m => m.Project)
                                 // Filtrer par Id
                                 .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);
        }

        /// <summary>
        /// Récupère tous les modules appartenant à un projet donné.
        /// </summary>
        /// <param name="projectId">Id du projet</param>
        /// <returns>Liste des modules du projet</returns>
        public async Task<IReadOnlyList<Module>> GetByProjectIdAsync(Guid projectId)
        {
            return await _context.Modules
                                 .Include(m => m.Features) // Inclure les features si nécessaire
                                 .Where(m => m.ProjectId == projectId && !m.IsDeleted) // Filtrer par projet
                                 .ToListAsync();
        }
    }
}