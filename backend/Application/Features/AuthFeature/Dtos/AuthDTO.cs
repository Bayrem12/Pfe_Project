// Application/Features/UserFeature/Dtos/UserDTO.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace Application.Features.UserFeature.Dtos
{
    /// <summary>
    /// DTO public pour l'utilisateur.
    /// Ne contient jamais PasswordHash et expose uniquement les champs
    /// nécessaires aux endpoints Auth/Users (register/login/refresh/change-password + listing).
    /// </summary>
    public class UserDTO
    {
        /// <summary>Identifiant unique.</summary>
        public Guid Id { get; set; }

        /// <summary>Prénom.</summary>
        public string FirstName { get; set; } = default!;

        /// <summary>Nom.</summary>
        public string LastName { get; set; } = default!;

        /// <summary>Email (identifiant de connexion).</summary>
        public string Email { get; set; } = default!;

        /// <summary>Compte actif ou non.</summary>
        public bool IsActive { get; set; }

        /// <summary>Nom complet calculé.</summary>
        public string FullName => $"{FirstName} {LastName}";

        /// <summary>
        /// Noms des rôles associés. Le service doit précharger les relations
        /// (Include(u => u.UserRoles).ThenInclude(ur => ur.Role)).
        /// </summary>
        public List<string> Roles { get; set; } = new();

        /// <summary>Date de création (optionnel, utile pour listing/admin).</summary>
        public DateTime? CreatedDate { get; set; }

        /// <summary>Date de dernière modification (optionnel).</summary>
        public DateTime? ModifiedDate { get; set; }

        /// <summary>
        /// Mappe l'entité Domain.Entities.Identity.User vers UserDTO.
        /// Ne force pas le chargement des relations.
        /// </summary>
        public static UserDTO FromEntity(Domain.Entities.Identity.User user)
            => new UserDTO
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                IsActive = user.IsActive,
                CreatedDate = user.CreatedDate,
                ModifiedDate = user.ModifiedDate,
                Roles = user.UserRoles?
                            .Select(ur => ur.Role?.Name)
                            .Where(n => !string.IsNullOrEmpty(n))
                            .ToList()
                        ?? new List<string>()
            };
    }
}