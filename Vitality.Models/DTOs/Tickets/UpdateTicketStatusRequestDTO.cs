using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.DTOs.Tickets
{
    public class UpdateTicketStatusRequestDTO
    {
        public long Id { get; set; }

        public int Status { get; set; }
    }
}
