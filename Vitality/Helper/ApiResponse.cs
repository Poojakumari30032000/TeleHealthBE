using System.Collections;
using System.Linq;

namespace Vitality.Helper
{
    public class ApiResponse<T>
    {
        public int Status { get; set; } = 1;
        public bool? Success { get; set; }
        public string? Message { get; set; } = "Success";
        public int Count { get; private set; } = 0;

        private T _data;

        public T Data
        {
            get => _data;
            set
            {
                _data = value;
                Count = CalculateRecordCount(value);
            }
        }
        private int CalculateRecordCount(T value)
        {
            if (value is ICollection collection)
            {
                return collection.Count;
            }

            return value != null ? 1 : 0;
        }

        public int? TotalEntityCount { get; set; }
        public decimal? TotalPages { get; set; }
    }

    public  class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }
}
