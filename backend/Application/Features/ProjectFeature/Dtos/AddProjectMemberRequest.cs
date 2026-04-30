using Domain.Enums;

namespace Application.Features.ProjectFeature.Dtos
{
    public class AddProjectMemberRequest
    {
        public Guid UserId { get; set; }
        public ProjectRole Role { get; set; }
    }
}
