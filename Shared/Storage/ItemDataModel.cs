using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Storage
{
    public class ItemDataModel
    {
        public int CustomerId { get; set; }
        public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ItemData { get; set; }

        public bool ContainsHelloWorld { get; set; }
        public bool IsPalindrome { get; set; }
    }
}
