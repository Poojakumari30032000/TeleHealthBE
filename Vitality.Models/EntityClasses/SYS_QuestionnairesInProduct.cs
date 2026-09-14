using System;
using System.Collections.Generic;

namespace Vitality.Models.EntityClasses
{
    public partial class SYS_QuestionnairesInProduct
    {
        public long QuestionnaireInProductId { get; set; }
        public long? QuestionnaireId { get; set; }
        public string? QuestionaireType { get; set; }
        public long ProductId { get; set; }
        public long? CategoryId { get; set; }
    }
}
