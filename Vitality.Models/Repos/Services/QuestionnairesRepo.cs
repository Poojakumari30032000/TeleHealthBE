using AutoMapper;
using Dapper;
using DudeMeds.Models.DTOs.Facilities;
using DudeMeds.Models.DTOs.Patients;
using DudeMeds.Models.DTOs.Questionnaires;
using DudeMeds.Models.Repos.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos;
using Vitality.Models.Repos.Services.Audit;

namespace DudeMeds.Models.Repos.Services
{
    public class QuestionnairesRepo : BaseRepo, IQuestionnairesRepo
    {
        private readonly IMapper _mapper;
        private readonly IAuditService _auditService;

        public QuestionnairesRepo(IMapper mapper, IAuditService auditService)
        {
            _mapper = mapper;
            _auditService = auditService;
        }

        public List<GetAllQuestionnairesResponseDTO> GetAllQuestionnaires(
          GetAllQuestionnairesRequestDTO request,
          out int totalQuestionniareCount)
        {
            var pageNumber = (request?.PageNumber > 0) ? request.PageNumber : 1;
            var pageSize = (request?.PageSize > 0) ? request.PageSize : 10;

            var query = _db.SYS_Questionnaires
                .AsNoTracking()
                .Where(q => q.IsActive == true);

            if (request?.OrganizationId is long orgId)
                query = query.Where(q => q.OrgzanizationId == orgId);

            if (!string.IsNullOrWhiteSpace(request?.Title))
                query = query.Where(q => (q.QuestionnaireName ?? "").Contains(request.Title));

            if (request?.StartDate is DateTime start)
                query = query.Where(q => q.CreatedDate >= start);

            if (request?.EndDate is DateTime end)
                query = query.Where(q => q.CreatedDate < end);

            if (!string.IsNullOrWhiteSpace(request?.Status))
                query = query.Where(q => q.Status == request.Status);

            if (request?.FacilityId is long facilityId && facilityId > 0)
            {
                var visibleIds =
                    from qp in _db.SYS_QuestionnairesInProducts.AsNoTracking()
                    join fc in _db.PD_FacilityCategories.AsNoTracking()
                          on qp.CategoryId equals fc.CategoryId
                    where fc.FacilityId == facilityId && fc.IsActive == true
                    select qp.QuestionnaireId;

                query = query.Where(q => visibleIds.Contains(q.QuestionnaireId));
            }

            totalQuestionniareCount = query.Count();

            var result = query
                .OrderBy(q => q.QuestionnaireId)
                .Skip(pageSize * (pageNumber - 1))
                .Take(pageSize)
                .Select(q => new GetAllQuestionnairesResponseDTO
                {
                    Guid = q.Guid,
                    QuestionnaireId = q.QuestionnaireId,
                    QuestionnaireName = q.QuestionnaireName,
                    Language = q.Language,
                    Status = q.Status,
                    Review = q.Review,
                    CreatedDate = q.CreatedDate,
                    QuestionCount = q.QuestionCount,
                    QuestionaireType = q.QuestionaireType,
                    ProductCount = _db.SYS_QuestionnairesInProducts
                                      .Where(x => x.QuestionnaireId == q.QuestionnaireId)
                                      .Select(x => x.ProductId)
                                      .Distinct()
                                      .Count()
                })
                .ToList();

            return result;
        }

        public GetQuestionnaireByIdResponseDTO? GetQuestionnaireById(long questionnaireId, long? facilityId = null)
        {
            var questionnaire = _db.SYS_Questionnaires
                .AsNoTracking()
                .FirstOrDefault(x => x.QuestionnaireId == questionnaireId);

            if (questionnaire == null) return null;

            var response = _mapper.Map<GetQuestionnaireByIdResponseDTO>(questionnaire);

            var qip = _db.SYS_QuestionnairesInProducts
                         .AsNoTracking()
                         .Where(x => x.QuestionnaireId == questionnaireId);

            if (facilityId is long fid && fid > 0)
            {

                var facilityCategoryIds = _db.PD_FacilityCategories
                    .AsNoTracking()
                    .Where(fc => fc.FacilityId == fid && fc.IsActive == true)
                    .Select(fc => fc.CategoryId)
                    .ToList();

                var hasVisibility = qip.Any(x => x.CategoryId.HasValue &&
                                                 facilityCategoryIds.Contains(x.CategoryId.Value));
                if (!hasVisibility) return null;

                response.ProductId = qip
                    .Where(x => x.CategoryId.HasValue && facilityCategoryIds.Contains(x.CategoryId.Value))
                    .GroupBy(x => x.ProductId)
                    .Select(g => g.Key)
                    .ToList();

                response.CategoryId = qip
                    .Where(x => x.CategoryId.HasValue && facilityCategoryIds.Contains(x.CategoryId.Value))
                    .Select(x => x.CategoryId!.Value)
                    .Distinct()
                    .ToList();
            }
            else
            {

                response.ProductId = qip
                    .GroupBy(x => x.ProductId)
                    .Select(g => g.Key)
                    .ToList();

                response.CategoryId = qip
                    .Where(x => x.CategoryId.HasValue)
                    .Select(x => x.CategoryId!.Value)
                    .Distinct()
                    .ToList();
            }

            response.CreatedByName = _db.SYS_UserDetails
                .Where(x => x.UserId == questionnaire.CreatedBy)
                .Select(x => (x.FirstName ?? "") + " " + (x.LastName ?? ""))
                .FirstOrDefault();

            return response;
        }

        public string SaveQuestionnaire(SaveQuestionnaireRequestDTO request, long UserId, long OrganizationId)
        {

            try
            {
                SYS_Questionnaire questionnaire = new SYS_Questionnaire();
                Guid guid = Guid.NewGuid();
                if (request.QuestionnaireId == 0)
                {
                    questionnaire = _mapper.Map<SYS_Questionnaire>(request);
                    questionnaire.Guid = guid.ToString();
                    questionnaire.CreatedBy = UserId;
                    questionnaire.CreatedDate = DateTime.UtcNow;
                    questionnaire.IsActive = true;
                    questionnaire.Status = "Pending";
                    questionnaire.Language = "English";
                    questionnaire.QuestionCount = 0;
                    questionnaire.OrgzanizationId = OrganizationId;
                    _db.SYS_Questionnaires.Add(questionnaire);
                    _db.SaveChanges();

                    foreach (var categoryId in request.CategoryId)
                    {
                        SYS_QuestionnairesInProduct newItem = new SYS_QuestionnairesInProduct
                        {
                            QuestionnaireId = questionnaire.QuestionnaireId,

                            CategoryId = categoryId,
                            QuestionaireType = request.QuestionaireType,
                        };
                        _db.SYS_QuestionnairesInProducts.Add(newItem);
                    }
                    _db.SaveChanges();

                    _auditService.LogEntityChange(
                        action: "Create",
                        entityType: "SYS_Questionnaire",
                        entityId: questionnaire.QuestionnaireId,
                        newValues: questionnaire,
                        userId: UserId,
                        description: $"Intake Form Question '{questionnaire.QuestionnaireName}' created - Type: {questionnaire.QuestionaireType}, Question Count: {questionnaire.QuestionCount}",
                        module: "IntakeForm"
                    );

                    return "Questionnaire Created Successfully";

                }
                else
                {
                    questionnaire = _db.SYS_Questionnaires.Where(x => x.QuestionnaireId == request.QuestionnaireId).FirstOrDefault();
                    if(request.QuestionnaireJson != null)
                    {
                        questionnaire.QuestionnaireJson = request.QuestionnaireJson;
                        questionnaire.QuestionCount = request.QuestionCount;
                        questionnaire.Status = request.Status;
                    }
                    else
                    {
                        questionnaire.QuestionnaireName = request.QuestionnaireName;
                        questionnaire.QuestionaireType = request.QuestionaireType;
                        questionnaire.QuestionnaireJson = questionnaire.QuestionnaireJson;
                        questionnaire.QuestionCount = questionnaire.QuestionCount;
                        questionnaire.ModifiedBy = UserId;
                        questionnaire.ModifiedDate = DateTime.UtcNow;
                    }
                    _db.SaveChanges();

                    if (request.CategoryId?.Count != 0)
                    {
                        List<SYS_QuestionnairesInProduct> list = new List<SYS_QuestionnairesInProduct>();
                        list = _db.SYS_QuestionnairesInProducts.Where(x => x.QuestionnaireId == request.QuestionnaireId).ToList();
                        _db.SYS_QuestionnairesInProducts.RemoveRange(list);
                        _db.SaveChanges();

                        foreach (var category in request.CategoryId)
                        {
                            SYS_QuestionnairesInProduct newItem = new SYS_QuestionnairesInProduct
                            {
                                QuestionnaireId = questionnaire.QuestionnaireId,
                                CategoryId = category,
                                QuestionaireType = request.QuestionaireType,
                            };
                            _db.SYS_QuestionnairesInProducts.Add(newItem);
                        }
                        _db.SaveChanges();

                    }

                    return "Questionnaire Updated Successfully";

                }
            }
            catch (Exception ex)
            {
                return "Something went wrong. Please try agmin later.";
            }
        }

        public bool DeleteQuestionnaire(long QuestionnaireId)
        {
            try
            {
                SYS_Questionnaire questionnaire = _db.SYS_Questionnaires.Where(x => x.QuestionnaireId == QuestionnaireId).FirstOrDefault();
                if (questionnaire != null)
                {
                    questionnaire.IsActive = false;

                    List<SYS_QuestionnairesInProduct> questionnairesInProduct = _db.SYS_QuestionnairesInProducts.Where(x => x.QuestionnaireId == QuestionnaireId).ToList();
                    _db.RemoveRange(questionnairesInProduct);

                    _db.SaveChanges();

                    _auditService.LogEntityChange(
                        action: "Delete",
                        entityType: "SYS_Questionnaire",
                        entityId: questionnaire.QuestionnaireId,
                        oldValues: questionnaire,
                        description: $"Intake Form Question '{questionnaire.QuestionnaireName}' deleted",
                        module: "IntakeForm"
                    );

                    return true;
                }
                else
                {
                    return false;
                }

            }
            catch
            {
                return false;
            }
        }

        public bool UpdateQuestionnaireStatus(UpdateQuestionnaireStatusRequestDTO request)
        {
            SYS_Questionnaire questionnaire = _db.SYS_Questionnaires.Where(x => x.QuestionnaireId == request.QuestionnaireId).FirstOrDefault();
            if (questionnaire != null)
            {
                questionnaire.Status = request.Status;
                questionnaire.Review = request.Review;
                _db.SaveChanges();
                return true;
            }
            else
            {
                return false;
            }

        }
        public bool UpdateQuestionnaireJson(UpdateQuestionnaireJsonRequestDTO request)
        {
            var questionnaire = _db.SYS_Questionnaires
                                   .FirstOrDefault(x => x.QuestionnaireId == request.QuestionnaireId);
            if (questionnaire == null) return false;

            questionnaire.QuestionnaireJson = request.Json;
            questionnaire.ModifiedDate = DateTime.UtcNow;
            _db.SaveChanges();
            return true;
        }

        public long DuplicateQuestionnaire(DuplicateQuestionnaireRequestDTO request, long UserId, long OrganizationId)
        {

            try
            {
                SYS_Questionnaire questionnaire = _db.SYS_Questionnaires.Where(x => x.QuestionnaireId == request.QuestionnaireId).FirstOrDefault();
                if (questionnaire != null)
                {
                    Guid guid = Guid.NewGuid();
                    SYS_Questionnaire newQuestionnaire = new SYS_Questionnaire();
                    newQuestionnaire.QuestionnaireId = 0;
                    newQuestionnaire.QuestionnaireName = questionnaire.QuestionnaireName;
                    newQuestionnaire.QuestionnaireJson = questionnaire.QuestionnaireJson;
                    newQuestionnaire.QuestionaireType = questionnaire.QuestionaireType;
                    newQuestionnaire.CreatedDate = DateTime.UtcNow;
                    newQuestionnaire.CreatedBy = UserId;
                    newQuestionnaire.Guid = guid.ToString();
                    newQuestionnaire.IsActive = true;
                    newQuestionnaire.Status = "Pending";
                    newQuestionnaire.QuestionCount = questionnaire.QuestionCount;
                    newQuestionnaire.Language = questionnaire.Language;
                    newQuestionnaire.OrgzanizationId = OrganizationId;
                    _db.SYS_Questionnaires.Add(newQuestionnaire);
                    _db.SaveChanges();
                    return newQuestionnaire.QuestionnaireId;

                }
                else
                {
                    return 0;

                }
            }
            catch (Exception ex)
            {
                return -1;
            }
        }

        public GetQuestionnaireJsonByIdResponseDTO GetQuestionnaireJsonById(GetQuestionnaireJsonByIdRequestDTO request)
        {
            GetQuestionnaireJsonByIdResponseDTO response = new GetQuestionnaireJsonByIdResponseDTO();
            SYS_QuestionnairesInProduct questionnaireProduct = _db.SYS_QuestionnairesInProducts.Where(x => x.CategoryId == request.CategoryId).FirstOrDefault();
            if (questionnaireProduct != null)
            {
                SYS_Questionnaire questionnaire = _db.SYS_Questionnaires.Where(x => x.QuestionnaireId == questionnaireProduct.QuestionnaireId).FirstOrDefault();
                if(questionnaire != null)
                {
                    response.QuestionnaireId = questionnaire.QuestionnaireId;
                    response.QuestionnaireName = questionnaire.QuestionnaireName;
                    response.QuestionnaireJson = questionnaire.QuestionnaireJson;
                    response.QuestionaireType = questionnaire.QuestionaireType;
                }
            }
            return response;

        }

        public bool UpsertFacilityQuestionnaireJson(UpdateFacilityQuestionnaireJsonRequestDTO request, long userId)
        {

            var questionnaire = _db.SYS_Questionnaires
                                   .AsNoTracking()
                                   .FirstOrDefault(x => x.QuestionnaireId == request.QuestionnaireId && x.IsActive == true);
            if (questionnaire == null) return false;

            var allowed =
                (from qp in _db.SYS_QuestionnairesInProducts.AsNoTracking()
                 join fc in _db.PD_FacilityCategories.AsNoTracking() on qp.CategoryId equals fc.CategoryId
                 where qp.QuestionnaireId == request.QuestionnaireId
                    && fc.FacilityId == request.FacilityId
                    && fc.IsActive == true
                 select 1).Any();

            if (!allowed) return false;

            var now = DateTime.UtcNow;
            var set = _db.Set<SYS_QuestionnaireFacilityJson>();

            var row = set.FirstOrDefault(x =>
                x.QuestionnaireId == request.QuestionnaireId &&
                x.FacilityId == request.FacilityId &&
                x.IsActive == true);

            if (row == null)
            {
                row = new SYS_QuestionnaireFacilityJson
                {
                    QuestionnaireId = request.QuestionnaireId,
                    FacilityId = request.FacilityId,
                    QuestionnaireJson = request.Json,
                    IsActive = true,
                    CreatedBy = userId,
                    CreatedDate = now
                };
                set.Add(row);
            }
            else
            {
                row.QuestionnaireJson = request.Json;
                row.ModifiedBy = userId;
                row.ModifiedDate = now;
            }

            _db.SaveChanges();
            return true;
        }

        public GetQuestionnaireJsonByIdResponseDTO GetQuestionnaireJson(GetQuestionnaireJsonRequestDTO request)
        {
            var response = new GetQuestionnaireJsonByIdResponseDTO();

            var q = _db.SYS_Questionnaires
                       .AsNoTracking()
                       .FirstOrDefault(x => x.QuestionnaireId == request.QuestionnaireId && x.IsActive == true);

            if (q == null) return response;

            string? jsonToReturn = q.QuestionnaireJson;

            if (request.FacilityId is long facilityId && facilityId > 0)
            {

                var allowed =
                    (from qp in _db.SYS_QuestionnairesInProducts.AsNoTracking()
                     join fc in _db.PD_FacilityCategories.AsNoTracking() on qp.CategoryId equals fc.CategoryId
                     where qp.QuestionnaireId == request.QuestionnaireId
                        && fc.FacilityId == facilityId
                        && fc.IsActive == true
                     select 1).Any();

                if (allowed)
                {
                    var overrideRow = _db.Set<SYS_QuestionnaireFacilityJson>()
                        .AsNoTracking()
                        .FirstOrDefault(x => x.QuestionnaireId == request.QuestionnaireId
                                          && x.FacilityId == facilityId
                                          && x.IsActive == true);

                    if (overrideRow?.QuestionnaireJson != null)
                        jsonToReturn = overrideRow.QuestionnaireJson;
                }
            }

            response.QuestionnaireId = q.QuestionnaireId;
            response.QuestionnaireName = q.QuestionnaireName;
            response.QuestionaireType = q.QuestionaireType;
            response.QuestionnaireJson = jsonToReturn;

            return response;
        }
    }
}
