using Application.Features.TagsFeature.DTOs;
using Application.Interfaces.Repositories;
using MediatR;

namespace Application.Features.TagsFeature.Queries
{
    public record GetTagsByProjectIdQuery(Guid ProjectId) : IRequest<List<TagDTO>>;

    public class GetTagsByProjectIdQueryHandler : IRequestHandler<GetTagsByProjectIdQuery, List<TagDTO>>
    {
        private readonly ITagsRepository _tagsRepository;

        public GetTagsByProjectIdQueryHandler(ITagsRepository tagsRepository)
        {
            _tagsRepository = tagsRepository;
        }

        public async Task<List<TagDTO>> Handle(GetTagsByProjectIdQuery request, CancellationToken cancellationToken)
        {
            var tags = await _tagsRepository.GetByProjectIdAsync(request.ProjectId);

            return tags.Select(t => new TagDTO
            {
                Id = t.Id,
                ProjectId = t.ProjectId,
                Name = t.Name,
                Color = t.Color,
                Description = t.Description
            }).ToList();
        }
    }
}
