//using AutoMapper;
//using CleanArchitectur.Application.Features.ScenariosFeature.DTOs;
//using Domain.Common;
//using Domain.Entities.Scenarios;
//using Domain.Enums;
//using Domain.Interfaces;
//using Domain.Interfaces.Services;
//using MediatR;

//namespace CleanArchitectur.Application.Features.ScenariosFeature.Commands;

//public class CreateScenarioCommandHandler : IRequestHandler<CreateScenarioCommand, Result<ScenarioDetailDto>>
//{
//    private readonly IUnitOfWork _unitOfWork;
//    private readonly IGherkinParserService _gherkinParser;
//    private readonly IMapper _mapper;

//    public CreateScenarioCommandHandler(IUnitOfWork unitOfWork, IGherkinParserService gherkinParser, IMapper mapper)
//    {
//        _unitOfWork = unitOfWork;
//        _gherkinParser = gherkinParser;
//        _mapper = mapper;
//    }

//    public async Task<Result<ScenarioDetailDto>> Handle(CreateScenarioCommand request, CancellationToken ct)
//    {
//        // Valider la syntaxe Gherkin
//        if (!_gherkinParser.ValidateSyntax(request.GherkinContent, out var errors))
//            return Result<ScenarioDetailDto>.Failure(errors.ToList());

//        var scenario = new Scenario
//        {
//            FeatureId = request.FeatureId,
//            Title = request.Title,
//            Description = request.Description,
//            GherkinContent = request.GherkinContent,
//            Status = ScenarioStatus.Draft,
//            CurrentVersion = 1,
//            CreatedById = request.CreatedById
//        };

//        // Parser les steps depuis le contenu Gherkin
//        var (_, parsedScenarios) = _gherkinParser.ParseFeatureContent(request.GherkinContent);
//        if (parsedScenarios.Any())
//        {
//            int order = 0;
//            foreach (var step in parsedScenarios.First().Steps)
//            {
//                scenario.Steps.Add(new Step
//                {
//                    StepType = Enum.Parse<StepType>(step.Keyword, true),
//                    Text = step.Text,
//                    DisplayOrder = order++,
//                    DataTable = step.DataTable
//                });
//            }
//        }

//        // Créer la version initiale
//        scenario.Versions.Add(new ScenarioVersion
//        {
//            VersionNumber = 1,
//            GherkinContent = request.GherkinContent,
//            ChangeDescription = "Création initiale",
//            CreatedById = request.CreatedById
//        });

//        await _unitOfWork.Scenarios.AddAsync(scenario, ct);
//        await _unitOfWork.SaveChangesAsync(ct);

//        var created = await _unitOfWork.Scenarios.GetFullAsync(scenario.Id, ct);
//        return Result<ScenarioDetailDto>.Success(_mapper.Map<ScenarioDetailDto>(created));
//    }
//}

//public class UpdateScenarioCommandHandler : IRequestHandler<UpdateScenarioCommand, Result<ScenarioDetailDto>>
//{
//    private readonly IUnitOfWork _unitOfWork;
//    private readonly IGherkinParserService _gherkinParser;
//    private readonly IMapper _mapper;

//    public UpdateScenarioCommandHandler(IUnitOfWork unitOfWork, IGherkinParserService gherkinParser, IMapper mapper)
//    {
//        _unitOfWork = unitOfWork;
//        _gherkinParser = gherkinParser;
//        _mapper = mapper;
//    }

//    public async Task<Result<ScenarioDetailDto>> Handle(UpdateScenarioCommand request, CancellationToken ct)
//    {
//        var scenario = await _unitOfWork.Scenarios.GetFullAsync(request.Id, ct)
//            ?? throw new NotFoundException(nameof(Scenario), request.Id);

//        if (!_gherkinParser.ValidateSyntax(request.GherkinContent, out var errors))
//            return Result<ScenarioDetailDto>.Failure(errors.ToList());

//        scenario.Title = request.Title;
//        scenario.Description = request.Description;
//        scenario.GherkinContent = request.GherkinContent;
//        scenario.CurrentVersion++;
//        scenario.UpdatedAt = DateTime.UtcNow;

//        // Ajouter nouvelle version
//        scenario.Versions.Add(new ScenarioVersion
//        {
//            VersionNumber = scenario.CurrentVersion,
//            GherkinContent = request.GherkinContent,
//            ChangeDescription = request.ChangeDescription ?? $"Mise à jour v{scenario.CurrentVersion}",
//            CreatedById = request.UpdatedById
//        });

//        await _unitOfWork.Scenarios.UpdateAsync(scenario, ct);
//        await _unitOfWork.SaveChangesAsync(ct);

//        return Result<ScenarioDetailDto>.Success(_mapper.Map<ScenarioDetailDto>(scenario));
//    }
//}

//public class ValidateGherkinCommandHandler : IRequestHandler<ValidateGherkinCommand, ValidationResultDto>
//{
//    private readonly IGherkinParserService _gherkinParser;

//    public ValidateGherkinCommandHandler(IGherkinParserService gherkinParser) => _gherkinParser = gherkinParser;

//    public Task<ValidationResultDto> Handle(ValidateGherkinCommand request, CancellationToken ct)
//    {
//        var isValid = _gherkinParser.ValidateSyntax(request.GherkinContent, out var errors);
//        return Task.FromResult(new ValidationResultDto(isValid, errors));
//    }
//}
