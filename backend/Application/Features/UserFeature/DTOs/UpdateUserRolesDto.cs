using System;
using System.Collections.Generic;

namespace Application.Features.UserFeature.DTOs
{
    public class UpdateUserRolesDto
    {
        public List<string> Roles { get; set; } = new List<string>();
    }
}
