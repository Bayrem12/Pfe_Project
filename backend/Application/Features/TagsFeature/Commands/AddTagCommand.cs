using Application.Interfaces;
using Application.Interfaces.Repositories;
using Domain.Entities.Scenarios;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.TagsFeature.Commands
{
    public record AddTagCommand(Guid ProjectId, string Name, string? Color = null, string? Description = null) : IRequest<Guid>;

    public class AddTagCommandHandler : IRequestHandler<AddTagCommand, Guid>
    {
        private readonly ITagsRepository _tagsRepository;
        private readonly ITestTestAutoumatisationContext _context;

        public AddTagCommandHandler(ITagsRepository tagsRepository, ITestTestAutoumatisationContext context)
        {
            _tagsRepository = tagsRepository;
            _context = context;
        }

        public async Task<Guid> Handle(AddTagCommand request, CancellationToken cancellationToken)
        {
            var projectExists = await _context.Projects.AnyAsync(p => p.Id == request.ProjectId, cancellationToken);
            if (!projectExists)
            {
                throw new InvalidOperationException($"Project with id '{request.ProjectId}' does not exist.");
            }

            var normalizedName = request.Name.Trim();

            var duplicateExists = await _context.Tags.AnyAsync(
                t => t.ProjectId == request.ProjectId && !t.IsDeleted && t.Name.ToLower() == normalizedName.ToLower(),
                cancellationToken);

            if (duplicateExists)
            {
                throw new InvalidOperationException($"Tag '{normalizedName}' already exists for this project.");
            }

            var tag = new Tag
            {
                Id = Guid.NewGuid(),
                ProjectId = request.ProjectId,
                Name = normalizedName,
                Color = request.Color ?? "#6366F1",
                Description = request.Description,
                CreatedDate = DateTime.UtcNow
            };

            var result = await _tagsRepository.AddAsync(tag);
            return result.Id;
        }
    }
}
