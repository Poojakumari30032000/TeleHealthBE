using DudeMeds.Models.DTOs.Questionnaires;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.Repos.Interfaces
{
    public interface IQuestionnairesRepo
    {
        public List<GetAllQuestionnairesResponseDTO> GetAllQuestionnaires(GetAllQuestionnairesRequestDTO request, out int totalQuestionniareCount);
        GetQuestionnaireByIdResponseDTO? GetQuestionnaireById(long questionnaireId, long? facilityId = null);
        public string SaveQuestionnaire(SaveQuestionnaireRequestDTO request, long UserId, long OrganizationId);
        public bool DeleteQuestionnaire(long QuestionnaireId);
        public bool UpdateQuestionnaireStatus(UpdateQuestionnaireStatusRequestDTO request);
        public bool UpdateQuestionnaireJson(UpdateQuestionnaireJsonRequestDTO request);
        public long DuplicateQuestionnaire(DuplicateQuestionnaireRequestDTO request, long UserId, long OrganizationId);
        bool UpsertFacilityQuestionnaireJson(UpdateFacilityQuestionnaireJsonRequestDTO request, long userId);
        public GetQuestionnaireJsonByIdResponseDTO GetQuestionnaireJsonById(GetQuestionnaireJsonByIdRequestDTO request);
        GetQuestionnaireJsonByIdResponseDTO GetQuestionnaireJson(GetQuestionnaireJsonRequestDTO request);
    }
}
