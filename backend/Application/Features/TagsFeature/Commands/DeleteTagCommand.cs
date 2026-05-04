using Application.Interfaces.Repositories;
using MediatR;

namespace Application.Features.TagsFeature.Commands
{
    public record DeleteTagCommand(Guid TagId) : IRequest<bool>;

    public class DeleteTagCommandHandler : IRequestHandler<DeleteTagCommand, bool>
    {
        private readonly ITagsRepository _tagsRepository;

        public DeleteTagCommandHandler(ITagsRepository tagsRepository)
        {
            _tagsRepository = tagsRepository;
        }

        public async Task<bool> Handle(DeleteTagCommand request, CancellationToken cancellationToken)
        {
            var tag = await _tagsRepository.GetByIdAsync(request.TagId, cancellationToken);
            if (tag == null)
            {
                return false;
            }

            var deleted = await _tagsRepository.SoftDelete(request.TagId);
            if (!deleted)
            {
                return false;
            }

            await _tagsRepository.SaveChange(cancellationToken);
            return true;
        }
    }
}
