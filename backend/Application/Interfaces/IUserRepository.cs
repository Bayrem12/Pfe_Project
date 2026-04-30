using Domain.Entities;
using Domain.Entities.Identity;

namespace Application.Interfaces.Identity
{
    // Interface pour gérer les utilisateurs et opérations d'authentification
    public interface IUserRepository : IGenericRepository<User>
    {
        // ---------------- AUTH ----------------

        /// <summary>
        /// Crée un nouvel utilisateur
        /// </summary>
        Task<User> CreateAsync(User user);

        /// <summary>
        /// Crée un nouvel utilisateur avec le rôle Viewer par défaut
        /// </summary>
        Task<User> CreateWithDefaultRoleAsync(User user);

        /// <summary>
        /// Vérifie si un utilisateur existe par email
        /// </summary>
        Task<bool> ExistsAsync(string email);

        /// <summary>
        /// Récupère un utilisateur par email
        /// </summary>
        Task<User?> GetByEmailAsync(string email);

        /// <summary>
        /// Met à jour un utilisateur (auth/infos)
        /// </summary>
        Task UpdateAsync(User user);

        // ---------------- USER OPERATIONS ----------------

        /// <summary>
        /// Récupère tous les utilisateurs
        /// </summary>
        Task<List<User>> GetAllUsersAsync();

        /// <summary>
        /// Récupère un utilisateur par son ID
        /// </summary>
        Task<User?> GetUserByIdAsync(Guid id);

        /// <summary>
        /// Recherche des utilisateurs par mot-clé
        /// </summary>
        Task<List<User>> SearchUsersAsync(string keyword);

        /// <summary>
        /// Met à jour un utilisateur (infos générales)
        /// </summary>
        Task UpdateUserAsync(User user);

        /// <summary>
        /// Met à jour les rôles d'un utilisateur
        /// </summary>
        Task UpdateUserRolesAsync(Guid id, List<string> roles);

        /// <summary>
        /// Active ou désactive un utilisateur
        /// </summary>
        Task ToggleUserStatusAsync(Guid id);

        /// <summary>
        /// Récupère un utilisateur par son token de vérification d'email
        /// </summary>
        Task<User?> GetByVerificationTokenAsync(string token);
    }
}