using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vitality.Models.DTOs.Common
{
    public class UploadFileResponseDTO
    {
        public string? FileName { get; set; }
        public string? RelativePath { get; set; }
        public string? Url { get; set; }
        public long Size { get; set; }
        public string? ContentType { get; set; }
    }
}
