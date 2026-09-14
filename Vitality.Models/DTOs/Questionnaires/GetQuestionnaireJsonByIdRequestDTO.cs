using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Questionnaires
{
    public class GetQuestionnaireJsonByIdRequestDTO
    {
        public long? ProductId { get; set; }
        public long? CategoryId { get; set; }
    }
}
