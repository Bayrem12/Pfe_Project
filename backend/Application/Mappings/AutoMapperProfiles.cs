using Application.Features.FeatureFeature.Dtos;
using Application.Features.ModulesFeature.Commands;
using Application.Features.ModulesFeature.DTOs;
using Application.Features.NlpFeature.Dtos;
using Application.Features.ProjectFeature.Commands;
using Application.Features.ProjectFeature.Dtos;
using Application.Features.ScenariosFeature.DTOs;
using Application.Features.TestFeature.Commands;
using Application.Features.TestFeature.Dtos;
using Application.Features.TestSuitesFeature.DTOs;
using Application.Features.UserFeature.Commands;
using Application.Features.UserFeature.Dtos;

using Application.Users.DTOs;
using AutoMapper;
using Domain.Common;
using Domain.Entities;
using Domain.Entities.Identity;
using Domain.Entities.NLP;
using Domain.Entities.ProjectManagement;
using Domain.Entities.Scenarios;
using System.Reflection;
using Module = Domain.Entities.ProjectManagement.Module;

namespace Application.Mappings
{
    public class AutoMapperProfiles : Profile
    {
        public AutoMapperProfiles()
        {
            // Test mappings
            CreateMap<AddTestCommandNew, Test>();
            CreateMap<PagedList<Test>, PagedList<TestDTO>>().ReverseMap();
            CreateMap<Test, TestDTO>().ReverseMap();

            // Project mappings
            CreateMap<AddProjectCommand, Project>();
            CreateMap<PagedList<Project>, PagedList<ProjectDTO>>().ReverseMap();
            CreateMap<Project, ProjectDTO>()
                .ForMember(dest => dest.Members, opt => opt.MapFrom(src => src.Members))
                .ForMember(dest => dest.ModulesCount, opt => opt.Ignore())
                .ForMember(dest => dest.ScenariosCount, opt => opt.Ignore())
                .ForMember(dest => dest.LastRun, opt => opt.Ignore());
            CreateMap<ProjectDTO, Project>();

            // ProjectMember mappings
            CreateMap<ProjectMember, ProjectMemberDTO>()
                .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src =>
                    src.User != null
                        ? (!string.IsNullOrWhiteSpace(src.User.FirstName)
                            ? src.User.FirstName
                            : (!string.IsNullOrWhiteSpace(src.User.FullName) ? src.User.FullName : src.User.Email))
                        : string.Empty))
                .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.User != null ? src.User.LastName : string.Empty))
                .ForMember(dest => dest.UserName, opt => opt.MapFrom(src =>
                    src.User != null && !string.IsNullOrWhiteSpace(src.User.FullName)
                        ? src.User.FullName
                        : (src.User != null ? src.User.Email : string.Empty)))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.User != null ? src.User.Email : string.Empty))
                .ForMember(dest => dest.Role, opt => opt.MapFrom(src =>
                    // Use the user's global role name when available, otherwise fall back to the project role enum
                    (src.User != null && src.User.UserRoles != null && src.User.UserRoles.Any() && src.User.UserRoles.First().Role != null && !string.IsNullOrWhiteSpace(src.User.UserRoles.First().Role.Name))
                        ? src.User.UserRoles.First().Role.Name
                        : src.Role.ToString()));
            CreateMap<ProjectMemberDTO, ProjectMember>();

            // ===== NLP Feature Mappings =====
            // StepAnalysis entity → StepAnalysisDto
            // .ForMember customizes how a specific property is mapped:
            // - Intent (enum) is converted to its string name (e.g., StepIntentType.Click → "Click")
            // - Parameters (ICollection<StepParameter>) mapped to List<StepParameterDto>
            CreateMap<StepAnalysis, StepAnalysisDto>()
                .ForMember(dest => dest.Intent,
                    opt => opt.MapFrom(src => src.Intent.ToString()));  // Enum → string

            // StepParameter entity → StepParameterDto (straightforward 1:1 mapping)
            CreateMap<StepParameter, StepParameterDto>();

            // ActionMapping entity → ActionMappingDto
            // ActionType enum is converted to its string name for the API response
            CreateMap<ActionMapping, ActionMappingDto>()
                .ForMember(dest => dest.ActionType,
                    opt => opt.MapFrom(src => src.ActionType.ToString()));  // Enum → string

            // Auth

            // Commands
            CreateMap<AddUserRegister, User>();
            CreateMap<AddUserLogin, User>();
            CreateMap<AddUserRefresh, User>();
            CreateMap<AddUserChangePassword, User>();

            CreateMap<PagedList<User>, PagedList<UserDTO>>().ReverseMap();

            //Dto
            CreateMap<User, UserDTO>().ReverseMap();

            // User 

            // Commands
            CreateMap<AddToggleUserStatusCommand, User>();
            CreateMap<UpdateUserByIdCommand, User>();
            CreateMap<UpdateUserRolesByIdCommand, User>();
            CreateMap<PagedList<User>, PagedList<UserDto>>().ReverseMap();  // Pagination

            //Dto
            CreateMap<User, UserDTO>().ReverseMap();






            // Modules

            // Commands
            CreateMap<AddModulesCommand, Module>();
            CreateMap<PagedList<Module>, PagedList<ModulesDTO>>().ReverseMap();

            //Dto
            CreateMap<Module, TestDTO>().ReverseMap();
            // Scenario mappings
            CreateMap<Scenario, ScenarioDto>()
                .ForMember(dest => dest.FeatureName, opt => opt.MapFrom(src => src.Feature != null ? src.Feature.Name : string.Empty))
                .ForMember(dest => dest.ModuleName, opt => opt.MapFrom(src => src.Feature != null && src.Feature.Module != null ? src.Feature.Module.Name : string.Empty))
                .ForMember(dest => dest.StepCount, opt => opt.MapFrom(src => src.Steps != null ? src.Steps.Count(s => !s.IsDeleted) : 0))
                .ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.ScenarioTags.Where(st => st.Tag != null).Select(st => st.Tag.Name).ToList()))
                .ForMember(dest => dest.LastTestStatus, opt => opt.MapFrom(src =>
                    src.TestResults != null && src.TestResults.Any()
                        ? src.TestResults.OrderByDescending(tr => tr.CompletedAt).First().Status.ToString()
                        : null));
            CreateMap<Scenario, ScenarioDetailDto>()
                .ForMember(dest => dest.FeatureName, opt => opt.MapFrom(src => src.Feature != null ? src.Feature.Name : string.Empty))
                .ForMember(dest => dest.ModuleId, opt => opt.MapFrom(src => src.Feature != null ? src.Feature.ModuleId : (Guid?)null))
                .ForMember(dest => dest.ModuleName, opt => opt.MapFrom(src => src.Feature != null && src.Feature.Module != null ? src.Feature.Module.Name : string.Empty))
                .ForMember(dest => dest.ProjectId, opt => opt.MapFrom(src => src.Feature != null && src.Feature.Module != null ? src.Feature.Module.ProjectId : (Guid?)null))
                .ForMember(dest => dest.ProjectName, opt => opt.MapFrom(src => src.Feature != null && src.Feature.Module != null && src.Feature.Module.Project != null ? src.Feature.Module.Project.Name : string.Empty))
                .ForMember(dest => dest.Steps, opt => opt.MapFrom(src => src.Steps.Where(s => !s.IsDeleted).OrderBy(s => s.DisplayOrder).ToList()))
                .ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.ScenarioTags.Where(st => st.Tag != null).Select(st => st.Tag.Name).ToList()))
                .ForMember(dest => dest.LastTestStatus, opt => opt.MapFrom(src =>
                    src.TestResults != null && src.TestResults.Any()
                        ? src.TestResults.OrderByDescending(tr => tr.CompletedAt).First().Status.ToString()
                        : null));
            CreateMap<Step, StepDto>();
            CreateMap<ScenarioVersion, ScenarioVersionDto>();
            // Feature mappings
            CreateMap<Feature, FeatureDTO>()
                .ForMember(dest => dest.ModuleName, opt => opt.MapFrom(src => src.Module != null ? src.Module.Name : string.Empty))
                .ForMember(dest => dest.ScenarioCount, opt => opt.MapFrom(src => src.Scenarios != null ? src.Scenarios.Count : 0));
            CreateMap<Feature, FeatureListDTO>()
                .ForMember(dest => dest.ScenarioCount, opt => opt.MapFrom(src => src.Scenarios != null ? src.Scenarios.Count : 0));

            // TestSuite mappings
            CreateMap<TestSuite, TestSuiteDTO>();
            CreateMap<TestSuite, TestSuiteWithCasesDTO>()
                .ForMember(dest => dest.Scenarios, opt => opt.MapFrom(src => src.TestSuiteScenarios));
            CreateMap<TestSuiteScenario, TestSuiteScenarioDTO>()
                .ForMember(dest => dest.ScenarioTitle,
                    opt => opt.MapFrom(src => src.Scenario != null ? src.Scenario.Title : string.Empty))
                .ForMember(dest => dest.ScenarioDescription,
                    opt => opt.MapFrom(src => src.Scenario != null ? src.Scenario.Description : string.Empty));

        }
    }
}
