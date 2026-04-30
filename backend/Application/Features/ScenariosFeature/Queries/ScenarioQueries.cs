//using AutoMapper;
//using MediatR;
//using CleanArchitectur.Application.Features.ScenariosFeature.DTOs;
//using Domain.Enums;
//using CleanArchitectur.Domain.Interfaces.Repositories;
//using Domain.Interfaces;

//namespace CleanArchitectur.Application.Features.ScenariosFeature.Queries;

//public record GetScenariosQuery(Guid ProjectId, string? SearchTerm = null, ScenarioStatus? Status = null) : IRequest<IReadOnlyList<ScenarioDto>>;
//public record GetScenarioByIdQuery(Guid Id) : IRequest<ScenarioDetailDto?>;
//public record GetScenarioVersionsQuery(Guid ScenarioId) : IRequest<IReadOnlyList<ScenarioVersionDto>>;
//public record ExportScenarioQuery(Guid Id) : IRequest<string?>;

//public class GetScenariosQueryHandler : IRequestHandler<GetScenariosQuery, IReadOnlyList<ScenarioDto>>
//{
//    private readonly IUnitOfWork _unitOfWork;
//    private readonly IMapper _mapper;

//    public GetScenariosQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
//    {
//        _unitOfWork = unitOfWork;
//        _mapper = mapper;
//    }

//    public async Task<IReadOnlyList<ScenarioDto>> Handle(GetScenariosQuery request, CancellationToken ct)
//    {
//        var scenarios = await _unitOfWork.Scenarios.SearchAsync(request.ProjectId, request.SearchTerm, request.Status, ct);
//        return _mapper.Map<IReadOnlyList<ScenarioDto>>(scenarios);
//    }
//}

//public class GetScenarioByIdQueryHandler : IRequestHandler<GetScenarioByIdQuery, ScenarioDetailDto?>
//{
//    private readonly IUnitOfWork _unitOfWork;
//    private readonly IMapper _mapper;

//    public GetScenarioByIdQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
//    {
//        _unitOfWork = unitOfWork;
//        _mapper = mapper;
//    }

//    public async Task<ScenarioDetailDto?> Handle(GetScenarioByIdQuery request, CancellationToken ct)
//    {
//        var scenario = await _unitOfWork.Scenarios.GetFullAsync(request.Id, ct);
//        return scenario == null ? null : _mapper.Map<ScenarioDetailDto>(scenario);
//    }
//}

//public class ExportScenarioQueryHandler : IRequestHandler<ExportScenarioQuery, string?>
//{
//    private readonly IUnitOfWork _unitOfWork;

//    public ExportScenarioQueryHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

//    public async Task<string?> Handle(ExportScenarioQuery request, CancellationToken ct)
//    {
//        var scenario = await _unitOfWork.Scenarios.GetByIdAsync(request.Id, ct);
//        return scenario?.GherkinContent;
//    }
//}
