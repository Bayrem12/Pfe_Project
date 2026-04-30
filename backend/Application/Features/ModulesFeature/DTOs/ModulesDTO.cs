using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Features.ModulesFeature.DTOs
{
        public class ModulesDTO
        {
            /// <summary>
            /// Identifiant du module
            /// </summary>
            public Guid Id { get; set; }

            /// <summary>
            /// Identifiant du projet auquel appartient le module
            /// </summary>
            public Guid ProjectId { get; set; }

            /// <summary>
            /// Nom du module
            /// </summary>
            public string Name { get; set; } = string.Empty;

            /// <summary>
            /// Description du module
            /// </summary>
            public string Description { get; set; } = string.Empty;

            /// <summary>
            /// Ordre d'affichage du module
            /// </summary>
            public int DisplayOrder { get; set; }
        }
    
}
